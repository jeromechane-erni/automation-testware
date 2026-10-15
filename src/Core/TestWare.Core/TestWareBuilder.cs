using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json.Nodes;
using TestWare.Core.Configuration;
using TestWare.Core.Interfaces;

namespace TestWare.Core;

/// <summary>
/// Registration used by <see cref="TestWareProvider.Configure(string, Action{TestWareBuilder})"/>.
/// Only the engines, cockpits, page sets and components named here exist. Nothing is scanned.
/// The caller names the types; the container builds them, filling constructor parameters from what is registered.
/// </summary>
public sealed class TestWareBuilder
{
    private readonly Dictionary<string, Func<ConfigurationScope, IServiceCollection, Type>> _engines = new();
    private readonly Dictionary<string, Action<ConfigurationScope, IServiceCollection>> _cockpits = new();
    private readonly List<Action<IServiceCollection, IReadOnlyDictionary<string, Type>>> _pageSets = new();
    private readonly List<Action<IServiceCollection, IReadOnlyDictionary<string, Type>>> _components = new();
    private readonly HashSet<(Type Type, string Scope)> _registered = new();

    /// <summary>
    /// A scope whose CoreName is <paramref name="coreName"/> gets a <typeparamref name="TEngine"/> built from the scope's Config.
    /// The engine needs a public constructor taking a <see cref="JsonObject"/>.
    /// It is reachable by its class and by each of its engine interfaces (ISeleniumEngine, ITestWareEngine...), keyed by scope name.
    /// One instance per test: it is created when the test first asks for it and disposed when the test ends.
    /// </summary>
    public TestWareBuilder AddEngine<TEngine>(string coreName) where TEngine : class, ITestWareEngine
    {
        RequireConfigConstructor(typeof(TEngine));
        _engines[coreName] = (scope, services) =>
        {
            var key = scope.ScopeName;
            services.AddKeyedScoped<TEngine>(key, (sp, _) =>
            {
                var engine = ActivatorUtilities.CreateInstance<TEngine>(sp, scope.Config);
                sp.GetRequiredService<TestEngines>().Created.Add(engine);   // the test's scope closes it, see TestScope.Close
                return engine;
            });
            foreach (var engineInterface in typeof(TEngine).GetInterfaces().Where(i => typeof(ITestWareEngine).IsAssignableFrom(i)))
                services.AddKeyedScoped(engineInterface, key, (sp, _) => sp.GetRequiredKeyedService<TEngine>(key));
            return typeof(TEngine);
        };
        return this;
    }

    /// <summary>
    /// Same as AddEngine, for reporters. The unkeyed <see cref="ITestWareCockpit"/> is the first cockpit scope in the configuration.
    /// </summary>
    public TestWareBuilder AddCockpit<TCockpit>(string coreName) where TCockpit : class, ITestWareCockpit
    {
        RequireConfigConstructor(typeof(TCockpit));
        _cockpits[coreName] = (scope, services) =>
        {
            var key = scope.ScopeName;
            services.AddKeyedSingleton<TCockpit>(key, (sp, _) => ActivatorUtilities.CreateInstance<TCockpit>(sp, scope.Config));
            services.TryAddSingleton<TCockpit>(sp => sp.GetRequiredKeyedService<TCockpit>(key));
            foreach (var cockpitInterface in typeof(TCockpit).GetInterfaces().Where(i => typeof(ITestWareCockpit).IsAssignableFrom(i)))
            {
                services.AddKeyedSingleton(cockpitInterface, key, (sp, _) => sp.GetRequiredKeyedService<TCockpit>(key));
                services.TryAddSingleton(cockpitInterface, sp => sp.GetRequiredKeyedService<TCockpit>(key));
            }
        };
        return this;
    }

    /// <summary>
    /// Page set <typeparamref name="TPages"/>, implemented by <typeparamref name="TImpl"/>, for every scope whose engine fits the
    /// engine of its pages. One line per application and engine, for example
    /// <c>AddPages&lt;ISwagLabsPages, SeleniumSwagLabsPages&gt;()</c>.
    /// The implementation lists its pages as constructor parameters, <c>SeleniumSwagLabsPages(LoginPage login)</c>, and the container
    /// builds them from the scope's engine, so nobody writes <c>new LoginPage(engine)</c>. The interface still forces every page to
    /// be exposed, so a page missing for an engine is a build error. A page constructor must not touch the browser: all the pages
    /// of a set are built together. The engine is the one engine parameter found in the constructors of the pages (and of
    /// <typeparamref name="TImpl"/> itself, which may also take it directly, <c>SeleniumSwagLabsPages(ISeleniumEngine engine)</c>).
    /// Each property of the page set (<c>ILoginPage Login</c>) can then be asked for directly: <c>Page&lt;ILoginPage&gt;()</c>.
    /// </summary>
    public TestWareBuilder AddPages<TPages, TImpl>() where TPages : class where TImpl : class, TPages
    {
        // The properties of the page set interface, including those of interfaces it extends. Only this one interface is read.
        var pages = typeof(TPages).GetInterfaces().Prepend(typeof(TPages)).SelectMany(t => t.GetProperties()).ToList();
        // Constructor parameters of TImpl that are page classes (LoginPage for ILoginPage Login), built by the container.
        var pageClasses = typeof(TImpl).GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .Where(t => t.IsClass && !t.IsAbstract && pages.Any(page => page.PropertyType.IsAssignableFrom(t)))
            .Distinct()
            .ToList();
        var engineParameter = EngineParameterOf(typeof(TImpl), pageClasses);

        _pageSets.Add((services, scopeEngines) =>
        {
            foreach (var (scope, engineType) in scopeEngines)
            {
                if (!engineParameter.IsAssignableFrom(engineType))
                    continue;
                Claim(typeof(TPages), scope);
                services.AddKeyedTransient<TPages>(scope, (sp, key) =>
                {
                    var engine = sp.GetRequiredKeyedService(engineParameter, key);
                    var builtPages = pageClasses.Select(t => CreateWithEngine(sp, t, engine));
                    return (TPages)CreateWithEngine(sp, typeof(TImpl), engine, builtPages);
                });

                foreach (var page in pages)
                {
                    Claim(page.PropertyType, scope);
                    services.AddKeyedTransient(page.PropertyType, scope, (sp, key) => page.GetValue(sp.GetRequiredKeyedService<TPages>(key))!);
                }
            }
        });
        return this;
    }

    /// <summary>
    /// A component that does not belong to an engine scope, for example a Screenplay actor. One instance per test,
    /// asked for with <c>GetTestWareComponent&lt;T&gt;()</c> or as a <c>[TestWareMethod]</c> parameter.
    /// </summary>
    public TestWareBuilder AddComponent<TComponent>() where TComponent : class
    {
        _components.Add((services, _) =>
        {
            if (!_registered.Add((typeof(TComponent), string.Empty)))
                throw new InvalidOperationException($"{typeof(TComponent).Name} is added twice.");
            services.AddScoped<TComponent>();
        });
        return this;
    }

    /// <summary>
    /// A component built from the engine of a scope, for example a Screenplay ability. One instance per test for each scope whose
    /// engine is a <typeparamref name="TEngine"/>, asked for with <c>GetTestWareComponent&lt;T&gt;("scope")</c>.
    /// The constructor must take the engine.
    /// </summary>
    public TestWareBuilder AddComponent<TComponent, TEngine>() where TComponent : class where TEngine : class, ITestWareEngine
    {
        if (!typeof(TComponent).GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType.IsAssignableFrom(typeof(TEngine)))))
            throw new InvalidOperationException($"{typeof(TComponent).Name} needs a public constructor with a {typeof(TEngine).Name} parameter.");

        _components.Add((services, scopeEngines) =>
        {
            foreach (var (scope, engineType) in scopeEngines)
            {
                if (!typeof(TEngine).IsAssignableFrom(engineType))
                    continue;
                Claim(typeof(TComponent), scope);
                services.AddKeyedScoped<TComponent>(scope,
                    (sp, key) => ActivatorUtilities.CreateInstance<TComponent>(sp, sp.GetRequiredKeyedService<TEngine>(key)));
            }
        });
        return this;
    }

    // Engines and cockpits are built with the scope's Config, so the class must accept it.
    private static void RequireConfigConstructor(Type type)
    {
        if (type.GetConstructor([typeof(JsonObject)]) is null)
            throw new InvalidOperationException($"{type.Name} needs a public constructor taking a JsonObject (the scope's Config).");
    }

    // The one engine a page set is built from, read from the constructor parameters of its implementation and of its pages.
    private static Type EngineParameterOf(Type implementation, IEnumerable<Type> pageClasses)
    {
        var engines = pageClasses.Prepend(implementation)
            .SelectMany(t => t.GetConstructors())
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .Where(t => typeof(ITestWareEngine).IsAssignableFrom(t))
            .Distinct()
            .ToList();
        if (engines.Count != 1)
            throw new InvalidOperationException(
                $"{implementation.Name} and its pages must take exactly one engine (a type implementing ITestWareEngine) in their public constructors; they take {engines.Count}.");
        return engines[0];
    }

    // The container builds the type. The engine is handed over only to a type that takes one, because an argument no constructor accepts is an error.
    private static object CreateWithEngine(IServiceProvider sp, Type type, object engine, IEnumerable<object>? pages = null)
    {
        var takesEngine = type.GetConstructors().Any(c => c.GetParameters().Any(p => typeof(ITestWareEngine).IsAssignableFrom(p.ParameterType)));
        var arguments = (takesEngine ? new[] { engine } : []).Concat(pages ?? []).ToArray();
        return ActivatorUtilities.CreateInstance(sp, type, arguments);
    }

    // One registration per type and scope. A second one is an error, not "last one wins".
    private void Claim(Type type, string scope)
    {
        if (!_registered.Add((type, scope)))
            throw new InvalidOperationException($"{type.Name} is provided twice for scope '{scope}'. Each page set, page and component may be added only once per scope.");
    }

    internal void Build(ITestWareConfiguration config, IServiceCollection services)
    {
        services.AddScoped<TestEngines>();
        var scopeEngines = new Dictionary<string, Type>();

        foreach (var scope in config.Scopes ?? [])
        {
            if (!_engines.TryGetValue(scope.CoreName, out var register))
                throw new InvalidOperationException(
                    $"Scope '{scope.ScopeName}' needs engine '{scope.CoreName}', but none is added. Added engines: {Names(_engines.Keys)}.");
            scopeEngines[scope.ScopeName] = register(scope, services);
        }

        foreach (var scope in config.CockpitScopes ?? [])
        {
            if (!_cockpits.TryGetValue(scope.CoreName, out var register))
                throw new InvalidOperationException(
                    $"Cockpit scope '{scope.ScopeName}' needs cockpit '{scope.CoreName}', but none is added. Added cockpits: {Names(_cockpits.Keys)}.");
            register(scope, services);
        }

        foreach (var addPageSet in _pageSets)
            addPageSet(services, scopeEngines);

        foreach (var addComponent in _components)
            addComponent(services, scopeEngines);
    }

    private static string Names(IEnumerable<string> names) => names.Any() ? string.Join(", ", names) : "(none)";
}

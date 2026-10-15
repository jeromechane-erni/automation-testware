using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json.Nodes;
using TestWare.Core.Configuration;
using TestWare.Core.Interfaces;

namespace TestWare.Core;

/// <summary>
/// Registration used by <see cref="TestWareProvider.Configure(string, Action{TestWareBuilder})"/>.
/// Only the engines, cockpits and page sets added here exist. Nothing is scanned.
/// </summary>
public sealed class TestWareBuilder
{
    private readonly Dictionary<string, Func<ConfigurationScope, IServiceCollection, Type>> _engines = new();
    private readonly Dictionary<string, Action<ConfigurationScope, IServiceCollection>> _cockpits = new();
    private readonly List<Action<IServiceCollection, IReadOnlyDictionary<string, Type>>> _pageSets = new();
    private readonly HashSet<(Type Type, string Scope)> _registered = new();

    /// <summary>
    /// A scope whose CoreName is <paramref name="coreName"/> gets an engine built by <paramref name="factory"/> from the scope's Config.
    /// The engine is reachable by its class and by each of its engine interfaces (ISeleniumEngine, ITestWareEngine...), keyed by scope name.
    /// </summary>
    public TestWareBuilder AddEngine<TEngine>(string coreName, Func<JsonObject, TEngine> factory) where TEngine : class, ITestWareEngine
    {
        _engines[coreName] = (scope, services) =>
        {
            var key = scope.ScopeName;
            services.AddKeyedSingleton<TEngine>(key, (_, _) => factory(scope.Config));
            foreach (var engineInterface in typeof(TEngine).GetInterfaces().Where(i => typeof(ITestWareEngine).IsAssignableFrom(i)))
                services.AddKeyedSingleton(engineInterface, key, (sp, _) => sp.GetRequiredKeyedService<TEngine>(key));
            return typeof(TEngine);
        };
        return this;
    }

    /// <summary>
    /// Same as AddEngine, for reporters. The unkeyed <see cref="ITestWareCockpit"/> is the first cockpit scope in the configuration.
    /// </summary>
    public TestWareBuilder AddCockpit<TCockpit>(string coreName, Func<JsonObject, TCockpit> factory) where TCockpit : class, ITestWareCockpit
    {
        _cockpits[coreName] = (scope, services) =>
        {
            var key = scope.ScopeName;
            services.AddKeyedSingleton<TCockpit>(key, (_, _) => factory(scope.Config));
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
    /// Page set <typeparamref name="TPages"/> for every scope whose engine is a <typeparamref name="TEngine"/>.
    /// One line per application and engine, for example <c>AddPages&lt;ISwagLabsPages, ISeleniumEngine&gt;(e => new SeleniumSwagLabsPages(e))</c>.
    /// Each property of the page set (<c>ILoginPage Login</c>) can then be asked for directly: <c>Page&lt;ILoginPage&gt;()</c>.
    /// </summary>
    public TestWareBuilder AddPages<TPages, TEngine>(Func<TEngine, TPages> factory) where TPages : class where TEngine : class, ITestWareEngine
    {
        // The properties of the page set interface, including those of interfaces it extends. Only this one interface is read.
        var pages = typeof(TPages).GetInterfaces().Prepend(typeof(TPages)).SelectMany(t => t.GetProperties()).ToList();

        _pageSets.Add((services, scopeEngines) =>
        {
            foreach (var (scope, engineType) in scopeEngines)
            {
                if (!typeof(TEngine).IsAssignableFrom(engineType))
                    continue;
                Claim(typeof(TPages), scope);
                services.AddKeyedTransient<TPages>(scope, (sp, key) => factory(sp.GetRequiredKeyedService<TEngine>(key)));

                foreach (var page in pages)
                {
                    Claim(page.PropertyType, scope);
                    services.AddKeyedTransient(page.PropertyType, scope, (sp, key) => page.GetValue(sp.GetRequiredKeyedService<TPages>(key))!);
                }
            }
        });
        return this;
    }

    // One registration per type and scope. A second one is an error, not "last one wins".
    private void Claim(Type type, string scope)
    {
        if (!_registered.Add((type, scope)))
            throw new InvalidOperationException($"{type.Name} is provided twice for scope '{scope}'. Each page set and each page may be added only once per scope.");
    }

    internal void Build(ITestWareConfiguration config, IServiceCollection services)
    {
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
    }

    private static string Names(IEnumerable<string> names) => names.Any() ? string.Join(", ", names) : "(none)";
}

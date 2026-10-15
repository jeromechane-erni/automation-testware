using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json.Nodes;
using TestWare.Core.Configuration;
using TestWare.Core.Interfaces;

namespace TestWare.Core;

/// <summary>
/// The container of a test run. Either <see cref="Configure"/> (explicit) or <c>RegisterTestWareComponents</c> (found in the assemblies you list) fills it once at start-up. Reporters and the configuration live for the whole run.
/// Engines, pages and components live for one test: <see cref="BeginTest"/> opens the test's scope and disposing it closes them.
/// </summary>
static public class TestWareProvider
{
    static private IServiceProvider _testWarePovider;
    private static readonly IServiceCollection _testWareServices;
    private static readonly AsyncLocal<TestScope?> _current = new();
    private static string? _registeredBy;

    static TestWareProvider()
    {
        _testWareServices = new ServiceCollection();
        _testWarePovider = _testWareServices.BuildServiceProvider();
    }

    // The running test's services when a test is active, otherwise the run-wide ones.
    private static IServiceProvider Services => _current.Value?.Provider ?? _testWarePovider;

    /// <summary>
    /// Starts a test. Until the returned scope is disposed, the engines, pages and components asked for from this
    /// execution context belong to this test: the same engine is returned each time, and disposing the scope disposes them,
    /// which closes the browsers. Two tests running at the same time get separate ones.
    /// </summary>
    public static TestScope BeginTest()
    {
        var test = new TestScope(_testWarePovider.CreateScope());
        _current.Value = test;
        return test;
    }

    internal static void EndTest(TestScope test)
    {
        if (ReferenceEquals(_current.Value, test))
            _current.Value = null;
    }

    /// <summary>Retrieves an unkeyed component: the configuration, a reporter, or a component added with <c>AddComponent&lt;T&gt;()</c>.</summary>
    /// <exception cref="InvalidOperationException">Nothing of that type is registered, or it belongs to a test and no test is active.</exception>
    public static T GetTestWareComponent<T>() where T : notnull => Services.GetRequiredService<T>();

    /// <inheritdoc cref="GetTestWareComponent{T}()"/>
    public static object GetTestWareComponent(Type T) => Services.GetRequiredService(T);

    /// <summary>Retrieves a component registered for a scope (an engine, a page, an ability), keyed by the scope name.</summary>
    /// <exception cref="InvalidOperationException">Nothing of that type is registered for the scope, or no test is active.</exception>
    public static object GetTestWareComponent(Type T, string key) => Services.GetRequiredKeyedService(T, key);

    /// <inheritdoc cref="GetTestWareComponent(Type, string)"/>
    static public T GetTestWareComponent<T>(string key) where T : notnull => Services.GetRequiredKeyedService<T>(key);

    /// <summary>
    /// Retrieves a component of the specified type for the first of <paramref name="tags"/> (scope names) that has one.
    /// </summary>
    /// <param name="consumedTag">The scope the component came from, or an empty string when it is not tied to a scope.</param>
    /// <remarks>
    /// If no tag has the type, the unkeyed registration is returned, but only for types no engine scope has (a reporter,
    /// the configuration). For an engine or a page that would be the implementation of another scope, so it throws instead.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The type is registered for engine scopes, but for none of <paramref name="tags"/>.
    /// </exception>
    static public object GetTestWareComponentFromTags(Type T, string[] tags, out string consumedTag)
    {
        var keyed = _testWarePovider.GetRequiredService<IServiceProviderIsKeyedService>();
        foreach (var key in tags)
        {
            if (keyed.IsKeyedService(T, key))
            {
                consumedTag = key;
                return Services.GetRequiredKeyedService(T, key);
            }
        }

        var config = _testWarePovider.GetRequiredService<ITestWareConfiguration>();
        var scopesWithType = (config.Scopes ?? []).Select(s => s.ScopeName).Where(s => keyed.IsKeyedService(T, s)).ToList();
        if (scopesWithType.Count > 0)
        {
            var hint = _registeredBy == nameof(RegisterTestWareComponents)
                ? $"add typeof(<the class implementing {T.Name}>).Assembly to the list passed to RegisterTestWareComponents."
                : "add an AddPages line for that engine to the Configure call.";
            throw new InvalidOperationException(
                $"No {T.Name} is registered for the scopes [{string.Join(", ", tags)}]. It is registered for: {string.Join(", ", scopesWithType)}. " +
                $"If one of these scopes should have it, {hint}");
        }

        consumedTag = string.Empty;
        return Services.GetRequiredService(T);
    }

    /// <summary>
    /// Reads the configuration file and registers only what <paramref name="setup"/> adds. No assembly is scanned or loaded.
    /// </summary>
    /// <param name="config_file">The path to the configuration file.</param>
    /// <param name="setup">Adds the engines, cockpits, page sets and components, for example <c>tw => tw.AddEngine&lt;SeleniumEngine&gt;("Selenium")</c>.</param>
    static public void Configure(string config_file, Action<TestWareBuilder> setup)
    {
        EnsureOneWay(nameof(Configure));
        var config = ConfigurationManager.ReadConfigurationFile(config_file);
        _testWareServices.AddSingleton(config);

        var builder = new TestWareBuilder();
        setup(builder);
        builder.Build(config, _testWareServices);

        // ValidateScopes: asking for an engine, page or component outside a test throws, instead of quietly sharing one between tests.
        _testWarePovider = _testWareServices.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>
    /// Registers test ware components using the provided extra assemblies.
    /// </summary>
    /// <param name="extraAssemblies">A collection of additional assemblies to include in the registration process.</param>
    /// <remarks>
    /// This method registers test ware components by calling the overloaded method with a new instance of <see cref="TestWareConfiguration"/> and the provided extra assemblies.
    /// </remarks>
    static public void RegisterTestWareComponents(IEnumerable<Assembly> extraAssemblies) => RegisterTestWareComponents(new TestWareConfiguration(), extraAssemblies);
    /// <summary>
    /// Registers test ware components using a default configuration and no extra assemblies.
    /// </summary>
    /// <remarks>
    /// This method registers test ware components using a default configuration and no additional assemblies.
    /// </remarks>
    static public void RegisterTestWareComponents() => RegisterTestWareComponents(new TestWareConfiguration(), []);
    /// <summary>
    /// Registers test ware components using the specified configuration file.
    /// </summary>
    /// <param name="config_file">The path to the configuration file.</param>
    /// <remarks>
    /// This method reads the configuration from the specified file and registers the test ware components using the configuration.
    /// </remarks>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the specified configuration file cannot be found.
    /// </exception>
    /// <exception cref="ConfigurationException">
    /// Thrown when there is an error reading the configuration file.
    /// </exception>
    static public void RegisterTestWareComponents(string config_file) => RegisterTestWareComponents(ConfigurationManager.ReadConfigurationFile(config_file), []);
    /// <summary>
    /// Registers test ware components using the specified configuration file and additional assemblies.
    /// </summary>
    /// <param name="config_file">The path to the configuration file.</param>
    /// <param name="extraAssemblies">A collection of additional assemblies to include in the registration process.</param>
    /// <remarks>
    /// This method reads the configuration from the specified file and registers the test ware components using the configuration and the provided additional assemblies.
    /// </remarks>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the specified configuration file cannot be found.
    /// </exception>
    /// <exception cref="ConfigurationException">
    /// Thrown when there is an error reading the configuration file.
    /// </exception>
    static public void RegisterTestWareComponents(string config_file, IEnumerable<Assembly> extraAssemblies) => RegisterTestWareComponents(ConfigurationManager.ReadConfigurationFile(config_file), extraAssemblies);
    /// <summary>
    /// Finds the engines, cockpits and components in the loaded assemblies and registers them for the scopes of the configuration.
    /// Cockpits live for the whole run. Engines, and the components found by the scan, live for one test (see <see cref="BeginTest"/>).
    /// </summary>
    static private void RegisterTestWareComponents(ITestWareConfiguration config, IEnumerable<Assembly> extraAssemblies)
    {
        EnsureOneWay(nameof(RegisterTestWareComponents));
        var assemblies = GetDomainAndReferencedAssemblies(extraAssemblies);

        _testWareServices.AddScoped<TestEngines>();
        //Register configuration
        _testWareServices.AddSingleton(config);

        //Register Test Cockpits by reflection
        var cockpitImplementedInterfaces = GetInterfacesAndImplementations<ITestWareCockpit>(assemblies);
        RegisterImplementations(_testWareServices, cockpitImplementedInterfaces);
        var keyedCockpits = RegisterConfiguredImplementations(_testWareServices, cockpitImplementedInterfaces, config.CockpitScopes, perTest: false);
        ThrowIfScopesUnmatched("cockpit", config.CockpitScopes, keyedCockpits, cockpitImplementedInterfaces.Keys);

        //Register Test engines by reflection
        var testEngineImplementedInterfaces = GetInterfacesAndImplementations<ITestWareEngine>(assemblies);
        RegisterImplementations(_testWareServices, testEngineImplementedInterfaces);
        var keyedImplementations = RegisterConfiguredImplementations(_testWareServices, testEngineImplementedInterfaces, config.Scopes, perTest: true);
        ThrowIfScopesUnmatched("engine", config.Scopes, keyedImplementations, testEngineImplementedInterfaces.Keys);

        //Register Test components
        var componentImplementedInterfaces = GetInterfacesAndImplementations<ITestwareComponent>(assemblies);
        foreach(var componentRegistration in componentImplementedInterfaces)
        {
            _testWareServices.AddScoped(componentRegistration.Key.AsType(), componentRegistration.Key);
            foreach (var keyedImplementation in keyedImplementations)
            {
                if (!componentRegistration.Key.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType.IsAssignableFrom(keyedImplementation.Value))))
                    continue;

                _testWareServices.AddKeyedScoped(
                    componentRegistration.Key,
                    keyedImplementation.Key,
                    (provider, key) =>
                    {
                        var keyedService = provider.GetRequiredKeyedService(keyedImplementation.Value, key);
                        return ActivatorUtilities.CreateInstance(provider, componentRegistration.Key.AsType(), [keyedService]);
                    });

                foreach (var interface_ in componentRegistration.Value)
                {
                    _testWareServices.AddScoped(interface_, componentRegistration.Key);

                    _testWareServices.AddKeyedScoped(
                        interface_,
                        keyedImplementation.Key,
                        (provider, key) => {
                            return provider.GetRequiredKeyedService(componentRegistration.Key, key);
                        });
                }
            }
        }
        _testWarePovider = _testWareServices.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    // Configure and RegisterTestWareComponents are two ways to fill the same container. Using both in one run would register everything twice.
    static private void EnsureOneWay(string way)
    {
        if (_registeredBy != null && _registeredBy != way)
            throw new InvalidOperationException($"TestWare was already set up with {_registeredBy}. Use either Configure or RegisterTestWareComponents in a test run, not both.");
        _registeredBy = way;
    }

    /// <summary>
    /// Fails at start-up when a configured scope found no engine or cockpit, instead of registering nothing and
    /// failing later in a test with a generic "no service registered" error.
    /// </summary>
    /// <param name="kind">"engine" or "cockpit", used in the message.</param>
    /// <param name="scopes">The scopes from the configuration file.</param>
    /// <param name="matched">The scopes <see cref="RegisterConfiguredImplementations"/> registered an implementation for.</param>
    /// <param name="candidates">The implementations found by the scan, used to list the names that are available.</param>
    static private void ThrowIfScopesUnmatched(string kind, IEnumerable<ConfigurationScope>? scopes, IDictionary<string, Type> matched, IEnumerable<TypeInfo> candidates)
    {
        var unmatched = (scopes ?? []).Where(s => !matched.ContainsKey(s.ScopeName)).ToList();
        if (unmatched.Count == 0)
            return;

        var found = candidates.Select(c => c.GetField("Name")?.GetValue(null)?.ToString()).Where(n => n != null).Distinct().ToList();
        var details = string.Join("; ", unmatched.Select(s => $"scope '{s.ScopeName}' needs {kind} '{s.CoreName}'"));
        throw new InvalidOperationException(
            $"No {kind} was found for: {details}. {kind}s found: {(found.Count > 0 ? string.Join(", ", found) : "none")}. " +
            $"Only loaded assemblies are scanned, and an assembly nothing else references is not loaded: add typeof(<your {kind} class>).Assembly " +
            "to the list passed to RegisterTestWareComponents, or remove the scope from the configuration file.");
    }

    /// <summary>
    /// Retrieves a list of all assemblies in the current application domain, including referenced assemblies and additional assemblies provided.
    /// </summary>
    /// <param name="extraAssemblies">An enumerable collection of additional assemblies to include.</param>
    /// <returns>
    /// A list of <see cref="Assembly"/> objects representing all assemblies in the current application domain, including the provided extra assemblies and their references.
    /// </returns>
    /// <remarks>
    /// This method loads any additional assemblies provided and ensures all referenced assemblies are included in the returned list.
    /// </remarks>
    static private List<Assembly> GetDomainAndReferencedAssemblies(IEnumerable<Assembly> extraAssemblies)
    {
        var assembliesCount = -1;
        var assemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

        extraAssemblies.
            Distinct()
            .Where(e => assemblies.Any(a => a.FullName == e.FullName) == false)
            .ToList()
            .ForEach(x => assemblies.Add(AppDomain.CurrentDomain.Load(x.GetName())));

        do
        {
            assembliesCount = assemblies.Count;
            assemblies.SelectMany(x => x.GetReferencedAssemblies())
                .Distinct()
                .Where(y => assemblies.Any((a) => a.FullName == y.FullName) == false)
                .ToList()
                .ForEach(x => AppDomain.CurrentDomain.Load(x));

            assemblies = [.. AppDomain.CurrentDomain.GetAssemblies()];
        }
        while (assembliesCount != assemblies.Count);

        return assemblies;
    }
    /// <summary>
    /// Retrieves a dictionary of interfaces and their corresponding implementations from the provided assemblies.
    /// </summary>
    /// <typeparam name="T">The type of the interface to search for.</typeparam>
    /// <param name="assemblies">A collection of assemblies to search for interfaces and implementations.</param>
    /// <returns>
    /// A dictionary where the keys are <see cref="TypeInfo"/> objects representing the implementations, and the values are arrays of <see cref="TypeInfo"/> objects representing the interfaces implemented by the keys.
    /// </returns>
    /// <remarks>
    /// This method searches the provided assemblies for interfaces that implement the specified type <typeparamref name="T"/> and their corresponding class implementations.
    /// </remarks>
    private static Dictionary<TypeInfo, TypeInfo[]> GetInterfacesAndImplementations<T>(IEnumerable<Assembly> assemblies)
    {
        var implementedInterfaces = new Dictionary<TypeInfo, TypeInfo[]>();

        var definedTypes = assemblies.Where(a => !a.IsDynamic)
                            .Distinct()
                            .SelectMany(a => a.DefinedTypes);

        var interfaces = definedTypes.Where(
                                    t => t.IsInterface
                                    && t.ImplementedInterfaces.Any(i => i.FullName == typeof(T).FullName)
                                   ).ToList();

        interfaces.Insert(0, typeof(T).GetTypeInfo());

        foreach ( var interface_ in interfaces)
        {
            var implementations = definedTypes.Where(
                            t => t.IsClass
                            && !t.IsAbstract
                            && t.ImplementedInterfaces.Any(i => i.FullName == interface_.FullName)
                           ).ToList();
            implementations.ForEach(impl => implementedInterfaces[impl]  = implementedInterfaces.GetValueOrDefault(impl)?.Append(interface_)?.ToArray() ?? [interface_]);
        }
        
        return implementedInterfaces;
    }

    /// <summary>
    /// Registers an implementation with its matching interfaces in the service collection.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to which the implementation and interfaces will be registered.</param>
    /// <param name="implementation">The <see cref="TypeInfo"/> of the implementation to register.</param>
    /// <param name="interfaces">An <see cref="IEnumerable{TypeInfo}"/> of interfaces to be matched and registered with the implementation.</param>
    /// <returns>
    /// A list of <see cref="TypeInfo"/> representing the interfaces that were successfully matched and registered.
    /// </returns>
    static private List<TypeInfo> RegisterImplementationWithInterfaces(IServiceCollection serviceCollection, TypeInfo implementation, IEnumerable<TypeInfo> interfaces)
    {
        var matchInterfaces = new List<TypeInfo>();
        foreach (var interface_ in interfaces)
        {
            if (implementation.ImplementedInterfaces.Any(i => i.FullName == (interface_.AsType().FullName)))
            {
                serviceCollection.AddTransient(interface_.AsType(), provider => { return Activator.CreateInstance(implementation.AsType())!; });
                matchInterfaces.Add(interface_);
            }
        }
        return matchInterfaces;
    }

    /// <summary>
    /// Registers implementations and their corresponding interfaces in the service collection.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to which the services will be added.</param>
    /// <param name="implementedInterfaces">A dictionary where the keys are the implementations and the values are arrays of the interfaces they implement.</param>
    /// <remarks>
    /// This method registers each interface to be resolved to its corresponding implementation using transient lifetime.
    /// </remarks>
    static private void RegisterImplementations(IServiceCollection serviceCollection, IDictionary<TypeInfo, TypeInfo[]> implementedInterfaces)
    {

        foreach (var registration in implementedInterfaces)
        {
            foreach (var interface_ in registration.Value)
            {
                serviceCollection.AddTransient(interface_.AsType(), provider => { return ActivatorUtilities.CreateInstance(provider, registration.Key.AsType()); });
            }
        }
    }

    /// <summary>
    /// Registers an implementation and its interfaces for one configured scope. Cockpits are shared by the whole run. Engines are
    /// created for one test and remembered by the test's scope, which closes them when the test ends (see <see cref="TestScope.Close"/>).
    /// </summary>
    static private void RegisterInterfacesConfiguredAndKeyed(IServiceCollection serviceCollection, TypeInfo implementation, TypeInfo[] interfaces, string key, JsonObject config, bool perTest)
    {
        if (!perTest)
        {
            serviceCollection.AddSingleton(implementation.AsType());

            serviceCollection.AddKeyedSingleton(
                implementation.AsType(),
                key,
                (provider, key) => { return ActivatorUtilities.CreateInstance(provider, implementation.AsType(), config); }
            );
            foreach (var interface_ in interfaces)
            {
                serviceCollection.AddSingleton(
                    interface_.AsType(),
                    provider => { return provider.GetRequiredKeyedService(implementation.AsType(), key); }
                );

                serviceCollection.AddKeyedSingleton(
                    interface_.AsType(),
                    key,
                    (provider, key) => { return provider.GetRequiredKeyedService(implementation.AsType(), key); }
                );
            }
            return;
        }

        serviceCollection.AddScoped(implementation.AsType());

        serviceCollection.AddKeyedScoped(
            implementation.AsType(),
            key,
            (provider, key) =>
            {
                var engine = ActivatorUtilities.CreateInstance(provider, implementation.AsType(), config);
                provider.GetRequiredService<TestEngines>().Created.Add((ITestWareEngine)engine);
                return engine;
            }
        );
        foreach (var interface_ in interfaces)
        {
            serviceCollection.AddScoped(
                interface_.AsType(),
                provider => { return provider.GetRequiredKeyedService(implementation.AsType(), key); }
            );

            serviceCollection.AddKeyedScoped(
                interface_.AsType(),
                key,
                (provider, key) => { return provider.GetRequiredKeyedService(implementation.AsType(), key); }
            );
        }
    }

    /// <summary>
    /// Registers the implementations whose public constant <c>Name</c> equals the <c>CoreName</c> of a scope, once for each such scope.
    /// </summary>
    /// <returns>The scopes that got an implementation, and which one.</returns>
    static private Dictionary<string, Type> RegisterConfiguredImplementations(IServiceCollection serviceCollection, IDictionary<TypeInfo, TypeInfo[]> targets, IEnumerable<ConfigurationScope> scopes, bool perTest)
    {
        var keyedImplementations = new Dictionary<string, Type>();
        if (scopes?.Count() > 0)
        {
            foreach (var target in targets)
            {
                var coreName = target.Key.GetField("Name")?.GetValue(null)?.ToString();
                foreach (var scope in scopes.Where(scope => scope.CoreName == coreName))
                {
                    RegisterInterfacesConfiguredAndKeyed(serviceCollection, target.Key, target.Value, scope.ScopeName, scope.Config, perTest);
                    keyedImplementations[scope.ScopeName] = target.Key;
                };
            }
        }
        return keyedImplementations;
    }
}

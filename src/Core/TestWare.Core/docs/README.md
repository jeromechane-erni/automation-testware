# How to use TestWare.Core

TestWare.Core reads the config file, connects a test to the engine of its scope, and hands it page objects and reporters. It registers what a project needs in one of two ways, explicit (`Configure`) or automatic (`RegisterTestWareComponents`, the original). This guide covers the config file, both ways, pages and the attributes a test uses.

## Needs

- Packages: `Microsoft.Extensions.DependencyInjection` 8.0.0, `Microsoft.Extensions.DependencyInjection.Abstractions` 8.0.1 (net8.0).
- Projects: an engine (for example `TestWare.Engines.Selenium`) and, per application, one SUT interfaces project plus one SUT project per engine.
- Config: `TestConfig.json` with `Scopes` (each has `ScopeName`, `CoreName`, `Config`), `CockpitScopes` (same shape, for reporters) and `EvidenceBasePath` (where evidence files go).

## Setup

**Explicit way. 1. Page interface and page set** in the SUT interfaces project. The page set lists every page of the application. `ILoginPage.cs` and `ISwagLabsPages.cs`:

```csharp
public interface ILoginPage : ITestwareComponent { public void SetUserName(string value); /* ... */ }

public interface ISwagLabsPages
{
    ILoginPage Login { get; }
}
```

**2. One implementation of the page set per engine**, in that engine's SUT project. It lists the page classes as constructor parameters and exposes them through the interface. The container builds each page from the test's engine, so there is no `new LoginPage(engine)`. `SeleniumSwagLabsPages.cs`:

```csharp
public class SeleniumSwagLabsPages(LoginPage login) : ISwagLabsPages
{
    public ILoginPage Login => login;
}
```

All pages of a set are built together: keep page constructors to storing the engine and do not touch the browser in them.

**3. A scope in the config** (shortened from the NUnit sample's `TestConfig.json`):

```json
{ "Scopes": [ { "ScopeName": "swagLabs", "CoreName": "Selenium",
    "Config": { "Driver": "Chrome", "BaseUrl": "https://www.saucedemo.com/" } } ] }
```

**4. Register once per test run**, in the start-up hook. `TestWareRun.cs` (NUnit):

```csharp
TestWareProvider.Configure(configFile, tw => tw
    .AddEngine<SeleniumEngine>("Selenium")
    .AddPages<ISwagLabsPages, SeleniumSwagLabsPages>());
```

`"Selenium"` must equal the scope's `CoreName`. The hook names types and the container builds them: an engine needs a constructor taking the scope's config (`JsonObject`), and the page-set class with its pages needs exactly one engine type in their constructors (`LoginPage(ISeleniumEngine engine)`). `AddPages` applies to every scope whose engine fits it. For a Playwright scope add `AddEngine<PlaywrightEngine>("Playwright")` and `AddPages<ISwagLabsPages, PlaywrightSwagLabsPages>()`.

**Automatic way.** The MSTest samples call `RegisterTestWareComponents` instead. Nothing is listed per engine or page: they are found in the assemblies you list. An engine or cockpit matches a scope by its `Name` constant. A page is any class that implements a page interface (one that extends `ITestwareComponent`) and takes the engine in its constructor, so steps 1 and 2 reduce to the interface and the class. `Hooks.cs` of the MSTest sample:

```csharp
IEnumerable<Assembly> extraAssemblies = [
    typeof(ExtentCockpit).Assembly, typeof(ILoginPage).Assembly,
    typeof(Suts.SwagLabs.Selenium.POM.LoginPage).Assembly, typeof(Suts.SwagLabs.Playwright.POM.LoginPage).Assembly ];
TestWareProvider.RegisterTestWareComponents("TestConfig.json", extraAssemblies);
```

List the assemblies that nothing else references: the page implementations and the cockpits. Use one way per test run; using both throws. Each MSTest sample keeps its `Configure` form as a commented block above the scan call; swap the two to try the explicit way on the same tests.

## Use

The scope decides which engine and which page implementation a test gets. The test code does not name either. Two attributes on the class or the method drive it: `[TestWareScopes("a", "b")]` lists the scope names (class and method lists are joined), and `[TestWareDoc("Tags", ["Selenium"])]` gives a reporter its metadata (`Name`, `Description`, `Tags`, `Devices`, `Authors`).

NUnit. `POMSeleniumSwagLabs.cs` in the NUnit sample. `TestWareTestBase.Page<T>()` returns a new page for the test's scope:

```csharp
[TestWareScopes("swagLabs")]
public class POMSeleniumSwagLabs : TestWareTestBase
{
    public void ValidLogin(string user, string password)
    {
        var loginPage = Page<ILoginPage>();
```

MSTest. `POMSeleniumSwagLabs.cs` in the MSTest sample. With `[TestWareMethod]`, `DataRow` values fill the parameters whose type matches them in order, and every other parameter is injected from the test's scopes:

```csharp
[TestWareMethod]
[DataRow("standard_user", "secret_sauce")]
public void ValidLogin(string user, string password, ILoginPage loginPage)
```

Engines, pages and components live for one test. The MSTest wheel opens the test's scope before injection and closes it after `[TestCleanup]`; the NUnit base class does the same in `[SetUp]` and `[TearDown]`. Inside the test every request returns the same engine, and closing the scope closes the browser, so a suite never calls `Dispose`. If one engine fails to close, the others are still closed and the error is reported with the test result. The NUnit base class is marked `[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]`, which tests running in parallel need.

## Extend

- **New page:** add a property to the page set. The build fails (CS0535) in every engine project until its implementation creates the page.
- **Pages only some engines have:** split the page set. Put shared pages in one interface and extra pages in an interface that extends it. Register each engine with the set it implements. `AddPages` also registers the pages of the interfaces the set extends.
- **New application:** a new page set, one implementation per engine, one `AddPages` line per engine. With the automatic way a new page class needs no line; its assembly must be in the list.
- **Objects that are not pages** (an actor, an ability): `AddComponent`. Each is one instance per test, like the engines. `TestSuiteBase.cs` in the Screenplay sample:

```csharp
.AddComponent<StandardUser>()
.AddComponent<BrowseTheWeb, PlaywrightEngine>()
```

## Mistakes and what you see

| Mistake | What happens |
|---|---|
| No `AddPages` for the application, or its engine type does not match the scope's engine | When the test asks for the page: `InvalidOperationException: No service for type '...ILoginPage' has been registered.` |
| `[TestWareMethod]` asks for a page that other scopes have but the test's scopes do not | `InvalidOperationException: No ILoginPage is registered for the scopes [nope]. It is registered for: swagLabs. If one of these scopes should have it, add an AddPages line for that engine to the Configure call.` |
| `CoreName` has no `AddEngine` | At start-up: `Scope 'swagLabs' needs engine 'Selenium', but none is added. Added engines: Selenium2.` |
| Automatic way: the assembly with an engine, cockpit or page is not in the list and nothing else references it | An engine or cockpit scope fails at start-up: `No engine was found for: scope 'swagLabs' needs engine 'Selenium'. engines found: none. Only loaded assemblies are scanned, and an assembly nothing else references is not loaded: add typeof(<your engine class>).Assembly to the list passed to RegisterTestWareComponents, or remove the scope from the configuration file.` A missing page assembly is only noticed when the test asks: `No service for type '...ILoginPage' has been registered.` |
| Both ways in one run | `InvalidOperationException: TestWare was already set up with Configure. Use either Configure or RegisterTestWareComponents in a test run, not both.` |
| Same page set added twice for one scope | At start-up: `ISwagLabsPages is provided twice for scope 'swagLabs'. Each page set, page and component may be added only once per scope.` |
| An engine without a `JsonObject` constructor, or a page set whose pages name no engine type or two | At start-up: `EngineWithoutConfig needs a public constructor taking a JsonObject (the scope's Config).` and `TwoEnginePages and its pages must take exactly one engine (a type implementing ITestWareEngine) in their public constructors; they take 2.` |
| Asking for an engine, page or component outside a test | `InvalidOperationException: Cannot resolve scoped service 'TestWare.Core.Interfaces.ITestWareEngine' from root provider.` |
| The config file path is wrong | `Configure` throws `FileNotFoundException: Could not find file '...nope.json'.` |
| NUnit test class without `[TestWareScopes]` | In setup: `InvalidOperationException: POMSeleniumSwagLabs.ValidLogin needs a [TestWareScopes("...")] attribute on the class or the method.` (`TestWareTestBase.cs`) |

## Related

- Map: sections 3.1 to 3.5, 6.1, 6.2.
- Changes: 0001 to 0004 in [docs/CHANGES.md](../../../../docs/CHANGES.md).

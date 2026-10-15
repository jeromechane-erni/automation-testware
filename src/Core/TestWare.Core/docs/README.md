# How to use TestWare.Core

TestWare.Core connects a test to the engine of its scope and hands it the page objects for that engine. This guide covers pages: declaring them, getting one in a test, and what fails when a step is missed. Other parts of Core (cockpits, attributes other than scopes) are not covered yet.

## Needs

- Packages: `Microsoft.Extensions.DependencyInjection` 8.0.0, `Microsoft.Extensions.DependencyInjection.Abstractions` 8.0.1 (net8.0).
- Projects: an engine (for example `TestWare.Engines.Selenium`) and, per application, one SUT interfaces project plus one SUT project per engine.
- Config: `Scopes` in `TestConfig.json`. Each scope has `ScopeName`, `CoreName` and `Config`.
- Other: none.

## Setup

**1. Page interface and page set** in the SUT interfaces project. The page set lists every page of the application. `ILoginPage.cs` and `ISwagLabsPages.cs`:

```csharp
public interface ILoginPage : ITestwareComponent { public void SetUserName(string value); /* ... */ }

public interface ISwagLabsPages
{
    ILoginPage Login { get; }
}
```

**2. One implementation of the page set per engine**, in that engine's SUT project. `SeleniumSwagLabsPages.cs`:

```csharp
public class SeleniumSwagLabsPages(ISeleniumEngine engine) : ISwagLabsPages
{
    public ILoginPage Login => new LoginPage(engine);
}
```

**3. A scope in the config** (shortened from the NUnit sample's `TestConfig.json`):

```json
{ "Scopes": [ { "ScopeName": "swagLabs", "CoreName": "Selenium",
    "Config": { "Driver": "Chrome", "BaseUrl": "https://www.saucedemo.com/" } } ] }
```

**4. Register once per test run**, in the start-up hook. `TestWareRun.cs` (NUnit):

```csharp
TestWareProvider.Configure(configFile, tw => tw
    .AddEngine("Selenium", cfg => new SeleniumEngine(cfg))
    .AddPages<ISwagLabsPages, ISeleniumEngine>(engine => new SeleniumSwagLabsPages(engine)));
```

`"Selenium"` must equal the scope's `CoreName`. `AddPages` applies to every scope whose engine is an `ISeleniumEngine`. For a Playwright scope add `AddEngine("Playwright", ...)` and `AddPages<ISwagLabsPages, PlaywrightEngine>(...)`.

**Older way.** The MSTest samples call `RegisterTestWareComponents(configFile, assemblies)` instead. It finds page classes by scanning the listed assemblies (`Hooks.cs`), so the assembly with each page implementation must be in the list:

```csharp
TestWareProvider.RegisterTestWareComponents("TestConfig.json", extraAssemblies);
```

## Use

The scope decides which engine and which page implementation a test gets. The test code does not name either.

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

The page reads the engine's driver on every call (`PageBase.cs`: `Driver => Engine.Driver`), so a page can be created before the browser starts.

## Extend

- **New page:** add a property to the page set. The build fails (CS0535) in every engine project until its implementation creates the page.
- **Pages only some engines have:** split the page set. Put shared pages in one interface and extra pages in an interface that extends it. Register each engine with the set it implements. `AddPages` also registers the pages of the interfaces the set extends.
- **New application:** a new page set, one implementation per engine, one `AddPages` line per engine.

## Mistakes and what you see

| Mistake | What happens |
|---|---|
| No `AddPages` for the application, or its engine type does not match the scope's engine | When the test asks for the page: `InvalidOperationException: No service for type '...ILoginPage' has been registered.` |
| Scan only: assembly with the page implementation missing from the list | The same message when the test asks. Nothing fails at start-up. |
| `[TestWareMethod]` asks for a page that other scopes have but the test's scopes do not | `InvalidOperationException: No ILoginPage is registered for the scopes [nope]. It is registered for: swagLabs. ...` |
| `CoreName` has no `AddEngine` | At start-up: `Scope 'swagLabs' needs engine 'Selenium', but none is added. Added engines: Selenium2.` |
| Same page set added twice for one scope | At start-up: `ISwagLabsPages is provided twice for scope 'swagLabs'. Each page set and each page may be added only once per scope.` |
| NUnit test class without `[TestWareScopes]` | In setup: `InvalidOperationException: POMSeleniumSwagLabs.ValidLogin needs a [TestWareScopes("...")] attribute on the class or the method.` (`TestWareTestBase.cs`) |

## Related

- Map: sections 3.1, 3.2, 3.3, 3.5.
- Changes: 0001, 0002 in [docs/CHANGES.md](../../../../docs/CHANGES.md).

# TestWare framework map

Last verified against the code on 2026-10-15 (commit `3fd5c98`, no uncommitted code changes). Changes made after that are listed in [CHANGES.md](CHANGES.md).

## 1. What TestWare is, in one paragraph

TestWare is a thin layer between a .NET test runner (MSTest, NUnit) and the tools that drive an application under test (Selenium, Playwright) or report results (ExtentReports, ReportPortal).
A JSON file declares named **scopes**. Each scope binds an **engine** kind to its settings. Tests say which scope they run in through an attribute, and ask TestWare for the engine or the page objects of that scope.
TestWare builds everything with the standard Microsoft dependency injection container and hands it out through one static entry point.

## 2. Solution layout

```
src/
  Core/TestWare.Core                       the container, config model, attributes, interfaces
  Engines/TestWare.Engines.Selenium        ITestWareEngine for Selenium WebDriver
  Engines/TestWare.Engines.Playwright      ITestWareEngine for Playwright
  cockpits/TestWare.Cockpits.ExtentReports ITestWareCockpit writing an HTML report
  cockpits/TestWare.Cockpits.ReportPortal  ITestWareCockpit pushing to a ReportPortal server
  Wheels/TestWare.Wheels.MsTestWheel       MSTest adapter: parameter injection + result mapping
  Assistance/TestWare.Assistance.FluentSteps  step chain with before/after/error hooks
samples/
  suts/swaglabs/*                          page object interfaces + Selenium and Playwright implementations
  tests/*                                  one test project per usage style (MSTest, NUnit, Screenplay, engine extension, interoperability)
```

Vocabulary used by the original author:

| Word | Meaning |
|---|---|
| Engine | Something that drives an application: a browser session, later an API client or a mobile driver. Implements `ITestWareEngine`. |
| Cockpit | A reporter. Implements `ITestWareCockpit`. |
| Wheel | An adapter for a specific test runner. Today only MSTest has one. |
| Component | Any page object or helper that a test asks TestWare for. |
| Scope | A named configuration entry that creates one engine instance with one set of settings. |
| Evidence | A file produced during a test, usually a screenshot, attached to the report. |

## 3. Core mechanism

### 3.1 Configuration file

Each test project ships a `TestConfig.json` copied to the output folder. Its shape is `TestWareConfiguration` in `src/Core/TestWare.Core/Configuration/`:

```json
{
  "Scopes":        [ { "ScopeName": "swagLabs", "CoreName": "Selenium",      "Config": { ...engine settings... } } ],
  "CockpitScopes": [ { "ScopeName": "Local",    "CoreName": "ExtentReports", "Config": { ...reporter settings... } } ],
  "EvidenceBasePath": "C:\\Temp\\Results"
}
```

- `ScopeName` is the key a test uses to pick this entry.
- `CoreName` names the engine or cockpit kind. With `RegisterTestWareComponents` it must equal the public constant `Name` of the engine or cockpit class (for example `SeleniumEngine.Name = "Selenium"`). With `Configure` it must equal the name given to `AddEngine` / `AddCockpit`.
- `Config` is an untyped `JsonObject`. Each engine or cockpit deserialises it into its own settings class (`SeleniumConfig`, `PlaywrightConfig`, or reads keys directly).

Several scopes may use the same `CoreName` with different settings. That is how one test project runs the same engine against different sites or browsers.

### 3.2 The container: `TestWareProvider`

`src/Core/TestWare.Core/TestWareProvider.cs` is a static class holding one `IServiceCollection` and one `IServiceProvider` for the whole test process.

Registration happens once, at process start, from the runner's assembly-level hook, in one of two ways. `Configure(configFile, tw => tw.AddEngine(...).AddPages(...))` registers only what it is told; see section 3.5. The original way, still used by the MSTest samples, is `RegisterTestWareComponents(configFile, extraAssemblies)`. It works by reflection:

1. It takes every assembly already loaded in the process, loads the `extraAssemblies`, and keeps loading everything those reference until no new assembly appears.
2. It scans all of them for classes implementing `ITestWareCockpit`, `ITestWareEngine` and `ITestwareComponent` (directly or through a sub-interface).
3. For every scope in the config it looks for the engine (or cockpit) class whose public constant `Name` equals the scope's `CoreName`. A `CoreName` that matches nothing throws at startup.

What it registers, per matching scope:

- the engine class as a **keyed singleton** under the scope name, created from the scope's `Config` (the class needs a constructor taking a `JsonObject`);
- every engine interface (`ITestWareEngine`, `ISeleniumEngine`, ...) keyed under the same scope name, pointing at that instance;
- the same interfaces **unkeyed**, so `GetTestWareComponent<ITestWareEngine>()` without a key also works. When several scopes exist, the unkeyed one is whichever registration ran last.

Cockpits follow the same pattern with `CockpitScopes`.

Pages (components) are registered **per scope** and **only for the scopes whose engine type they accept**. A Selenium `LoginPage` is reachable under `swagLabs` but not under `PlaySwagLabs`. This is inferred from the page class's constructors: a page is keyed under a scope if any constructor parameter accepts that scope's engine type. Its interfaces are registered keyed (and unkeyed) the same way.

Resolution is done with:

```csharp
TestWareProvider.GetTestWareComponent<T>()           // unkeyed
TestWareProvider.GetTestWareComponent<T>("scope")    // keyed by scope name
TestWareProvider.GetTestWareComponentFromTags(type, scopes, out consumed)  // tries each scope; falls back to unkeyed only for types no engine scope has (cockpit, config), otherwise throws
```

`CreateScope(scopes)` only stores the current scope names in a `TestWareScopes` singleton. The DI scope it creates is discarded. Nothing reads `CurrentScopes` today.

### 3.3 Attributes on tests

`src/Core/TestWare.Core/Attributes/`:

- `[TestWareScopes("a", "b")]` on a class or a method. Lists the scope names the test uses, in order. Class and method attributes are concatenated.
- `[TestWareDoc("Key", "value", ...)]` on a class or a method, repeatable. Free-form metadata forwarded to the cockpit. The Extent cockpit understands the keys `Name`, `Description`, `Tags`, `Devices`, `Authors`.
- `TestWareAttributes` has helpers that read both, either from `Type`/`MethodInfo` or from class and method names (then it searches all loaded assemblies for the type).

### 3.4 Interfaces every plug-in implements

`ITestWareEngine`: `Initialize`, `Dispose`, `CollectEvidence(path, name)` returning the file written, `StartRecordingEvidences`, `StopRecordingEvidences(path, name)`.

`ITestWareCockpit`: `Initialize`, `Dispose`, and Start/Stop for Suite, Case and Step, each returning a `Guid` handle, plus `Add...Activity(params string[])`. An activity that is an existing file path becomes an attachment, anything else becomes a log line. Results use the `TestWareResult` enum (Pass, Fail, Skip, Unknown).

`ITestwareComponent` is an empty marker interface. The scan finds page objects through it: a page interface extends it, and the classes implementing that interface are registered as components. `Configure` does not use it.

### 3.5 Declaring pages with `Configure` and page sets

Used by the NUnit sample. Code: `src/Core/TestWare.Core/TestWareBuilder.cs`. A page is declared with three pieces, and nothing is scanned.

**1. The page interface** (SUT interfaces project). What a test can do on the page. Engine-neutral.

```csharp
public interface ILoginPage { void SetUserName(string value); ... }
```

**2. The page set** (SUT interfaces project). One interface per application that lists every page as a property. It is the checklist each engine must fill.

```csharp
public interface ISwagLabsPages
{
    ILoginPage Login { get; }
}
```

**3. One implementation of the page set per engine** (SUT engine project). Each property creates the engine's page with plain `new`.

```csharp
public class SeleniumSwagLabsPages(ISeleniumEngine engine) : ISwagLabsPages
{
    public ILoginPage Login => new LoginPage(engine);
}
```

**Registration** (the test project's start-up hook, for example `TestWareRun.cs`). One `AddEngine` per `CoreName` used in the config, one `AddPages` per application and engine:

```csharp
TestWareProvider.Configure(configFile, tw => tw
    .AddEngine("Selenium", cfg => new SeleniumEngine(cfg))
    .AddPages<ISwagLabsPages, ISeleniumEngine>(engine => new SeleniumSwagLabsPages(engine)));
```

What `Configure` does with this:

- Each config scope is matched to the `AddEngine` whose name equals its `CoreName`. The engine is registered keyed by scope name, by its class and by each of its engine interfaces (`ISeleniumEngine`, `ITestWareEngine`).
- `AddPages<TPages, TEngine>` applies to every scope whose engine is a `TEngine`. For each such scope it registers the page set, and each property type of the page set (`ILoginPage`), keyed by scope name. It reads the properties of that one interface and of the interfaces it extends; nothing else is inspected.

**Use in a test.** The test asks for the page, not the page set: `Page<ILoginPage>()` in the NUnit base class, or an `ILoginPage loginPage` parameter with `[TestWareMethod]`. Each call creates a new page object for the test's scope.

**Pages only some engines have.** Split the page set: the shared pages in one interface, the extra pages in an interface that extends it. Each engine implements the set it supports and is registered with that set.

```csharp
public interface IKaimaiPages        { ILoginPage Login { get; } }
public interface IKaimaiAndroidPages : IKaimaiPages { INotificationSettingsPage NotificationSettings { get; } }

.AddPages<IKaimaiAndroidPages, AndroidEngine>(e => new AndroidPages(e))   // Login + NotificationSettings
.AddPages<IKaimaiPages, IosEngine>(e => new IosPages(e))                  // Login
```

Applications that share no pages get separate page sets.

**When a mistake is caught:**

| Mistake | Caught |
|---|---|
| An engine's page set implementation misses a page | Build (CS0535) |
| A config `CoreName` has no `AddEngine` / `AddCockpit` | Start-up |
| The same page set or page type added twice for one scope | Start-up |
| The `AddPages` line for an application is missing | When a test asks for the page. With `[TestWareMethod]` the message names the scopes that have it; `GetTestWareComponent<T>("scope")` (NUnit `Page<T>()`) gives the generic "No service registered" message. |
| A test asks for a page its scope's engine does not have | When the test asks for it, same messages as above |

Do not fill a page an engine lacks with `throw new NotImplementedException()`: it compiles, so the gap only shows when a test runs. Split the page set instead.

## 4. Engines

### 4.1 Selenium (`TestWare.Engines.Selenium`, namespace `TestWare.Engines.SeleniumEngine`)

- `SeleniumEngine : ISeleniumEngine`. `Name = "Selenium"`. Exposes `Driver`. The driver is created lazily on first access or by `Initialize()`, which also navigates to `BaseUrl` when set.
- `SeleniumConfig`: `Driver` (Chrome, Firefox, Edge, InternetExplorer), `BaseUrl`, `ImplicitWaitSeconds`, `Service` (driver folder), `Options` (a JSON object applied by reflection onto the browser's options class; the key `Arguments` is special-cased to `AddArguments`).
- `DriverFactory` creates a local driver service. Remote (Grid) is a stub that throws.
- Extensions in `Extensions/` add `SafeClick`, `SafeSendKeys`, `ElementIsPresent`, and similar waits used by the sample pages.
- Evidence is a PNG screenshot. Recording start/stop are empty.

Extending: subclass `SeleniumEngine`, give it its own public constant `Name`, override what you need, and use that name as `CoreName` in the config. The scan finds it if its assembly is among those passed to `RegisterTestWareComponents`. `samples/tests/TestWare.Samples.Test.MsTest.ExtemdingEngines` does this to capture network traffic through DevTools.

### 4.2 Playwright (`TestWare.Engines.Playwright`, namespace `TestWare.Engines.PlaywrightEngine`)

- `PlaywrightEngine : ITestWareEngine`. `Name = "Playwright"`. Public fields `Browser` and `Page`. There is no `IPlaywrightEngine` interface, so pages depend on the class.
- `PlaywrightConfig`: `Browser` (Chromium, Firefox, Webkit), `BaseUrl`, `LaunchOptions` and `PageOptions` as raw JSON passed to Playwright's option classes.
- `Initialize` is a blocking wrapper over the async setup.

## 5. Cockpits (reporters)

Both keep a `Dictionary<Guid, handle>` so a test can address a suite, case or step by the `Guid` it got back, or omit it and address the current one.

- `ExtentCockpit` (`Name = "ExtentReports"`): config keys `ReportPath`, `ReportName`. `Initialize` creates the report, `Dispose` flushes it to disk. Suites are not report nodes; their metadata is merged into every test case.
- `ReportPortalCockpit` (`Name = "ReportPortal"`): config keys `Url`, `ProjectName`, `ApiKey`. `Initialize` starts a launch, `Dispose` finishes it. Suites, cases and steps are real items.

## 6. Wheels (runner adapters)

### 6.1 MSTest (`TestWare.Wheels.MsTestWheel`)

- `[TestWareMethod]` replaces `[DataTestMethod]`. If the method has more parameters than the `[DataRow]` supplies, the extra ones are resolved from TestWare using the test's scopes, in order, consuming one scope per injected parameter. That is how `ValidLogin(string user, string password, ILoginPage loginPage)` receives a page.
- `ResultMapper.Translate` maps `UnitTestOutcome` to `TestWareResult`.

### 6.2 NUnit

No wheel project. `samples/tests/TestWare.Samples.Tests.NUnitIntegration/TestWareTestBase.cs` shows the equivalent done by hand (its `TestWareRun.cs` registers with `Configure`): `[SetUp]` reads the scope attribute, starts the engine, `Page<T>()` resolves a page for the test's scope, `[TearDown]` takes a screenshot on failure and disposes.

## 7. Assistance: fluent steps

`StepChain` is an abstract class with `Step(name, action)` overloads. Each call runs `BeforeStep`, the action, then `AfterStep`, or `ErrorStep` on exception, and increments a step counter.
`AAAChain` (Arrange, Act, Assert) and `GherkinChain` (Given, When, Then) are the same thing with renamed methods.

The MSTest sample's `StepWithEvidenceChain` implements the hooks: before a step it starts a cockpit step, after it collects a screenshot from each engine and attaches it, on error it does the same and marks the step failed.

## 8. Lifecycle of one MSTest run

Taken from `samples/tests/TestWare.Samples.Tests.MsTest/`:

```
[AssemblyInitialize]   Hooks.AssemblyInitialize
                         TestWareProvider.RegisterTestWareComponents("TestConfig.json", assemblies)   scan and build the container
                         cockpit.Initialize()                                  open the report

[ClassInitialize]      TestSuiteBase.ClassInitialize
                         cockpit.StartTestSuite(className, docAttributes)

[TestInitialize]       TestSuiteBase.Setup
                         cockpit.StartTestCase(testName, docAttributes)
                         engine = GetTestWareComponent<ITestWareEngine>(firstScope)
                         engine.Initialize()                                   open the browser
                         engine.StartRecordingEvidences()
                         Steps = new StepWithEvidenceChain(...)

[TestWareMethod]       injects pages for the test's scopes, invokes the test body

[TestCleanup]          TestSuiteBase.TearDown
                         engine.StopRecordingEvidences(...) -> attached
                         engine.Dispose()                                      close the browser
                         cockpit.StopTestCase(result)

[ClassCleanup]         cockpit.StopTestSuite()
[AssemblyCleanup]      cockpit.Dispose()                                       flush the report
```

Engines are singletons per scope, so `Initialize` and `Dispose` are called once per test on the same object. The Selenium engine recreates its driver on the next `Initialize`.

## 9. How to add things

| Goal | Steps |
|---|---|
| New engine | Implement `ITestWareEngine` (and your own interface if pages should depend on an abstraction), with a constructor taking a `JsonObject`. With `Configure`: add `.AddEngine("YourName", cfg => new YourEngine(cfg))` and use `"CoreName": "YourName"` in the config. With `RegisterTestWareComponents`: add `public const string Name = "YourName"` and make sure the engine's assembly is in the list passed to it. |
| New cockpit | Same with `ITestWareCockpit` and `AddCockpit`. |
| New page object (`Configure`) | Add the page interface to the SUT interfaces project and a property for it to the application's page set. The build then fails until every engine's page set implementation creates it (section 3.5). |
| New application (`Configure`) | Add a page set interface, one implementation per engine, and one `.AddPages<TPages, TEngine>(...)` line per engine in the test project's start-up hook. |
| New page object (`RegisterTestWareComponents`) | Define the interface in a SUT project and make it extend `ITestwareComponent`. Implement it per engine with a public constructor taking the engine interface or class. Add the SUT assemblies to the list passed to `RegisterTestWareComponents`. |
| New test project | Copy the `Hooks.cs` + `TestSuiteBase.cs` pair (MSTest) or `TestWareRun.cs` + `TestWareTestBase.cs` (NUnit), add a `TestConfig.json` copied to output. |
| Use a second scope in one test | List both in `[TestWareScopes("a", "b")]`, declare two page parameters; they are filled in order. |

## 10. Known rough edges

Facts worth knowing before touching the code. None of these is a change request by itself.

- The root `README.md` describes `AutomationLifeCycleBase`, `IEngineManager`, `ContainerManager`. None exist in the code. This map is the current truth.
- Folder names and namespaces differ: folder `TestWare.Engines.Selenium`, namespace `TestWare.Engines.SeleniumEngine`. Same for Playwright.
- The marker interface is spelled `ITestwareComponent` (lower-case w).
- `TestWareScopes` the attribute and `TestWareScopes` the internal holder class share a name in different namespaces.
- With `RegisterTestWareComponents`, `CoreName` is matched against a `Name` constant found by reflection. A typo throws at startup.
- `RegisterTestWareComponents` force-loads every assembly referenced by anything in the process. Under NUnit this pulls in the test adapter's references, which needed the `TestCentric.Metadata` package. The NUnit sample now uses `Configure` and no longer carries it.
- With `RegisterTestWareComponents`, pages are matched to engines by looking at constructor parameters, so a page can silently end up under a scope you did not intend, or under none.
- `ReportPortalCockpit.Dispose` is `async void`, so the launch may not be finished when the process exits.
- The sample `TestConfig.json` files contain a ReportPortal API key for the public demo server. Treat it as a placeholder.
- Targets `net8.0` despite the "net6" in the original repository name.

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

`src/Core/TestWare.Core/TestWareProvider.cs` is a static class holding one `IServiceCollection` and one root `IServiceProvider` for the whole test process. The configuration and the reporters live in the root for the whole run. Engines, pages and components live for one test (see "A test's scope" below).

Registration happens once, at process start, from the runner's assembly-level hook, in one of two ways. Use one of them per test run; using both throws `InvalidOperationException`.

| Way | Call | How components are found | Used by |
|---|---|---|---|
| Automatic (the original) | `RegisterTestWareComponents(configFile, extraAssemblies)` | Scans the assemblies that are loaded | the MSTest samples |
| Explicit | `Configure(configFile, tw => tw.AddEngine<T>(...).AddPages<..>())` (section 3.5) | Only what the caller names; nothing is scanned | the NUnit sample; each MSTest sample carries its `Configure` form as a commented block next to the scan call |

**The automatic way** works by reflection:

1. It takes every assembly already loaded in the process, loads the `extraAssemblies`, and keeps loading everything those reference until no new assembly appears.
2. It scans all of them for classes implementing `ITestWareCockpit`, `ITestWareEngine` and `ITestwareComponent` (directly or through a sub-interface).
3. For every scope in the config it looks for the engine (or cockpit) class whose public constant `Name` equals the scope's `CoreName`. A `CoreName` that matches nothing throws at startup and names the engines found.
4. Pages and other components need no registration line. A class that implements an interface extending `ITestwareComponent` is registered for every scope whose engine type one of its constructor parameters accepts, and the container builds it with that engine.

With the explicit way a scope whose `CoreName` has no matching `AddEngine` / `AddCockpit` throws at startup.

What it registers, per scope in the config:

- the engine class as a **keyed, per-test service** under the scope name. The container builds it, passing the scope's `Config` (`JsonObject`) to its constructor;
- every engine interface (`ITestWareEngine`, `ISeleniumEngine`, ...) keyed under the same scope name, pointing at that instance. With `Configure` engines are **not** registered unkeyed, so an engine is always asked for by scope. The automatic way also registers them unkeyed, and with several scopes the last one registered answers.

Cockpits follow the same pattern with `CockpitScopes`, but live for the whole run. With `Configure` the unkeyed `ITestWareCockpit` is the first cockpit scope in the config; with the automatic way it is the last one registered.

Pages are registered **per scope** and **only for the scopes whose engine type they accept**. A Selenium page is reachable under `swagLabs` but not under `PlaySwagLabs`. With `Configure` the engine type is read from the constructors of the page set's pages, and a new page set, with its pages, is created for every request. With the automatic way the engine type is inferred from the page class's constructor parameters, and the page object lives for the running test. Either way it is built from the engine of that test.

Resolution is done with:

```csharp
TestWareProvider.GetTestWareComponent<T>()           // unkeyed
TestWareProvider.GetTestWareComponent<T>("scope")    // keyed by scope name
TestWareProvider.GetTestWareComponentFromTags(type, scopes, out consumed)  // tries each scope; falls back to unkeyed only for types no engine scope has (cockpit, config), otherwise throws
```

**A test's scope.** `TestWareProvider.BeginTest()` returns a `TestScope`. Until it is disposed, the calls above return the running test's engines, pages and components: the same engine for every request, and a different one for another test. Closing the scope (`TestScope.Close`) calls `Dispose` on each engine the test created, once each and last created first, then disposes the rest of the scope. An engine that throws does not stop the others from closing; `Close` returns the errors and never throws, and `Dispose` throws them as an `AggregateException`. The engines are tracked by the scope itself (`TestEngines`), not by the container, so `ITestWareEngine` is not `IDisposable`. The current test is carried in an `AsyncLocal`, so tests running at the same time do not see each other's. `TestScope.Get<T>("scope")` returns the same objects without the `AsyncLocal`. The root provider is built with scope validation, so asking for an engine, page or component outside a test throws `Cannot resolve scoped service ... from root provider`.

Who opens the scope: the MSTest wheel, at the start of `TestWareMethod.Execute` (section 6.1), and the NUnit sample's base class, in `[SetUp]` (section 6.2).

### 3.3 Attributes on tests

`src/Core/TestWare.Core/Attributes/`:

- `[TestWareScopes("a", "b")]` on a class or a method. Lists the scope names the test uses, in order. Class and method attributes are concatenated.
- `[TestWareDoc("Key", "value", ...)]` on a class or a method, repeatable. Free-form metadata forwarded to the cockpit. The Extent cockpit understands the keys `Name`, `Description`, `Tags`, `Devices`, `Authors`.
- `TestWareAttributes` has helpers that read both, either from `Type`/`MethodInfo` or from class and method names (then it searches all loaded assemblies for the type).

### 3.4 Interfaces every plug-in implements

`ITestWareEngine`: `Initialize`, `Dispose`, `CollectEvidence(path, name)` returning the file written, `StartRecordingEvidences`, `StopRecordingEvidences(path, name)`.

`ITestWareCockpit`: `Initialize`, `Dispose`, and Start/Stop for Suite, Case and Step, each returning a `Guid` handle, plus `Add...Activity(params string[])`. An activity that is an existing file path becomes an attachment, anything else becomes a log line. Results use the `TestWareResult` enum (Pass, Fail, Skip, Unknown).

`ITestwareComponent` is an empty marker interface. The automatic way finds page objects and actors through it: a page interface extends it, and the classes implementing that interface are registered as components. `Configure` does not use it.

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

**3. One implementation of the page set per engine** (SUT engine project). It lists the engine's page classes as constructor parameters and exposes them through the properties of the interface. Nobody writes `new LoginPage(engine)`: the container builds each page class from the test's engine. The interface still forces the property, so a page missing for an engine does not compile.

```csharp
public class SeleniumSwagLabsPages(LoginPage login) : ISwagLabsPages
{
    public ILoginPage Login => login;
}
```

All pages of a set are built together, so a page constructor should only store the engine and must not touch the browser. The automatic way builds only the page that is asked for. A page set that takes the engine itself and uses `new` (`SeleniumSwagLabsPages(ISeleniumEngine engine)`) still works.

**Registration** (the test project's start-up hook, for example `TestWareRun.cs`). One `AddEngine` per `CoreName` used in the config, one `AddPages` per application and engine. The hook names types; the container builds them (see below):

```csharp
TestWareProvider.Configure(configFile, tw => tw
    .AddEngine<SeleniumEngine>("Selenium")
    .AddPages<ISwagLabsPages, SeleniumSwagLabsPages>());
```

What `Configure` does with this:

- Each config scope is matched to the `AddEngine` whose name equals its `CoreName`. The engine is registered keyed by scope name, by its class and by each of its engine interfaces (`ISeleniumEngine`, `ITestWareEngine`), for one test at a time. The engine class needs a public constructor taking a `JsonObject` (the scope's `Config`); `AddEngine` throws at start-up if it has none.
- `AddPages<TPages, TImpl>` applies to every scope whose engine fits the engine parameter found in the constructors of `TImpl`'s pages (and of `TImpl` itself). The pages and `TImpl` together must name exactly one engine type, otherwise `AddPages` throws at start-up. A constructor parameter of `TImpl` counts as a page when it is a class that fits a property of the page set. For each such scope it registers the page set, and each property type of the page set (`ILoginPage`), keyed by scope name. It reads the properties of that one interface and of the interfaces it extends; nothing else is inspected.
- The container builds the objects, so a constructor may also take anything else that is registered (for example `ITestWareConfiguration`) without a change to the hook.

**Use in a test.** The test asks for the page, not the page set: `Page<ILoginPage>()` in the NUnit base class, or an `ILoginPage loginPage` parameter with `[TestWareMethod]`. Each call creates a new page object for the test's scope.

**Pages only some engines have.** Split the page set: the shared pages in one interface, the extra pages in an interface that extends it. Each engine implements the set it supports and is registered with that set.

```csharp
public interface IKaimaiPages        { ILoginPage Login { get; } }
public interface IKaimaiAndroidPages : IKaimaiPages { INotificationSettingsPage NotificationSettings { get; } }

.AddPages<IKaimaiAndroidPages, AndroidPages>()   // Login + NotificationSettings
.AddPages<IKaimaiPages, IosPages>()                  // Login
```

Applications that share no pages get separate page sets.

**When a mistake is caught:**

| Mistake | Caught |
|---|---|
| An engine's page set implementation misses a page | Build (CS0535) |
| A config `CoreName` has no `AddEngine` / `AddCockpit` | Start-up |
| The same page set, page type or component added twice for one scope | Start-up |
| An engine or cockpit without a `JsonObject` constructor; a page set whose pages and implementation name no engine type or more than one | Start-up, naming the class |
| An engine, page or component asked for outside a test | When asked: `Cannot resolve scoped service ... from root provider` |
| The `AddPages` line for an application is missing | When a test asks for the page. With `[TestWareMethod]` the message names the scopes that have it; `GetTestWareComponent<T>("scope")` (NUnit `Page<T>()`) gives the generic "No service registered" message. |
| A test asks for a page its scope's engine does not have | When the test asks for it, same messages as above |

Do not fill a page an engine lacks with `throw new NotImplementedException()`: it compiles, so the gap only shows when a test runs. Split the page set instead.

**Components that are not pages.** `AddComponent` registers other objects a test asks for. Each is one instance per test, built by the container like the engines:

```csharp
.AddComponent<StandardUser>()                                           // unkeyed: GetTestWareComponent<StandardUser>()
.AddComponent<BrowseTheWeb, PlaywrightEngine>()  // one per Playwright scope: GetTestWareComponent<BrowseTheWeb>("scope")
```

The Screenplay sample uses both (`TestSuiteBase.cs`). Adding the same component twice, or twice for one scope, throws at start-up.

## 4. Engines

### 4.1 Selenium (`TestWare.Engines.Selenium`, namespace `TestWare.Engines.SeleniumEngine`)

- `SeleniumEngine : ISeleniumEngine`. Registered with `AddEngine<SeleniumEngine>("Selenium")`, or found by the scan through `Name = "Selenium"`. Exposes `Driver`. The driver is created lazily on first access or by `Initialize()`, which also navigates to `BaseUrl` when set. `Dispose` quits the driver if one was started, never starts one, and releases the driver even if `Quit` throws.
- `SeleniumConfig`: `Driver` (Chrome, Firefox, Edge, InternetExplorer), `BaseUrl`, `ImplicitWaitSeconds`, `Service` (driver folder), `Options` (a JSON object applied by reflection onto the browser's options class; the key `Arguments` is special-cased to `AddArguments`).
- `DriverFactory` creates a local driver service. Remote (Grid) is a stub that throws.
- Extensions in `Extensions/` add `SafeClick`, `SafeSendKeys`, `ElementIsPresent`, and similar waits used by the sample pages.
- Evidence is a PNG screenshot. Recording start/stop are empty.

Extending: subclass `SeleniumEngine`, override what you need, register it under a new name with `AddEngine<SeleniumEngineWithNetworkTraces>("ExtendedSelenium")` and use that name as `CoreName` in the config. Pages written for `ISeleniumEngine` work with the subclass. `samples/tests/TestWare.Samples.Test.MsTest.ExtemdingEngines` does this to capture network traffic through DevTools.

### 4.2 Playwright (`TestWare.Engines.Playwright`, namespace `TestWare.Engines.PlaywrightEngine`)

- `PlaywrightEngine : ITestWareEngine`. Registered with `AddEngine<PlaywrightEngine>("Playwright")`, or found by the scan through `Name = "Playwright"`. Public fields `Browser` and `Page`; `Dispose` closes the browser. There is no `IPlaywrightEngine` interface, so pages depend on the class.
- `PlaywrightConfig`: `Browser` (Chromium, Firefox, Webkit), `BaseUrl`, `LaunchOptions` and `PageOptions` as raw JSON passed to Playwright's option classes.
- `Initialize` is a blocking wrapper over the async setup.

## 5. Cockpits (reporters)

Both keep a `Dictionary<Guid, handle>` so a test can address a suite, case or step by the `Guid` it got back, or omit it and address the current one.

- `ExtentCockpit` (`AddCockpit<ExtentCockpit>("ExtentReports")`, or `Name = "ExtentReports"` for the scan): config keys `ReportPath`, `ReportName`. `Initialize` creates the report, `Dispose` flushes it to disk. Suites are not report nodes; their metadata is merged into every test case.
- `ReportPortalCockpit` (`AddCockpit<ReportPortalCockpit>("ReportPortal")`, or `Name = "ReportPortal"` for the scan): config keys `Url`, `ProjectName`, `ApiKey`. `Initialize` starts a launch, `Dispose` finishes it. Suites, cases and steps are real items.

## 6. Wheels (runner adapters)

### 6.1 MSTest (`TestWare.Wheels.MsTestWheel`)

- `[TestWareMethod]` replaces `[DataTestMethod]`. `Execute` first opens the test's scope (section 3.2) and closes it at the end, so everything below, `[TestInitialize]` and `[TestCleanup]` included, sees the same engines. If an engine fails to close, a passed test is reported as failed with that error, and a failed test keeps its own failure and gets the error added to its error output. If the method has more parameters than the `[DataRow]` supplies, the extra ones are resolved from TestWare using the test's scopes, in order, consuming one scope per injected parameter. That is how `ValidLogin(string user, string password, ILoginPage loginPage)` receives a page.
- `ResultMapper.Translate` maps `UnitTestOutcome` to `TestWareResult`.

### 6.2 NUnit

No wheel project. `samples/tests/TestWare.Samples.Tests.NUnitIntegration/TestWareTestBase.cs` shows the equivalent done by hand (its `TestWareRun.cs` registers with `Configure`): `[SetUp]` reads the scope attribute, opens the test's scope (`BeginTest`) and starts the engine, `Page<T>()` resolves a page from that scope, `[TearDown]` takes a screenshot on failure and disposes the test's scope, which closes the browser. The base class is marked `[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]`, because NUnit otherwise shares one fixture instance between tests, and tests running in parallel would overwrite each other's engine.

## 7. Assistance: fluent steps

`StepChain` is an abstract class with `Step(name, action)` overloads. Each call runs `BeforeStep`, the action, then `AfterStep`, or `ErrorStep` on exception, and increments a step counter.
`AAAChain` (Arrange, Act, Assert) and `GherkinChain` (Given, When, Then) are the same thing with renamed methods.

The MSTest sample's `StepWithEvidenceChain` implements the hooks: before a step it starts a cockpit step, after it collects a screenshot from each engine and attaches it, on error it does the same and marks the step failed.

## 8. Lifecycle of one MSTest run

Taken from `samples/tests/TestWare.Samples.Tests.MsTest/`:

```
[AssemblyInitialize]   Hooks.AssemblyInitialize
                         TestWareProvider.RegisterTestWareComponents("TestConfig.json", assemblies)   scan and build the container (or Configure)
                         cockpit.Initialize()                                  open the report

[ClassInitialize]      TestSuiteBase.ClassInitialize
                         cockpit.StartTestSuite(className, docAttributes)

[TestWareMethod]       Execute: BeginTest()                                    open this test's scope
                         injects pages and engines for the test's scopes
                         testMethod.Invoke(...) runs, in order:

[TestInitialize]       TestSuiteBase.Setup
                         cockpit.StartTestCase(testName, docAttributes)
                         engine = GetTestWareComponent<ITestWareEngine>(firstScope)   this test's engine
                         engine.Initialize()                                   open the browser
                         engine.StartRecordingEvidences()
                         Steps = new StepWithEvidenceChain(...)

                       the test body

[TestCleanup]          TestSuiteBase.TearDown
                         engine.StopRecordingEvidences(...) -> attached
                         cockpit.StopTestCase(result)

[TestWareMethod]       the test's scope is disposed                            every engine of the test is disposed: the browser closes

[ClassCleanup]         cockpit.StopTestSuite()
[AssemblyCleanup]      cockpit.Dispose()                                       flush the report
```

Each test gets its own engines, so `Initialize` and `Dispose` are called once per engine instance. The Selenium engine creates its driver in `Initialize`.

## 9. How to add things

| Goal | Steps |
|---|---|
| New engine | Implement `ITestWareEngine` (and your own interface if pages should depend on an abstraction). Give it a public constructor taking a `JsonObject`; its `Dispose` runs once when the test ends and must close only what `Initialize` opened. Add `.AddEngine<YourEngine>("YourName")` to the start-up hook and use `"CoreName": "YourName"` in the config. |
| New cockpit | Same with `ITestWareCockpit` and `AddCockpit`. |
| New page object | Add the page interface to the SUT interfaces project and a property for it to the application's page set. The build then fails until every engine's page set implementation creates it (section 3.5). |
| New engine or cockpit (automatic way) | Implement the interface, add `public const string Name = "YourName"` and a constructor taking a `JsonObject`, use `"CoreName": "YourName"` in the config, and make sure its assembly is in the list passed to `RegisterTestWareComponents`. |
| New page object (automatic way) | Define the interface in a SUT project and make it extend `ITestwareComponent`. Implement it per engine with a public constructor taking the engine interface or class. Add the SUT assemblies to the list passed to `RegisterTestWareComponents`. No registration line per page. |
| New application | Add a page set interface, one implementation per engine, and one `.AddPages<TPages, TImpl>()` line per engine in the test project's start-up hook. |
| Object that is not a page (actor, ability) | `.AddComponent<T>()` or `.AddComponent<T, TEngine>()` in the start-up hook (section 3.5). |
| New test project | Copy the `Hooks.cs` + `TestSuiteBase.cs` pair (MSTest) or `TestWareRun.cs` + `TestWareTestBase.cs` (NUnit), add a `TestConfig.json` copied to output. |
| Use a second scope in one test | List both in `[TestWareScopes("a", "b")]`, declare two page parameters; they are filled in order. |

## 10. Known rough edges

Facts worth knowing before touching the code. None of these is a change request by itself.

- The root `README.md` describes `AutomationLifeCycleBase`, `IEngineManager`, `ContainerManager`. None exist in the code. This map is the current truth.
- Folder names and namespaces differ: folder `TestWare.Engines.Selenium`, namespace `TestWare.Engines.SeleniumEngine`. Same for Playwright.
- The marker interface is spelled `ITestwareComponent` (lower-case w).
- With `RegisterTestWareComponents`, `CoreName` is matched against a `Name` constant found by reflection. A typo or an assembly missing from the list throws at startup. Pages are matched to engines by looking at constructor parameters, so a page missing from a listed assembly is only noticed when a test asks for it.
- `RegisterTestWareComponents` force-loads every assembly referenced by anything in the process (134 assemblies and about 29,000 types for the main MSTest sample), and a referenced assembly that is not shipped makes it throw. Under NUnit this once needed the `TestCentric.Metadata` package; the NUnit sample avoids it by using `Configure`.
- The reporters are shared for the whole run, so running tests in parallel is not yet safe for reporting.
- `TestSuiteBase` (choose the scope, initialize the engine, evidence folder) is copied into each MSTest sample project, and the NUnit sample has its own base class. The per-test steps are not shared between projects.
- The suites initialize only the first scope's engine. A test with a second scope initializes that engine itself; the test's scope disposes it.
- `ReportPortalCockpit.Dispose` is `async void`, so the launch may not be finished when the process exits.
- Targets `net8.0` despite the "net6" in the original repository name.

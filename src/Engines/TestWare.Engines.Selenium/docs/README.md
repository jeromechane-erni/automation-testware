# How to use TestWare.Engines.Selenium

Drives a local browser with Selenium WebDriver. A test gets one `ISeleniumEngine` per test and scope. Use it for web tests in Chrome, Firefox, Edge or Internet Explorer.

## Needs

- Packages: `Selenium.WebDriver` 4.20.0, `SeleniumExtras.WaitHelpers` 1.0.2, project `TestWare.Core` (net8.0).
- Config: a scope with `"CoreName": "Selenium"`. Keys in its `Config`:

| Key | Meaning |
|---|---|
| `Driver` | `Chrome`, `Firefox`, `Edge` or `InternetExplorer` (not case sensitive). Required. |
| `BaseUrl` | Opened by `Initialize()`. Optional. |
| `Options` | `Arguments` (array) plus any property of the browser's options class by name, for example `ImplicitWaitTimeout` or `BinaryLocation`. |
| `Service` | Folder with the driver executable. Leave it out and Selenium finds or downloads the driver. |
| `ImplicitWaitSeconds` | Not an implicit wait: it is passed to the driver as the command timeout. |

- Other: the browser installed on the machine. Internet access on the first run if Selenium has to download a driver. Only local drivers: remote (Grid) is not implemented.

## Setup

A scope from `TestConfig.json` of the MSTest sample:

```json
{ "ScopeName": "swagLabs", "CoreName": "Selenium",
  "Config": { "Driver": "Chrome", "BaseUrl": "https://www.saucedemo.com/",
    "Options": { "ImplicitWaitTimeout": "00:00:10",
                 "Arguments": [ "--incognito", "--start-maximized" ] } } }
```

Registration, `TestWareRun.cs` in the NUnit sample:

```csharp
.AddEngine<SeleniumEngine>("Selenium")
```

With the automatic way (`RegisterTestWareComponents`) there is no registration line. The engine is found by its `Name = "Selenium"`, as long as `typeof(SeleniumEngine).Assembly` is in the list the hook passes (`Hooks.cs` of the Interoperability sample).

## Use

In a test, ask for the engine or let a page object read it. `LinearScriptSeleniumSwagLabs.cs` (MSTest sample):

```csharp
public void ValidLogin(ISeleniumEngine Engine, ITestWareCockpit Reporter)
{
    var userNameInput = Engine.Driver.FindElement(By.Id("user-name"));
    userNameInput.SendKeys("standard_user");
    var evidence = Engine.CollectEvidence(EvidencePath, "1.Credentials introduced");
```

- `Engine.Driver` creates the driver on first use. `Initialize()` creates it and opens `BaseUrl`. The suites in the samples call `Initialize()` in setup.
- `CollectEvidence(path, name)` saves `<name>.png` and returns the path. Recording start and stop do nothing.
- The test's scope calls `Dispose()` when the test ends. It quits the driver if one was started and never starts one.

Wait helpers in `TestWare.Engines.SeleniumEngine.Extensions` (`SafeClick`, `SafeSendKeys`, `ElementIsPresent`, `ElementIsNotPresent`, `UrlPathEndsWith`). `LoginPage.cs` in the Selenium SUT project:

```csharp
Driver.SafeSendKeys(_userNameInput, value);
```

Each helper has an overload with a `TimeSpan`. Without one it waits as long as the driver's implicit wait, which is why the sample sets `ImplicitWaitTimeout` in `Options`.

## Extend

Subclass `SeleniumEngine`, override what you need, and register it under a new name. `SeleniumEngineWithNetworkTraces.cs` in the ExtendingEngines sample captures network traffic:

```csharp
.AddEngine<SeleniumEngineWithNetworkTraces>("ExtendedSelenium")
```

Use `"CoreName": "ExtendedSelenium"` in the config. Pages written for `ISeleniumEngine` work with the subclass. With the automatic way the subclass declares `public new const string Name = "ExtendedSelenium"` and needs no line.

## Mistakes and what you see

| Mistake | What happens |
|---|---|
| `Driver` is a name that is not supported | `ArgumentException: Requested value 'Opera' was not found.` |
| `Driver` is missing from `Config` | `ArgumentNullException: Value cannot be null. (Parameter 'value')` |
| The engine is built without a config (`new SeleniumEngine()`) | `NullReferenceException: Configuration is mandatory` |
| No implicit wait is set and a helper is used without a timeout | The timeout is 0 (the driver's default implicit wait), so the helper gives up at once if the element is not ready |
| `ImplicitWaitSeconds` is set to get a wait | It becomes the command timeout, not a wait |

## Related

- Map: section 4.1.
- Changes: 0003 and 0004 in [docs/CHANGES.md](../../../../docs/CHANGES.md).

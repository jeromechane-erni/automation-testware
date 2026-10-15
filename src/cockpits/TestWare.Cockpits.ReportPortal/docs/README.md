# How to use TestWare.Cockpits.ReportPortal

A reporter (cockpit) that sends a launch with suites, test cases and steps to a ReportPortal server, with screenshots and files attached. Use it when results must be shared on a server. No sample registers it today.

## Needs

- Packages: `ReportPortal.Client` 3.4.1, `MimeTypeMapOfficial` 1.0.17, project `TestWare.Core` (net8.0).
- Config: a cockpit scope in `CockpitScopes` with `"CoreName": "ReportPortal"`. Keys in its `Config`, all required: `Url`, `ProjectName`, `ApiKey`.
- Other: a reachable ReportPortal server and a project on it. The API key is a secret: keep it out of git, for example in a file that is not committed.

## Setup

The cockpit scope (the sample configs used to carry one for the public demo server):

```json
"CockpitScopes": [
  { "ScopeName": "Remote", "CoreName": "ReportPortal",
    "Config": { "Url": "https://demo.reportportal.io", "ProjectName": "default_personal", "ApiKey": "<your key>" } } ]
```

Register it next to the other cockpits, and open it in the start-up hook as in the ExtentReports guide (`Hooks.cs` of the MSTest sample):

```csharp
.AddCockpit<ReportPortalCockpit>("ReportPortal")   // with the automatic way: typeof(ReportPortalCockpit).Assembly must be in the list
var Reporter = TestWareProvider.GetTestWareComponent<ITestWareCockpit>();
Reporter.Initialize();
```

If it is the first cockpit in `CockpitScopes`, it is the one `GetTestWareComponent<ITestWareCockpit>()` returns. Otherwise ask for it by scope name: `GetTestWareComponent<ITestWareCockpit>("Remote")`.

## Use

It implements the same calls as every cockpit, so test base classes report to it the same way (see the ExtentReports guide). What it does:

- `Initialize()` starts a launch named `LaunchName` and waits for the server.
- `StartTestSuite`, `StartTestCase` and `StartTestStep` create real items on the server: a suite, a test under the current suite, and a step under the current test.
- `Add...Activity(text)` adds a log line. If the text is a path to an existing file, the file is attached with its MIME type.
- `Stop...(result)` sets the status: Pass, Fail, Skip, or a warning for any other result.
- `Dispose()` finishes the launch.

## Extend

Not designed to be extended. It is a good model for a new reporter: implement `ITestWareCockpit` and register it with `AddCockpit`.

## Mistakes and what you see

| Mistake | What happens |
|---|---|
| `Url`, `ProjectName` or `ApiKey` is missing from `Config` | `NullReferenceException: Object reference not set to an instance of an object.` when the cockpit is first asked for |
| A test case is started before a suite | The suite lookup fails with a `KeyNotFoundException`: a test is created under the current suite, which does not exist yet |
| The process ends right after `Dispose()` | The launch can stay open: `Dispose` is `async void`, so nothing waits for it to finish |
| Tests run in parallel | The cockpit keeps one "current" suite, test and step in fields, so items of parallel tests get mixed |
| The launch name | It is always `LaunchName`; it cannot be set from the config |

## Related

- Map: section 5.
- Changes: 0003 and 0004 in [docs/CHANGES.md](../../../../docs/CHANGES.md).

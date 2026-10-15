# How to use TestWare.Cockpits.ExtentReports

A reporter (cockpit) that writes an HTML report with ExtentReports. Tests, cases and steps become report entries, and screenshots are embedded. Use it when you want a local report file.

## Needs

- Packages: `ExtentReports` 5.0.2, project `TestWare.Core` (net8.0).
- Config: a cockpit scope in `CockpitScopes` with `"CoreName": "ExtentReports"`. Keys in its `Config`: `ReportPath` (folder) and `ReportName` (file name). Both are required.
- Other: the report folder must be writable. The sample uses `C:\Temp\Results`.

## Setup

The cockpit scope from `TestConfig.json` of the MSTest sample:

```json
"CockpitScopes": [
  { "ScopeName": "Local", "CoreName": "ExtentReports",
    "Config": { "ReportPath": "C:\\Temp\\Results", "ReportName": "CustomReport.html" } } ]
```

Registration and start, `Hooks.cs` of the MSTest sample. The hook opens the report, and the cleanup hook writes it:

```csharp
.AddCockpit<ExtentCockpit>("ExtentReports")   // with the automatic way: typeof(ExtentCockpit).Assembly in the list, found by Name = "ExtentReports"
// AssemblyInitialize
var Reporter = TestWareProvider.GetTestWareComponent<ITestWareCockpit>();
Reporter.Initialize();
// AssemblyCleanup
Reporter.Dispose();
```

## Use

The cockpit is shared for the whole run. The test base class reports to it. `TestSuiteBase.cs` of the MSTest sample:

```csharp
reporter.StartTestSuite(context.ManagedType!, data);     // ClassInitialize
reporter.StartTestCase(TestContext.TestName!, data);     // TestInitialize
reporter.AddTestCaseActivity(evidence);                  // a file path or a text line
reporter.StopTestCase(ResultMapper.Translate(TestContext.CurrentTestOutcome));
```

- `Add...Activity(text)` adds a log line. If the text is a path to an existing file it adds an embedded image instead. Every file type is added as an image.
- Metadata comes from `[TestWareDoc]` on the class and the method. The keys it understands are `Name`, `Description`, `Tags`, `Devices` and `Authors`; suite and test values are merged.
- A suite is not a report node. Its metadata is added to every test case.
- The report file is written when `Dispose()` runs, so the report exists only after the run ends.

## Extend

Not designed to be extended. To report somewhere else, implement `ITestWareCockpit` (see the ReportPortal cockpit) and register it with `AddCockpit`.

## Mistakes and what you see

| Mistake | What happens |
|---|---|
| `ReportPath` or `ReportName` is missing from `Config` | `NullReferenceException: Object reference not set to an instance of an object.` when the cockpit is first asked for, which is the hook that opens the report |
| `Dispose()` is called before `Initialize()` | `NullReferenceException` |
| Several cockpit scopes are configured | Only the first one answers `GetTestWareComponent<ITestWareCockpit>()`; the others are reachable by scope name |
| Tests run in parallel | The cockpit keeps one "current" test, suite and step in fields, so entries of parallel tests get mixed |

## Related

- Map: section 5.
- Changes: 0003 and 0004 in [docs/CHANGES.md](../../../../docs/CHANGES.md).

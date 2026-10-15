# TestWare

TestWare is a thin layer between a .NET test runner (MSTest, NUnit) and the tools that drive an application under test (Selenium, Playwright) or report results (ExtentReports, ReportPortal). A JSON file names **scopes**; each scope binds a technology to its settings. A test says which scope it runs in and asks TestWare for the engine and the page objects of that scope. Every test gets its own engines, closed when the test ends.

This repository is the framework (`src/`) and runnable examples (`samples/`).

## Run the tests on a new machine

### 1. Install

| Tool | Why |
|---|---|
| [Git](https://git-scm.com/downloads) | to get the code |
| [.NET SDK](https://dotnet.microsoft.com/download) 8.0 or newer | the projects target `net8.0`. Check with `dotnet --list-sdks`. |
| Google Chrome | every browser test uses it, Playwright included (`"channel": "chrome"`) |
| Mozilla Firefox | one MSTest sample scope uses it (`TheInternet-checkboxes`) |
| Internet access | the tests open public demo sites (`saucedemo.com`, `the-internet.herokuapp.com`), and Selenium downloads the browser drivers when it needs them |
| A desktop session | the browsers open visible windows (`"headless": false` in the Playwright scopes) |

Checked on Windows 11 with .NET SDK 10.0.402 and Chrome. Not checked: macOS, Linux, a second Windows machine. The sample configs use the Windows folder `C:\Temp\Results`; see step 4 for other systems.

### 2. Get the code and build

```
git clone https://github.com/jeromechane-erni/automation-testware.git
cd automation-testware
dotnet build TestWare.sln
```

### 3. Run

Run one project at a time. Each opens real browsers; do not use the browser windows while a test runs.

| Project (folder `samples/tests/...`) | Runner | Needs | Shows |
|---|---|---|---|
| `TestWare.Samples.Tests.NUnitIntegration` | NUnit 4.3 | Chrome | a login test through a page object, a failure screenshot attached to the result |
| `TestWare.Samples.Tests.MsTest` | MSTest | Chrome, Firefox, Chrome through Playwright | linear scripts, page objects, steps with evidence, an HTML report |
| `TestWare.Samples.Tests.MsTest.SeleniumPlaywrightInteroperability` | MSTest | Chrome | Selenium and Playwright in one test |
| `TestWare.Samples.Test.MsTest.ExtemdingEngines` | MSTest | Chrome | an engine subclass that records network traffic |
| `TestWare.Samples.Tests.MsTest.Screenplay` | MSTest | Chrome through Playwright | actors and abilities |

```
dotnet test samples/tests/TestWare.Samples.Tests.NUnitIntegration
dotnet test samples/tests/TestWare.Samples.Tests.MsTest
```

Other useful forms:

```
dotnet test TestWare.sln                                   # all five projects
dotnet test samples/tests/TestWare.Samples.Tests.MsTest --filter "FullyQualifiedName~POMSeleniumSwagLabs"   # one class
dotnet test samples/tests/TestWare.Samples.Tests.MsTest --logger trx                                        # result file in TestResults/
dotnet test TestWare.sln --list-tests                      # list tests without running any browser
```

The tests drive public demo sites. If a site changes its page, a test can fail without a fault in TestWare. `TestFast` in the MSTest sample carries a `TODO MOVE TO OWN MIGRATION PROJECT` comment; treat it as a scratch test.

### 4. Environment configuration

Each test project has a `TestConfig.json` next to its code. The build copies it to the output folder, so edit the file in the project folder and run the tests again.

| What | Setting | Default | Change it when |
|---|---|---|---|
| Browser of a Selenium scope | `Driver` in the scope's `Config` | `Chrome` (one scope `Firefox`) | you want `Chrome`, `Firefox`, `Edge` or `InternetExplorer`, whichever is installed |
| Browser of a Playwright scope | `Browser`, `LaunchOptions` | `Chromium` through installed Chrome (`"channel": "chrome"`), `"headless": false` | there is no desktop session: set `"headless": true`. You prefer Playwright's own browser: remove `channel`, then run `pwsh <test project>/bin/Debug/net8.0/playwright.ps1 install` (needs PowerShell 7). |
| Where evidence and the report go | `EvidenceBasePath`, and `ReportPath` of the Extent cockpit | `C:\Temp\Results` in the four MSTest projects, `Evidence` in the NUnit sample (under the test output folder) | you are not on Windows or cannot write to `C:\Temp` |
| Browser drivers | `Service` (optional) | not set: Selenium finds or downloads them | the machine is offline: put the drivers in a folder and set `Service` to it |
| Sites under test | `BaseUrl` | the demo sites | you point a scope at your own application |

### 5. Results

- MSTest sample: the HTML report is written when the run ends, to `C:\Temp\Results\CustomReport.html`. Screenshots go to a numbered folder under `EvidenceBasePath`.
- NUnit sample: a failed test gets its screenshot attached to the result, so it appears in the test explorer and in a `--logger trx` file.

### 6. If something fails

| What you see | Meaning |
|---|---|
| `Could not find file '...TestConfig.json'` | the tests were started from a folder other than the test output. Run them with `dotnet test` or the IDE test runner. |
| `ArgumentException: Requested value 'X' was not found.` | `Driver` or `Browser` in a config names a browser that is not supported |
| `Scope 'x' needs engine 'Y', but none is added.` | a scope's `CoreName` has no matching `AddEngine` in the project's start-up hook |
| `No engine was found for: scope 'x' needs engine 'Y'...` | the same mistake in a project that registers with `RegisterTestWareComponents`: the assembly with that engine is not in the list its start-up hook passes |
| `Cannot resolve scoped service ... from root provider.` | an MSTest test uses `[TestMethod]` instead of `[TestWareMethod]` |

More in the guide of each project (below).

## How the code is organised

| Project (`src/...`) | What it is | Guide |
|---|---|---|
| `Core/TestWare.Core` | config file, registration (`Configure`, or `RegisterTestWareComponents` which finds engines and pages itself), per-test scope, attributes, interfaces | [guide](src/Core/TestWare.Core/docs/README.md) |
| `Engines/TestWare.Engines.Selenium` | Selenium WebDriver engine and wait helpers | [guide](src/Engines/TestWare.Engines.Selenium/docs/README.md) |
| `Engines/TestWare.Engines.Playwright` | Playwright engine | [guide](src/Engines/TestWare.Engines.Playwright/docs/README.md) |
| `cockpits/TestWare.Cockpits.ExtentReports` | HTML report | [guide](src/cockpits/TestWare.Cockpits.ExtentReports/docs/README.md) |
| `cockpits/TestWare.Cockpits.ReportPortal` | report to a ReportPortal server | [guide](src/cockpits/TestWare.Cockpits.ReportPortal/docs/README.md) |
| `Wheels/TestWare.Wheels.MsTestWheel` | MSTest adapter (`[TestWareMethod]`) | [guide](src/Wheels/TestWare.Wheels.MsTestWheel/docs/README.md) |
| `Assistance/TestWare.Assistance.FluentSteps` | step chain with before/after/error hooks | [guide](src/Assistance/TestWare.Assistance.FluentSteps/docs/README.md) |

`samples/suts` holds the page objects of the demo site (one interface project, one project per engine). `samples/tests` holds the test projects above.

## Documentation

- [docs/README.md](docs/README.md): index of the documents.
- [docs/framework-map.md](docs/framework-map.md): how the framework works inside.
- [docs/CHANGES.md](docs/CHANGES.md): what changed and why.

## Contributing

Please see our [Contribution Guide](CONTRIBUTING.md) to learn how to contribute.

## License

![MIT](https://img.shields.io/badge/License-MIT-blue.svg)

(LICENSE) © {{Year}} [ERNI - Swiss Software Engineering](https://www.betterask.erni)

## Code of conduct

Please see our [Code of Conduct](CODE_OF_CONDUCT.md)


## Contributors ✨

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->
<table>
  <tr>
    <td align="center"><a href="https://github.com/LopezMDidac"><img src="https://avatars.githubusercontent.com/u/20030140?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Didac Lopez</b></sub></a><br /><a href="https://github.com/ERNI-Academy/net6-automation-testware/commits?author=LopezMDidac" title="Code">💻</a> <a href="#content-LopezMDidac" title="Content">🖋</a> <a href="https://github.com/ERNI-Academy/net6-automation-testware/commits?author=LopezMDidac" title="Documentation">📖</a> <a href="#design-LopezMDidac" title="Design">🎨</a> <a href="#ideas-LopezMDidac" title="Ideas, Planning, & Feedback">🤔</a> <a href="#maintenance-LopezMDidac" title="Maintenance">🚧</a> <a href="https://github.com/ERNI-Academy/net6-automation-testware/commits?author=LopezMDidac" title="Tests">⚠️</a> <a href="#example-LopezMDidac" title="Examples">💡</a> <a href="https://github.com/ERNI-Academy/net6-automation-testware/pulls?q=is%3Apr+reviewed-by%3ALopezMDidac" title="Reviewed Pull Requests">👀</a></td>
    <td align="center"><a href="https://github.com/mg-diego"><img src="https://avatars.githubusercontent.com/u/39908763?v=4?s=100" width="100px;" alt=""/><br /><sub><b>mg-diego</b></sub></a><br /><a href="https://github.com/ERNI-Academy/net6-automation-testware/commits?author=mg-diego" title="Code">💻</a> <a href="#content-mg-diego" title="Content">🖋</a> <a href="https://github.com/ERNI-Academy/net6-automation-testware/commits?author=mg-diego" title="Documentation">📖</a> <a href="#design-mg-diego" title="Design">🎨</a> <a href="#ideas-mg-diego" title="Ideas, Planning, & Feedback">🤔</a> <a href="#maintenance-mg-diego" title="Maintenance">🚧</a> <a href="https://github.com/ERNI-Academy/net6-automation-testware/commits?author=mg-diego" title="Tests">⚠️</a> <a href="#example-mg-diego" title="Examples">💡</a> <a href="https://github.com/ERNI-Academy/net6-automation-testware/pulls?q=is%3Apr+reviewed-by%3Amg-diego" title="Reviewed Pull Requests">👀</a></td>
    <td align="center"><a href="https://github.com/Rabosa616"><img src="https://avatars.githubusercontent.com/u/12774781?v=4?s=100" width="100px;" alt=""/><br /><sub><b>Rabosa616</b></sub></a><br /><a href="https://github.com/ERNI-Academy/net6-automation-testware/commits?author=Rabosa616" title="Code">💻</a> <a href="#content-Rabosa616" title="Content">🖋</a> <a href="https://github.com/ERNI-Academy/net6-automation-testware/commits?author=Rabosa616" title="Documentation">📖</a> <a href="#design-Rabosa616" title="Design">🎨</a> <a href="#ideas-Rabosa616" title="Ideas, Planning, & Feedback">🤔</a> <a href="#maintenance-Rabosa616" title="Maintenance">🚧</a> <a href="https://github.com/ERNI-Academy/net6-automation-testware/commits?author=Rabosa616" title="Tests">⚠️</a> <a href="#example-Rabosa616" title="Examples">💡</a> <a href="https://github.com/ERNI-Academy/net6-automation-testware/pulls?q=is%3Apr+reviewed-by%3ARabosa616" title="Reviewed Pull Requests">👀</a></td>
  </tr>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->

Thanks goes to these wonderful people ([emoji key](https://allcontributors.org/docs/en/emoji-key)):

<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- ALL-CONTRIBUTORS-LIST:END -->
This project follows the [all-contributors](https://github.com/all-contributors/all-contributors) specification. Contributions of any kind welcome!

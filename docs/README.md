# TestWare documentation

Three kinds of document, each with one job:

| Kind | Answers | Where |
|---|---|---|
| Map | How does the framework work inside? | [framework-map.md](framework-map.md) |
| Guide | How do I use this project? | `<project>/docs/README.md`, one per project under `src/` (list below) |
| Change log | What changed, and why? | [CHANGES.md](CHANGES.md) |

## Project guides

Every project under `src/` has a guide. The repository [README](../README.md) explains how to run the sample tests on a new machine.

| Project | Guide |
|---|---|
| TestWare.Core | [src/Core/TestWare.Core/docs/README.md](../src/Core/TestWare.Core/docs/README.md) |
| TestWare.Engines.Selenium | [src/Engines/TestWare.Engines.Selenium/docs/README.md](../src/Engines/TestWare.Engines.Selenium/docs/README.md) |
| TestWare.Engines.Playwright | [src/Engines/TestWare.Engines.Playwright/docs/README.md](../src/Engines/TestWare.Engines.Playwright/docs/README.md) |
| TestWare.Cockpits.ExtentReports | [src/cockpits/TestWare.Cockpits.ExtentReports/docs/README.md](../src/cockpits/TestWare.Cockpits.ExtentReports/docs/README.md) |
| TestWare.Cockpits.ReportPortal | [src/cockpits/TestWare.Cockpits.ReportPortal/docs/README.md](../src/cockpits/TestWare.Cockpits.ReportPortal/docs/README.md) |
| TestWare.Wheels.MsTestWheel | [src/Wheels/TestWare.Wheels.MsTestWheel/docs/README.md](../src/Wheels/TestWare.Wheels.MsTestWheel/docs/README.md) |
| TestWare.Assistance.FluentSteps | [src/Assistance/TestWare.Assistance.FluentSteps/docs/README.md](../src/Assistance/TestWare.Assistance.FluentSteps/docs/README.md) |

When written, a guide lives at `<project>/docs/README.md` and the row links to it, for example `[src/Core/TestWare.Core/docs/README.md](../src/Core/TestWare.Core/docs/README.md)`.

The projects under `samples/` have no guide. They are the examples the guides point at.

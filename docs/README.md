# TestWare documentation

Three kinds of document, each with one job:

| Kind | Answers | Where |
|---|---|---|
| Map | How does the framework work inside? | [framework-map.md](framework-map.md) |
| Guide | How do I use this project? | `<project>/docs/README.md`, one per project under `src/` (list below) |
| Change log | What changed, and why? | [CHANGES.md](CHANGES.md) |

## Project guides

Add a row when a guide is written.

| Project | Guide |
|---|---|
| TestWare.Core | [src/Core/TestWare.Core/docs/README.md](../src/Core/TestWare.Core/docs/README.md) (pages only so far) |
| TestWare.Engines.Selenium | not written yet |
| TestWare.Engines.Playwright | not written yet |
| TestWare.Cockpits.ExtentReports | not written yet |
| TestWare.Cockpits.ReportPortal | not written yet |
| TestWare.Wheels.MsTestWheel | not written yet |
| TestWare.Assistance.FluentSteps | not written yet |

When written, a guide lives at `<project>/docs/README.md` and the row links to it, for example `[src/Core/TestWare.Core/docs/README.md](../src/Core/TestWare.Core/docs/README.md)`.

The projects under `samples/` have no guide. They are the examples the guides point at.

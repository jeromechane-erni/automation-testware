# Changes

What changed in the framework and why. Newest at the bottom.

The code before entry 0001 is commit `3fd5c98` ("Added Nunit version for one test").

### 0001 · 2026-10-15 · Stop silent failures when a component is not registered

**Files:** `src/Core/TestWare.Core/TestWareProvider.cs`, `samples/tests/TestWare.Samples.Tests.MsTest/TestConfig.json`

**What:**
- `RegisterTestWareComponents` now throws at startup when a scope in the config finds no engine or cockpit, for example a typo in `CoreName` or an assembly missing from the list passed to it. The message names the scope, the missing `CoreName` and the names that were found.
- `GetTestWareComponentFromTags` (used by `[TestWareMethod]` to inject parameters) no longer hands out a page or engine that belongs to another scope. If the type is registered for some engine scope but not for the test's scopes, it throws and says which scopes have it. Types not tied to an engine scope (the cockpit, the config) still fall back to the unkeyed registration. Exceptions from a page constructor are no longer swallowed.
- The MSTest sample config loses its `Remote` (ReportPortal) cockpit scope. Its assembly was never in `Hooks.cs`, so it was never registered and the new startup check would reject it. Behaviour is unchanged.

**Why:** Mistakes in the setup did not fail where they were made. If a page assembly was missing from the list, a test on a Playwright scope silently received the Selenium page and failed later in a confusing way. A typo in `CoreName` registered nothing and only surfaced when a test ran, as a generic "No service registered" error. Both cases were reproduced before the fix. Failing at startup with a precise message removes that gap.

### 0002 · 2026-10-15 · Add explicit registration with page sets checked by the compiler

**Files:** `src/Core/TestWare.Core/TestWareBuilder.cs` (new), `src/Core/TestWare.Core/TestWareProvider.cs`, `samples/suts/swaglabs/TestWare.Samples.Suts.SwagLabs.Interfaces/POM/ISwagLabsPages.cs` (new), `samples/suts/swaglabs/TestWare.Samples.Suts.SwagLabs.Selenium/POM/SeleniumSwagLabsPages.cs` (new), `samples/suts/swaglabs/TestWare.Samples.Suts.SwagLabs.Playwright/POM/PlaywrightSwagLabsPages.cs` (new), `samples/tests/TestWare.Samples.Tests.NUnitIntegration/TestWareRun.cs`, `samples/tests/TestWare.Samples.Tests.NUnitIntegration/TestWareTestBase.cs`, `samples/tests/TestWare.Samples.Tests.NUnitIntegration/POMSeleniumSwagLabs.cs`, `samples/tests/TestWare.Samples.Tests.NUnitIntegration/TestWare.Samples.Tests.NUnitIntegration.csproj`

**What:**
- New `TestWareProvider.Configure(configFile, tw => tw...)` registers only what it is told: `AddEngine("Selenium", cfg => new SeleniumEngine(cfg))`, `AddCockpit(...)`, and `AddPages<ISwagLabsPages, ISeleniumEngine>(e => new SeleniumSwagLabsPages(e))`. No assembly is scanned or loaded. A config scope with no matching `AddEngine`/`AddCockpit`, or the same page set added twice for one scope, throws at startup. `RegisterTestWareComponents` and its scan still exist; the MSTest samples still use them.
- A **page set** is one interface per application that lists its pages (`ISwagLabsPages` with `Login`), implemented once per engine (`SeleniumSwagLabsPages`, `PlaywrightSwagLabsPages`) with plain `new LoginPage(engine)`. A page missing from an engine's implementation is a build error (CS0535). The page set lives in the SUT projects only. Tests still ask for the page itself, `Page<ILoginPage>()`: `AddPages` reads the properties of the one page set interface it is given (and of the interfaces it extends, so a page set can be split into shared and engine-specific pages) and registers each property type for the scope. A page type provided twice for one scope throws at startup.
- The NUnit sample uses it; its test code is unchanged. Its csproj drops the `TestCentric.Metadata` package, which was only needed because the scan force-loaded the NUnit adapter's references.

**Why:** With the scan, a page missing for one engine was only discovered at runtime, when a test asked for it. The scan also depends on a hand-maintained assembly list and force-loads every referenced assembly, which broke under NUnit. Page sets make a missing page a compile error, because each engine's implementation must provide every page in the interface. They need no reflection and no assembly list. Pages are still registered one by one, so tests keep asking for the page they use (`Page<ILoginPage>()`) and do not depend on the page set. An earlier prototype, `AddPagesFrom`, found pages by reflection in a named assembly; it was dropped because it still only failed at startup, not at compile time.

**Open:** The MSTest and Screenplay samples still use the scan. Move them to `Configure` too, and then remove the scan? Screenplay actors and abilities have no page-set equivalent yet.

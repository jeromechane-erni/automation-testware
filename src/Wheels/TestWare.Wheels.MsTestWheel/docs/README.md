# How to use TestWare.Wheels.MsTestWheel

The adapter between TestWare and MSTest. `[TestWareMethod]` replaces `[TestMethod]` and `[DataTestMethod]`: it opens the test's scope, fills the test's parameters (engines, pages, reporters) from TestWare, and closes the engines when the test ends. Use it for every MSTest test that uses TestWare.

## Needs

- Packages: `MSTest.TestFramework` 3.5.2, project `TestWare.Core` (net8.0). The samples use MSTest adapters 3.3.1 and 3.0.4.
- Config: none of its own. It reads the scopes from `[TestWareScopes]` and the file `TestWare.Core` loads.
- Other: registration has already run in an `[AssemblyInitialize]` hook, with `RegisterTestWareComponents` or `Configure` (see the Core guide).

## Setup

Reference the project and put `[TestWareMethod]` on each test. `POMSeleniumSwagLabs.cs` in the MSTest sample:

```csharp
[TestClass]
[TestWareScopes("swagLabs")]
public class POMSeleniumSwagLabs : TestSuiteBase
{
    [TestWareMethod]
    [DataRow("standard_user", "secret_sauce")]
    public void ValidLogin(string user, string password, ILoginPage loginPage)
```

## Use

For each run of a test (each `DataRow` is one run):

1. The test's scope is opened. Everything below sees the same engines.
2. Parameters are filled in order. A parameter whose type equals the next `DataRow` value takes that value. Every other parameter is built from the container, using the test's scope names one at a time. `ILoginPage loginPage` gets the page for the scope.
3. `[TestInitialize]`, the test body and `[TestCleanup]` run. A suite can ask for the engine with `GetTestWareComponent<ITestWareEngine>("swagLabs")` and gets the same one.
4. The scope is closed: each engine is disposed once. Suites do not call `Dispose`.

A test that uses two scopes lists both and takes two engines, in order. `LinearScriptInteroperabilityEngines.cs`:

```csharp
[TestWareScopes("PlaySwagLabs", "swagLabs")]
public async Task LinearPlaywrightSelenium(ITestWareEngine playwrightEngine, ITestWareEngine seleniumEngine)
```

`ResultMapper.Translate(outcome)` turns an MSTest outcome into a `TestWareResult` for a reporter. `TestSuiteBase.cs` uses it in `[TestCleanup]`:

```csharp
reporter.StopTestCase(ResultMapper.Translate(TestContext.CurrentTestOutcome));
```

## Extend

Not designed to be extended. A wheel for another runner follows the same three steps: open the test's scope, provide what the test needs, close the scope (see the NUnit sample base class, `TestWareTestBase.cs`).

## Mistakes and what you see

| Mistake | What happens |
|---|---|
| `[TestMethod]` instead of `[TestWareMethod]` | No scope is opened: `[TestInitialize]` fails with `Cannot resolve scoped service ... from root provider.` |
| A `DataRow` value does not match its parameter's exact type | It is not used as a value: TestWare tries to inject that type instead and fails with `No service for type ... has been registered.` |
| A test asks for a page its scopes do not have | `No ILoginPage is registered for the scopes [nope]. It is registered for: swagLabs. ...` |
| An engine fails to close | A passed test is reported as failed: `The test's engines could not all be closed: <message>`. A failed test keeps its own failure and gets that message in its error output. |

## Related

- Map: section 6.1.
- Changes: 0003 and 0004 in [docs/CHANGES.md](../../../../docs/CHANGES.md).

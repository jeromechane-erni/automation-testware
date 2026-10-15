# How to use TestWare.Assistance.FluentSteps

A step chain for tests: each step runs between a "before" and an "after" hook, or an "error" hook when it throws, and steps are numbered. You write the hooks once (for example start a report step, take a screenshot) and every step gets them. Use it for linear test scripts that need a report entry and evidence per step.

## Needs

- Packages: none. It has no dependency on the rest of TestWare (net8.0).
- Config: none.
- Other: a class of your own that implements the hooks. The MSTest sample has one.

## Setup

Derive from `StepChain` and implement `BeforeStep`, `AfterStep` and `ErrorStep`. `StepWithEvidenceChain.cs` in the MSTest sample reports each step and takes a screenshot:

```csharp
public class StepWithEvidenceChain(string evidencePath, IEnumerable<ITestWareEngine> engines, IEnumerable<ITestWareCockpit> cockpits) : StepChain
{
    public override void BeforeStep<T>(string name, Func<dynamic[], T> function, params dynamic[] args)
    {
        foreach (var cockpit in cockpits) cockpit.StartTestStep(name);
    }
    // AfterStep and ErrorStep collect evidence from each engine: engine.CollectEvidence(evidencePath, $"{_stepN:0000}.{name}")
}
```

Create it in the test base class, `TestSuiteBase.cs`:

```csharp
Steps = new StepWithEvidenceChain(EvidencePath!, engine, reporter);
```

`AAAChain` (`Arrange`, `Act`, `Assert`) and `GherkinChain` (`Given`, `When`, `Then`) are the same chain with other method names. Derive from them instead of `StepChain` to use those names.

## Use

Each call is one step. `Step` returns the chain, so steps are chained. `LinearScriptSeleniumSwagLabs.cs` (MSTest sample):

```csharp
Steps!.Step(args => { userNameInput.SendKeys(args[0]); }, "performance_glitch_user")
    .Step(args => { passwordInput.SendKeys(args[0]); }, "secret_sauce")
    .Step(submitBtn.Click)
    .Step<int>(args => { return Engine.Driver.FindElements(By.XPath(args[0])).Count; }, out products, "//img[@class='inventory_item_img']");
```

- Arguments after the action are passed to it as `args`.
- `Step<T>(..., out T value, ...)` returns the action's result.
- A step runs `BeforeStep`, the action, then `AfterStep`. If the action throws, `ErrorStep` runs and the exception is thrown again, so the test fails.
- The step counter (`_stepN`) goes up after every step, passed or failed.
- Name a step with the `Step(string name, ...)` overloads to get a readable report entry and evidence file.

## Extend

Implement the three hooks as you need. Hooks receive the step's name, the action and its arguments. To change what a step does around the action, override `Step<T>(string name, Func<dynamic[], T> function, out T returnValue, params dynamic[] args)`; it is `virtual`.

## Mistakes and what you see

| Mistake | What happens |
|---|---|
| A step is not named | The name is the action's type name, so evidence files look like `0002.System.Action`1[System.Object[]].png` |
| A step throws | `ErrorStep` runs, then the same exception reaches the test |
| `AfterStep` throws | The step counts as failed: `ErrorStep` runs and the exception reaches the test |
| `ErrorStep` throws | Its exception reaches the test instead of the step's own |

## Related

- Map: section 7.
- Changes: none yet.

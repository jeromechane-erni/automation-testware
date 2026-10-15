using NUnit.Framework;
using NUnit.Framework.Interfaces;
using TestWare.Core;
using TestWare.Core.Attributes;
using TestWare.Core.Configuration;
using TestWare.Core.Interfaces;

namespace TestWare.Samples.Tests.NUnitIntegration;

/// <summary>
/// NUnit equivalent of the MSTest TestSuiteBase plus the parameter injection of [TestWareMethod].
/// Per test: pick the scope from [TestWareScopes], start its engine, expose pages, and on teardown
/// attach a screenshot when the test failed and close the browser.
/// </summary>
public abstract class TestWareTestBase
{
    private string? _scope;
    private bool _engineStarted;

    protected ITestWareEngine Engine { get; private set; } = null!;
    protected string EvidencePath { get; private set; } = null!;

    [SetUp]
    public void TestWareSetUp()
    {
        var test = TestContext.CurrentContext.Test;
        var method = GetType().GetMethod(test.MethodName!);

        _scope = TestWareAttributes.GetTestWareScopes(GetType(), method).FirstOrDefault()
            ?? throw new InvalidOperationException($"{GetType().Name}.{test.MethodName} needs a [TestWareScopes(\"...\")] attribute on the class or the method.");

        var config = TestWareProvider.GetTestWareComponent<ITestWareConfiguration>();
        EvidencePath = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            config.EvidenceBasePath,
            $"{DateTime.Now:yyyyMMdd-HHmmss}_{SafeFileName(test.Name)}");

        Engine = TestWareProvider.GetTestWareComponent<ITestWareEngine>(_scope);
        Engine.Initialize();
        _engineStarted = true;
    }

    /// <summary>Page for the scope of the running test, for example <c>Page&lt;ILoginPage&gt;()</c>.</summary>
    protected T Page<T>() where T : notnull
        => TestWareProvider.GetTestWareComponent<T>(_scope ?? throw new InvalidOperationException("No active TestWare scope."));

    [TearDown]
    public void TestWareTearDown()
    {
        // If setup failed before the browser opened, there is nothing to capture or close.
        // (Disposing then would make the engine start a browser just to close it.)
        if (!_engineStarted)
            return;

        try
        {
            if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed)
            {
                var screenshot = Engine.CollectEvidence(EvidencePath, "FAILED");
                TestContext.AddTestAttachment(screenshot, "Screenshot at failure");
            }
        }
        finally
        {
            Engine.Dispose();
            _engineStarted = false;
        }
    }

    private static string SafeFileName(string name)
        => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}

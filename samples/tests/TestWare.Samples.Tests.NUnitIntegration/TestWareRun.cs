using NUnit.Framework;
using TestWare.Core;
using TestWare.Engines.SeleniumEngine;
using TestWare.Samples.Suts.SwagLabs.Interfaces.POM;

namespace TestWare.Samples.Tests.NUnitIntegration;

/// <summary>
/// Runs once for every test in this namespace. NUnit equivalent of the MSTest [AssemblyInitialize] hook.
/// Registers the engines and page objects declared in TestConfig.json.
/// </summary>
[SetUpFixture]
public class TestWareRun
{
    [OneTimeSetUp]
    public void RegisterTestWare()
    {
        // The assemblies that hold the engine, the page interface and its Selenium implementation.
        // TestWare scans only what it is given here, so every assembly the tests need is listed.
        var assemblies = new[]
        {
            typeof(SeleniumEngine).Assembly,
            typeof(ILoginPage).Assembly,
            typeof(Suts.SwagLabs.Selenium.POM.LoginPage).Assembly,
        };

        var configFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestConfig.json");
        TestWareProvider.RegisterTestWareComponents(configFile, assemblies);
    }
}

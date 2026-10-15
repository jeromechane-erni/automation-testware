using NUnit.Framework;
using TestWare.Core;
using TestWare.Engines.SeleniumEngine;
using TestWare.Samples.Suts.SwagLabs.Interfaces.POM;
using TestWare.Samples.Suts.SwagLabs.Selenium.POM;

namespace TestWare.Samples.Tests.NUnitIntegration;

/// <summary>
/// Runs once for every test in this namespace. NUnit equivalent of the MSTest [AssemblyInitialize] hook.
/// Declares which engines and page sets exist. Nothing is scanned.
/// </summary>
[SetUpFixture]
public class TestWareRun
{
    [OneTimeSetUp]
    public void RegisterTestWare()
    {
        var configFile = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestConfig.json");

        TestWareProvider.Configure(configFile, tw => tw
            // "CoreName": "Selenium" in TestConfig.json -> this engine
            .AddEngine("Selenium", cfg => new SeleniumEngine(cfg))
            // SwagLabs pages on a Selenium scope
            .AddPages<ISwagLabsPages, ISeleniumEngine>(engine => new SeleniumSwagLabsPages(engine)));
    }
}

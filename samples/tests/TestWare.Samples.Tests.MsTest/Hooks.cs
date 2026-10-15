
using System.Reflection;
using TestWare.Cockpits.ExtentReportsCockpit;
using TestWare.Core;
using TestWare.Core.Interfaces;
using TestWare.Engines.PlaywrightEngine;
using TestWare.Engines.SeleniumEngine;
using TestWare.Samples.Suts.SwagLabs.Interfaces.POM;
using TestWare.Samples.Suts.SwagLabs.Playwright.POM;
using TestWare.Samples.Suts.SwagLabs.Selenium.POM;


namespace TestWare.Samples.Tests.MsTest;

[TestClass]
public class Hooks
{
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext testContext)
    {
        // Compile-time registration of TestWare components, instead of the automatic way in TestWare.Samples.Tests.MsTest.
        TestWareProvider.Configure("TestConfig.json", tw => tw
            .AddEngine<SeleniumEngine>("Selenium")
            .AddEngine<PlaywrightEngine>("Playwright")
            .AddCockpit<ExtentCockpit>("ExtentReports")
            .AddPages<ISwagLabsPages, SeleniumSwagLabsPages>()
            .AddPages<ISwagLabsPages, PlaywrightSwagLabsPages>());

        // IEnumerable<Assembly> extraAssemblies = [
        //     typeof(ExtentCockpit).Assembly,
        //     typeof(ILoginPage).Assembly,
        //     typeof(Suts.SwagLabs.Selenium.POM.LoginPage).Assembly,
        //     typeof(Suts.SwagLabs.Playwright.POM.LoginPage).Assembly,
        //     ];
        // TestWareProvider.RegisterTestWareComponents("TestConfig.json", extraAssemblies);
        var Reporter = TestWareProvider.GetTestWareComponent<ITestWareCockpit>();
        Reporter.Initialize();
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        var Reporter = TestWareProvider.GetTestWareComponent<ITestWareCockpit>();
        Reporter.Dispose();
    }
}

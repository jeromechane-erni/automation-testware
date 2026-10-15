
using System.Reflection;
using TestWare.Core;
using TestWare.Engines.PlaywrightEngine;
using TestWare.Engines.SeleniumEngine;


namespace TestWare.Samples.Tests.MsTest.SeleniumPlaywrightInteroperability;

[TestClass]
public class Hooks
{
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext testContext)
    {
        // Compile-time registration of TestWare components, instead of the automatic way in TestWare.Samples.Tests.MsTest.
        // TestWareProvider.Configure("TestConfig.json", tw => tw
        //     .AddEngine<SeleniumEngine>("Selenium")
        //     .AddEngine<PlaywrightEngine>("Playwright"));

        IEnumerable<Assembly> extraAssemblies = [
            typeof(SeleniumEngine).Assembly,
            typeof(PlaywrightEngine).Assembly,
        ];

        TestWareProvider.RegisterTestWareComponents("TestConfig.json", extraAssemblies);

    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {

    }
}

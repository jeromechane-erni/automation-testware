
using TestWare.Core.Interfaces;
using TestWare.Core;
using TestWare.Core.Configuration;
using TestWare.Core.Attributes;
using TestWare.Wheels.MsTestWheel;
using System.Reflection;
using static System.Formats.Asn1.AsnWriter;
using TestWare.Samples.Tests.MsTest.Screenplay.Actors;
using TestWare.Samples.Tests.MsTest.Screenplay.Abilities;
using TestWare.Engines.PlaywrightEngine;


namespace TestWare.Samples.Tests.MsTest.Screenplay;


[TestClass]
public abstract class TestSuiteBase
{
    public TestContext? TestContext { get; set; }
    protected string? EvidencePath { get; set; }


    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext testContext)
    {
        // Compile-time registration of TestWare components, instead of the automatic way in TestWare.Samples.Tests.MsTest.
        // TestWareProvider.Configure("TestConfig.json", tw => tw
        //     .AddEngine<PlaywrightEngine>("Playwright")
        //     // the actor is one shared object for the run, as the scan made it
        //     .AddComponent<StandardUser>()
        //     // BrowseTheWeb for every Playwright scope
        //     .AddComponent<BrowseTheWeb, PlaywrightEngine>());

        TestWareProvider.RegisterTestWareComponents("TestConfig.json");
    }

    [TestInitialize]
    public void Setup()
    {
        var scopes = TestWareAttributes.GetTestWareScopes(TestContext!.ManagedType!, TestContext.TestName!);
        var actor = TestWareProvider.GetTestWareComponent<StandardUser>();
        actor.HasAbility(TestWareProvider.GetTestWareComponent<BrowseTheWeb>(scopes.First()))
            .HasAbility(LogTheActivity.To(Console.Out));
    }

    [TestCleanup]
    public void TearDown()
    {
        //var scopes = TestWareAttributes.GetTestWareScopes(TestContext!.ManagedType!, TestContext.TestName!);
        //var Engine = TestWareProvider.GetTestWareComponent<ITestWareEngine>(scopes.First());
        //Engine.Dispose();
    }
}

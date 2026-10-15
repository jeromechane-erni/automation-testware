using NUnit.Framework;
using TestWare.Core.Attributes;
using TestWare.Samples.Suts.SwagLabs.Interfaces.POM;

namespace TestWare.Samples.Tests.NUnitIntegration;

[TestFixture]
[TestWareScopes("swagLabs")]
[Description("Minimal test over the swagLabs scope using Selenium as engine and a page object as test implementation (NUnit).")]
public class POMSeleniumSwagLabs : TestWareTestBase
{
    [TestCase("standard_user", "secret_sauce")]
    [Category("SwagLabsTesting")]
    [Category("Selenium")]
    [Description("Test case for valid login in the platform, using the shared ILoginPage page object.")]
    public void ValidLogin(string user, string password)
    {
        var loginPage = Page<ILoginPage>();

        Assert.That(loginPage.IsLoaded(), Is.True);
        loginPage.SetUserName(user);
        loginPage.SetPassword(password);
        loginPage.SubmitCredentials();
        Assert.That(loginPage.IsNotLoaded(), Is.True);
    }
}

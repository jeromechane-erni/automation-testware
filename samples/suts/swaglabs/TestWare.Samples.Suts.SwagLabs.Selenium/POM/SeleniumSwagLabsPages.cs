using TestWare.Engines.SeleniumEngine;
using TestWare.Samples.Suts.SwagLabs.Interfaces.POM;

namespace TestWare.Samples.Suts.SwagLabs.Selenium.POM;

/// <summary>SwagLabs pages driven by Selenium.</summary>
public class SeleniumSwagLabsPages(ISeleniumEngine engine) : ISwagLabsPages
{
    public ILoginPage Login => new LoginPage(engine);
}

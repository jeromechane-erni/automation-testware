using TestWare.Engines.PlaywrightEngine;
using TestWare.Samples.Suts.SwagLabs.Interfaces.POM;

namespace TestWare.Samples.Suts.SwagLabs.Playwright.POM;

/// <summary>SwagLabs pages driven by Playwright.</summary>
public class PlaywrightSwagLabsPages(PlaywrightEngine engine) : ISwagLabsPages
{
    public ILoginPage Login => new LoginPage(engine);
}

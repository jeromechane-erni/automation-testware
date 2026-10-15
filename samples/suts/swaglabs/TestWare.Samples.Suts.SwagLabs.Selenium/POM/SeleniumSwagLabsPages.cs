using TestWare.Samples.Suts.SwagLabs.Interfaces.POM;

namespace TestWare.Samples.Suts.SwagLabs.Selenium.POM;

/// <summary>
/// SwagLabs pages driven by Selenium. One class like this per application and engine.
///
/// Usage:
///   - List every page of the application as a constructor parameter: the page CLASS of this engine (LoginPage).
///     The container builds each one from the test's engine, so nobody writes "new LoginPage(engine)".
///   - Expose it through the property that ISwagLabsPages requires.
///   - Register the set once, in the test project's start-up: <c>.AddPages&lt;ISwagLabsPages, SeleniumSwagLabsPages&gt;()</c>.
///
/// Compile-time check: ISwagLabsPages lists the pages of the application. When a page is added there,
/// this class does not compile (CS0535) until it provides it, so no engine can silently miss a page.
///
/// Keep page constructors to storing the engine: asking for any one page builds all the pages of the set.
/// </summary>
public class SeleniumSwagLabsPages(LoginPage login) : ISwagLabsPages
{
    public ILoginPage Login => login;
}

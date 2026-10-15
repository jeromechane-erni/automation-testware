namespace TestWare.Samples.Suts.SwagLabs.Interfaces.POM;

/// <summary>
/// Every page of SwagLabs. Each engine project implements this once,
/// so a page that is missing for an engine is a build error.
/// </summary>
public interface ISwagLabsPages
{
    ILoginPage Login { get; }
}

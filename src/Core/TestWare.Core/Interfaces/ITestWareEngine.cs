namespace TestWare.Core.Interfaces;

/// <summary>
/// An engine drives one technology (a browser, an app). One instance lives for one test. The test's scope calls
/// <c>Dispose</c> once when the test ends, so it must close what <c>Initialize</c> opened, and must not start anything
/// that was never started. If it throws, the error is reported with the test result and the other engines are still closed.
/// </summary>
public interface ITestWareEngine
{
    void Initialize();
    void Dispose();
    string CollectEvidence(string destinationPath, string evidenceName);

    void StartRecordingEvidences();

    string StopRecordingEvidences(string destinationPath, string evidenceName);
}

using Microsoft.Extensions.DependencyInjection;
using TestWare.Core.Interfaces;

namespace TestWare.Core;

/// <summary>
/// One running test. Created by <see cref="TestWareProvider.BeginTest"/>. The engines, pages and components of the test live in it.
/// Close it when the test ends: that closes every engine the test created, each one on its own, so one engine that fails to close
/// does not leave the others open.
/// </summary>
public sealed class TestScope : IDisposable
{
    private readonly IServiceScope _scope;
    private bool _closed;

    internal TestScope(IServiceScope scope) => _scope = scope;

    internal IServiceProvider Provider => _scope.ServiceProvider;

    /// <summary>Retrieves an unkeyed component for this test.</summary>
    public T Get<T>() where T : notnull => Provider.GetRequiredService<T>();

    /// <summary>Retrieves an engine, page or component of the scope named <paramref name="scopeName"/> for this test.</summary>
    public T Get<T>(string scopeName) where T : notnull => Provider.GetRequiredKeyedService<T>(scopeName);

    /// <summary>
    /// Ends the test: disposes its engines (last created first, each exactly once) and the rest of the scope.
    /// Never throws. Returns what went wrong, so a runner can report it next to the test's own result instead of in place of it.
    /// Calling it again returns an empty list.
    /// </summary>
    public IReadOnlyList<Exception> Close()
    {
        if (_closed)
            return [];
        _closed = true;
        TestWareProvider.EndTest(this);

        var errors = new List<Exception>();
        var engines = Provider.GetRequiredService<TestEngines>().Created;
        for (var i = engines.Count - 1; i >= 0; i--)
        {
            try { engines[i].Dispose(); }
            catch (Exception e) { errors.Add(e); }
        }

        try { _scope.Dispose(); }
        catch (Exception e) { errors.Add(e); }
        return errors;
    }

    /// <summary>Same as <see cref="Close"/>, but throws an <see cref="AggregateException"/> when something could not be closed.</summary>
    public void Dispose()
    {
        var errors = Close();
        if (errors.Count > 0)
            throw new AggregateException("The test's engines could not all be closed: " + string.Join("; ", errors.Select(e => e.Message)), errors);
    }
}

/// <summary>The engines created for one test, in creation order. A scoped service, so every test has its own list.</summary>
internal sealed class TestEngines
{
    public List<ITestWareEngine> Created { get; } = new();
}

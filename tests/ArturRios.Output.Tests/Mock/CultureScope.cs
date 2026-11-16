using System.Globalization;

namespace ArturRios.Output.Tests.Mock;

/// <summary>Sets the current culture until disposed; an empty name is the invariant culture.</summary>
/// <remarks>The culture flows with the test's execution context only, so tests running in parallel keep theirs.</remarks>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previous = CultureInfo.CurrentCulture;

    public CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);

    public void Dispose() => CultureInfo.CurrentCulture = _previous;
}

using MortysDLP.Services.Releases;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MortysDLP.Tests.Releases;

/// <summary>
/// Attrappe für <see cref="IReleaseSource"/>: fester Rückgabewert, künstliche Verzögerung und
/// optionale Ausnahme, alles über den Konstruktor konfigurierbar. Zählt ihre Aufrufe, damit die
/// Resolver-Tests unabhängig von HTTP bleiben.
/// </summary>
internal sealed class FakeReleaseSource(
    string name,
    bool isAuthoritative = false,
    ReleaseInfo? result = null,
    TimeSpan? delay = null,
    Exception? throwException = null) : IReleaseSource
{
    public string Name { get; } = name;

    public bool IsAuthoritative { get; } = isAuthoritative;

    public int CallCount { get; private set; }

    public async Task<ReleaseInfo?> TryGetLatestAsync(ReleaseQuery query, CancellationToken ct)
    {
        CallCount++;

        if (delay is { } d)
            await Task.Delay(d, ct);

        ct.ThrowIfCancellationRequested();

        if (throwException is { } exception)
            throw exception;

        return result;
    }
}

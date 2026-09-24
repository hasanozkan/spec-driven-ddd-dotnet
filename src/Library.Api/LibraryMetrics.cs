using System.Diagnostics.Metrics;
using Library.Contracts;

namespace Library.Api;

/// <summary>
/// Feature 004 (specs/telemetry.yaml). Domain numbers come from the integration
/// events — the contexts do not know they are measured. Request duration is
/// ASP.NET Core's own <c>http.server.request.duration</c> (OTel semantic conventions).
/// </summary>
public sealed class LibraryMetrics
{
    public const string MeterName = "Library";

    private readonly Counter<long> _opened;
    private readonly Counter<long> _closed;
    private readonly Counter<long> _lateFees;
    private readonly Counter<long> _refusals;

    public LibraryMetrics(IMeterFactory meters)
    {
        var meter = meters.Create(MeterName);
        _opened = meter.CreateCounter<long>("library.loans.opened", description: "Loans opened");
        _closed = meter.CreateCounter<long>("library.loans.closed", description: "Loans closed, by lateness");
        _lateFees = meter.CreateCounter<long>("library.late_fees.cents", description: "Late fees charged, in cents");
        _refusals = meter.CreateCounter<long>("library.refusals", description: "Domain refusals, by problem code");
    }

    /// <summary>OPS-R2.</summary>
    public void Attach(IEventBus bus)
    {
        bus.Subscribe<LoanOpened>(_ => _opened.Add(1));
        bus.Subscribe<LoanClosed>(e =>
        {
            _closed.Add(1, new KeyValuePair<string, object?>("library.late", e.LateFeeCents > 0 ? "true" : "false"));
            if (e.LateFeeCents > 0) _lateFees.Add(e.LateFeeCents);
        });
    }

    /// <summary>OPS-R3.</summary>
    public void Refused(string code) => _refusals.Add(1, new KeyValuePair<string, object?>("library.code", code));
}

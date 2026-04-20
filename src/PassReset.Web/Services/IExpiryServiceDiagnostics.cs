namespace PassReset.Web.Services;

/// <summary>
/// Exposes expiry notification service diagnostics for health monitoring.
/// Only aggregate state is exposed — no per-user data or email addresses.
/// </summary>
public interface IExpiryServiceDiagnostics
{
    /// <summary>Whether the service is configured and running.</summary>
    bool IsEnabled { get; }

    /// <summary>UTC timestamp of the last successful notification tick. Null if never run.</summary>
    DateTimeOffset? LastTickUtc { get; }
}

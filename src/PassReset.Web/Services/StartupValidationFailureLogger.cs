using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace PassReset.Web.Services;

/// <summary>
/// Writes <see cref="OptionsValidationException"/> detail to the Windows Application
/// Event Log under source <c>PassReset</c> (event ID 1001, Error) so operators see
/// the configuration failure in Event Viewer rather than a bare IIS 502.
/// </summary>
/// <remarks>
/// The Event Log source is registered by <c>Install-PassReset.ps1</c>. If the source
/// does not yet exist (first install before installer completes, or developer machine)
/// the write is silently skipped — the original exception still propagates and is
/// captured by the ASP.NET Core module's stdout logging.
/// </remarks>
internal static class StartupValidationFailureLogger
{
    private const string EventLogSource = "PassReset";
    private const int EventId = 1001;

    /// <summary>
    /// Best-effort: emit a single error-level Event Log entry describing each
    /// <see cref="OptionsValidationException.Failures"/> item. Never throws — the
    /// caller must still re-throw the original exception.
    /// </summary>
    public static void LogToEventLog(OptionsValidationException ex)
    {
        var message = "PassReset startup configuration validation failed:\n"
                      + string.Join("\n", ex.Failures ?? []);
        try
        {
#pragma warning disable CA1416 // System.Diagnostics.EventLog is Windows-only; PassReset.Web targets net10.0-windows.
            if (EventLog.SourceExists(EventLogSource))
            {
                EventLog.WriteEntry(EventLogSource, message, EventLogEntryType.Error, EventId);
            }
#pragma warning restore CA1416
            // If source doesn't exist, the installer hasn't registered it yet.
            // Swallow silently — the exception re-throws and IIS stdout logging captures it.
        }
        catch
        {
            // EventLog APIs can throw on permission issues; never let logging mask the
            // original configuration error.
        }
    }
}

using System.Diagnostics;
using Serilog.Context;

namespace PassReset.Web.Middleware;

/// <summary>
/// Enriches the Serilog <see cref="LogContext"/> with the current W3C
/// <see cref="Activity"/> TraceId and SpanId for the duration of each HTTP request,
/// so every log event emitted during request processing correlates via the W3C
/// distributed-tracing identifier (32-char lowercase hex TraceId +
/// 16-char lowercase hex SpanId).
/// </summary>
/// <remarks>
/// Registered in <c>Program.cs</c> after <c>UseSerilogRequestLogging()</c> and before
/// <c>UseRouting()</c>. ASP.NET Core hosting diagnostics populate
/// <see cref="Activity.Current"/> before any user middleware runs, so both values are
/// available on the very first log call. Null values are coalesced to
/// <c>"unknown"</c> to keep the property shape stable in the JSON sink output.
///
/// Both <see cref="LogContext.PushProperty(string, object?)"/> disposables MUST be
/// opened via <c>using</c> blocks bracketing the downstream <c>next(context)</c>
/// call — otherwise the properties are popped immediately and never reach the log
/// events emitted by downstream middleware / MVC handlers.
/// </remarks>
public sealed class TraceIdEnricherMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>Constructs the middleware with the next delegate in the pipeline.</summary>
    public TraceIdEnricherMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>Pushes TraceId + SpanId onto <see cref="LogContext"/> for the request.</summary>
    public async Task Invoke(HttpContext context)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? "unknown";
        var spanId  = Activity.Current?.SpanId.ToString()  ?? "unknown";

        using (LogContext.PushProperty("TraceId", traceId))
        using (LogContext.PushProperty("SpanId", spanId))
        {
            await _next(context);
        }
    }
}

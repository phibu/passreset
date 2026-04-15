using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace PassReset.PasswordProvider;

/// <summary>
/// Structured logging helper that walks an <see cref="Exception.InnerException"/>
/// chain and emits each frame as a <c>{depth, type, hresult, message}</c> object
/// under a single Serilog <c>ExceptionChain</c> scope property.
/// </summary>
/// <remarks>
/// Scoped to the two exception types that carry HRESULT / LDAP diagnostic value
/// for the AD password-change flow: <c>DirectoryServicesCOMException</c> and
/// <c>PasswordException</c> (see CONTEXT.md §3 and D-02). Other exception types
/// should continue to use <see cref="ILogger"/>'s default exception destructure.
/// The top-level exception is still passed to <see cref="ILogger.Log"/> so the
/// default renderer emits the stack trace; only the chain summary (without
/// <c>TargetSite</c>, <c>Data</c>, <c>HelpLink</c>, etc.) is attached as a scope
/// property for structured querying.
/// </remarks>
public static class ExceptionChainLogger
{
    /// <summary>
    /// Walks the inner-exception chain of <paramref name="exception"/>, pushes
    /// the resulting list onto Serilog's <see cref="LogContext"/> as an
    /// <c>ExceptionChain</c> structured property, and emits a warning log entry
    /// with the supplied template. The top-level exception is attached to the
    /// log entry so Serilog's default renderer captures the stack trace.
    /// </summary>
    /// <param name="logger">Destination logger.</param>
    /// <param name="exception">Top-level exception (non-null).</param>
    /// <param name="messageTemplate">Standard Serilog message template.</param>
    /// <param name="args">Template arguments.</param>
    /// <summary>
     /// Maximum inner-exception depth the walker will traverse. Guards against
     /// pathologically deep or adversarial chains that could bloat log events
     /// and stall the logging pipeline.
     /// </summary>
    internal const int MaxDepth = 32;

    public static void LogExceptionChain(
        ILogger logger,
        Exception exception,
        string messageTemplate,
        params object?[] args)
    {
        var chain = new List<object>();
        var seen = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var depth = 0;
        var cur = exception;
        bool depthTruncated = false;
        bool cycleDetected = false;

        while (cur is not null)
        {
            if (depth >= MaxDepth)
            {
                depthTruncated = true;
                break;
            }
            if (!seen.Add(cur))
            {
                cycleDetected = true;
                break;
            }

            chain.Add(new
            {
                depth,
                type    = cur.GetType().Name,
                hresult = $"0x{cur.HResult:X8}",
                message = cur.Message,
            });

            cur = cur.InnerException;
            depth++;
        }

        if (depthTruncated || cycleDetected)
        {
            chain.Add(new
            {
                depth,
                type    = "ExceptionChainSentinel",
                hresult = "0x00000000",
                message = cycleDetected
                    ? "inner-exception cycle detected; chain truncated"
                    : $"max depth {MaxDepth} reached; chain truncated",
            });
        }

        using (LogContext.PushProperty("ExceptionChain", chain, destructureObjects: true))
        {
            logger.LogWarning(exception, messageTemplate, args);
        }
    }
}

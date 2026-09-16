using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;

namespace Altinn.Dd.Correspondence.Exceptions;

/// <summary>
/// Translates the resilience pipeline's rejections into <see cref="CorrespondenceServiceException"/>,
/// so Polly types do not reach callers.
/// </summary>
internal static class ResilienceFailure
{
    /// <summary>Wraps <paramref name="rejection"/> in a message that says which limit was hit.</summary>
    public static CorrespondenceServiceException Translate(ExecutionRejectedException rejection) => rejection switch
    {
        TimeoutRejectedException => new CorrespondenceServiceException(
            "The correspondence request timed out before Altinn responded. The attempt and total " +
            "timeouts are configurable on the resilience options.", rejection),

        BrokenCircuitException => new CorrespondenceServiceException(
            "The correspondence request was not attempted because too many recent calls to Altinn " +
            "failed and the circuit breaker is open. It closes again after the break duration.", rejection),

        RateLimiterRejectedException => new CorrespondenceServiceException(
            "The correspondence request was rejected because too many requests were already in " +
            "flight. Reduce concurrency or raise the rate limiter's permit limit.", rejection),

        _ => new CorrespondenceServiceException(
            "The correspondence request was rejected by the resilience pipeline before it reached " +
            "Altinn.", rejection)
    };
}

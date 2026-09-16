namespace Altinn.Dd.Correspondence.Exceptions;

/// <summary>
/// Thrown when a correspondence request is rejected before Altinn produced a response - the
/// request timed out, the circuit breaker is open, or the concurrency limit was reached.
/// </summary>
/// <remarks>
/// The resilience pipeline raises these as Polly exceptions. They are translated here so callers
/// can handle them without referencing Polly, and so the library's public surface does not depend
/// on which resilience implementation is in use.
///
/// An API rejection that carries a problem document is returned as a failure result instead; see
/// the package README for which statuses take which route.
/// </remarks>
public class CorrespondenceServiceException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CorrespondenceServiceException"/> class with a specified error message
    /// </summary>
    /// <param name="message">The message that describes the error</param>
    public CorrespondenceServiceException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrespondenceServiceException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception</param>
    /// <param name="innerException">The exception that is the cause of the current exception</param>
    public CorrespondenceServiceException(string message, Exception innerException) : base(message, innerException) { }
}

namespace Altinn.Dd.Correspondence.Exceptions;

/// <summary>
/// Exception thrown when errors occur in the Altinn 3 Correspondence service
/// </summary>
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
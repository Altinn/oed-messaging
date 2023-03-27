﻿namespace Altinn.Oed.Messaging.Exceptions;
public class MessagingServiceException : Exception
{
    public MessagingServiceException(string message) : base(message) { }
    public MessagingServiceException(string message, Exception innerException) : base(message, innerException) { }
}

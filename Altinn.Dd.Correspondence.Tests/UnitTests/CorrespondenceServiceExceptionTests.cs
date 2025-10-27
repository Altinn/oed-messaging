using Altinn.Dd.Correspondence.Exceptions;
using FluentAssertions;
using Xunit;

namespace Altinn.Dd.Correspondence.Tests.UnitTests;

/// <summary>
/// Unit tests for CorrespondenceServiceException
/// </summary>
public class CorrespondenceServiceExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_CreatesExceptionWithMessage()
    {
        // Arrange
        const string message = "Test error message";

        // Act
        var exception = new CorrespondenceServiceException(message);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_CreatesExceptionWithBoth()
    {
        // Arrange
        const string message = "Test error message";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new CorrespondenceServiceException(message, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public void Constructor_WithNullMessage_CreatesExceptionWithDefaultMessage()
    {
        // Act
        var exception = new CorrespondenceServiceException(null!);

        // Assert
        // .NET exceptions don't allow null messages, they get a default message
        exception.Message.Should().NotBeNull();
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithEmptyMessage_CreatesExceptionWithEmptyMessage()
    {
        // Arrange
        const string message = "";

        // Act
        var exception = new CorrespondenceServiceException(message);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullInnerException_CreatesExceptionWithNullInnerException()
    {
        // Arrange
        const string message = "Test error message";

        // Act
        var exception = new CorrespondenceServiceException(message, null!);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Exception_InheritsFromException()
    {
        // Arrange & Act
        var exception = new CorrespondenceServiceException("Test");

        // Assert
        exception.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void Exception_CanBeThrownAndCaught()
    {
        // Arrange
        const string message = "Test error message";

        // Act & Assert
        CorrespondenceServiceException? exception = null;
        try
        {
            throw new CorrespondenceServiceException(message);
        }
        catch (CorrespondenceServiceException ex)
        {
            exception = ex;
        }

        exception.Should().NotBeNull();
        exception!.Message.Should().Be(message);
    }

    [Fact]
    public void Exception_CanBeThrownWithInnerException()
    {
        // Arrange
        const string message = "Test error message";
        var innerException = new ArgumentException("Inner error");

        // Act & Assert
        CorrespondenceServiceException? exception = null;
        try
        {
            throw new CorrespondenceServiceException(message, innerException);
        }
        catch (CorrespondenceServiceException ex)
        {
            exception = ex;
        }

        exception.Should().NotBeNull();
        exception!.Message.Should().Be(message);
        exception.InnerException.Should().BeSameAs(innerException);
    }
}

using Altinn.Dd.Correspondence.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Altinn.Dd.Correspondence.Tests.UnitTests;

/// <summary>
/// Unit tests for ReceiptExternal
/// </summary>
public class ReceiptExternalTests
{
    [Fact]
    public void CreateSuccess_WithDefaultMessage_CreatesSuccessReceipt()
    {
        // Act
        var receipt = ReceiptExternal.CreateSuccess();

        // Assert
        receipt.ReceiptStatusCode.Should().Be(ReceiptStatusEnum.OK);
        receipt.ReceiptText.Should().Be("Correspondence sent successfully");
    }

    [Fact]
    public void CreateSuccess_WithCustomMessage_CreatesSuccessReceiptWithMessage()
    {
        // Arrange
        const string customMessage = "Custom success message";

        // Act
        var receipt = ReceiptExternal.CreateSuccess(customMessage);

        // Assert
        receipt.ReceiptStatusCode.Should().Be(ReceiptStatusEnum.OK);
        receipt.ReceiptText.Should().Be(customMessage);
    }

    [Fact]
    public void CreateSuccess_WithNullMessage_CreatesSuccessReceiptWithNullMessage()
    {
        // Act
        var receipt = ReceiptExternal.CreateSuccess(null!);

        // Assert
        receipt.ReceiptStatusCode.Should().Be(ReceiptStatusEnum.OK);
        receipt.ReceiptText.Should().BeNull();
    }

    [Fact]
    public void CreateSuccess_WithEmptyMessage_CreatesSuccessReceiptWithEmptyMessage()
    {
        // Arrange
        const string emptyMessage = "";

        // Act
        var receipt = ReceiptExternal.CreateSuccess(emptyMessage);

        // Assert
        receipt.ReceiptStatusCode.Should().Be(ReceiptStatusEnum.OK);
        receipt.ReceiptText.Should().Be(emptyMessage);
    }

    [Fact]
    public void CreateError_WithMessage_CreatesErrorReceipt()
    {
        // Arrange
        const string errorMessage = "Error occurred";

        // Act
        var receipt = ReceiptExternal.CreateError(errorMessage);

        // Assert
        receipt.ReceiptStatusCode.Should().Be(ReceiptStatusEnum.Error);
        receipt.ReceiptText.Should().Be(errorMessage);
    }

    [Fact]
    public void CreateError_WithNullMessage_CreatesErrorReceiptWithNullMessage()
    {
        // Act
        var receipt = ReceiptExternal.CreateError(null!);

        // Assert
        receipt.ReceiptStatusCode.Should().Be(ReceiptStatusEnum.Error);
        receipt.ReceiptText.Should().BeNull();
    }

    [Fact]
    public void CreateError_WithEmptyMessage_CreatesErrorReceiptWithEmptyMessage()
    {
        // Arrange
        const string emptyMessage = "";

        // Act
        var receipt = ReceiptExternal.CreateError(emptyMessage);

        // Assert
        receipt.ReceiptStatusCode.Should().Be(ReceiptStatusEnum.Error);
        receipt.ReceiptText.Should().Be(emptyMessage);
    }

    [Fact]
    public void ReceiptExternal_CanBeCreatedManually()
    {
        // Arrange & Act
        var receipt = new ReceiptExternal
        {
            ReceiptStatusCode = ReceiptStatusEnum.OK,
            ReceiptText = "Manual receipt"
        };

        // Assert
        receipt.ReceiptStatusCode.Should().Be(ReceiptStatusEnum.OK);
        receipt.ReceiptText.Should().Be("Manual receipt");
    }

    [Fact]
    public void ReceiptExternal_DefaultValues_AreCorrect()
    {
        // Act
        var receipt = new ReceiptExternal();

        // Assert
        receipt.ReceiptStatusCode.Should().Be(default(ReceiptStatusEnum));
        receipt.ReceiptText.Should().Be(string.Empty);
    }

    [Fact]
    public void ReceiptStatusEnum_Values_AreCorrect()
    {
        // Assert
        Enum.GetValues<ReceiptStatusEnum>().Should().Contain(ReceiptStatusEnum.OK);
        Enum.GetValues<ReceiptStatusEnum>().Should().Contain(ReceiptStatusEnum.Error);
    }

    [Fact]
    public void ReceiptStatusEnum_OK_HasCorrectValue()
    {
        // Assert
        ((int)ReceiptStatusEnum.OK).Should().Be(0);
    }

    [Fact]
    public void ReceiptStatusEnum_Error_HasCorrectValue()
    {
        // Assert
        ((int)ReceiptStatusEnum.Error).Should().Be(1);
    }

    [Fact]
    public void ReceiptExternal_CanBeSerialized()
    {
        // Arrange
        var receipt = ReceiptExternal.CreateSuccess("Test message");

        // Act
        var json = System.Text.Json.JsonSerializer.Serialize(receipt);

        // Assert
        json.Should().Contain("0"); // ReceiptStatusEnum.OK = 0
        json.Should().Contain("Test message");
    }

    [Fact]
    public void ReceiptExternal_CanBeDeserialized()
    {
        // Arrange
        var originalReceipt = ReceiptExternal.CreateSuccess("Test message");
        var json = System.Text.Json.JsonSerializer.Serialize(originalReceipt);

        // Act
        var deserializedReceipt = System.Text.Json.JsonSerializer.Deserialize<ReceiptExternal>(json);

        // Assert
        deserializedReceipt.Should().NotBeNull();
        deserializedReceipt!.ReceiptStatusCode.Should().Be(originalReceipt.ReceiptStatusCode);
        deserializedReceipt.ReceiptText.Should().Be(originalReceipt.ReceiptText);
    }
}

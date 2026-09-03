using FluentAssertions;
using GymManagement.Application.DTOs.Client;
using GymManagement.Application.Validators;
using GymManagement.Domain.Enums;
using Xunit;

namespace GymManagement.UnitTests.Validators;

public class CreateClientRequestValidatorTests
{
    private readonly CreateClientRequestValidator _validator;

    public CreateClientRequestValidatorTests()
    {
        _validator = new CreateClientRequestValidator();
    }

    [Fact]
    public void Validate_ValidRequest_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var request = new CreateClientRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com",
            PhoneNumber = "1234567890",
            DateOfBirth = DateTime.Today.AddYears(-25),
            Gender = Gender.Male,
            Address = "123 Main St",
            PackageId = 1,
            MembershipStartDate = DateTime.Today
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData(" ")]
    // string? because one case deliberately passes null - the validator has to reject it,
    // and a non-nullable parameter makes the analyzer object to the test that proves it.
    public void Validate_EmptyFirstName_ShouldHaveValidationError(string? firstName)
    {
        // Arrange
        var request = new CreateClientRequest
        {
            FirstName = firstName!,
            LastName = "Doe",
            PhoneNumber = "1234567890"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateClientRequest.FirstName));
    }

    [Fact]
    public void Validate_InvalidEmail_ShouldHaveValidationError()
    {
        // Arrange
        var request = new CreateClientRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "invalid-email",
            PhoneNumber = "1234567890"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateClientRequest.Email));
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Invalid email format"));
    }

    [Fact]
    public void Validate_FutureDateOfBirth_ShouldHaveValidationError()
    {
        // Arrange
        var request = new CreateClientRequest
        {
            FirstName = "John",
            LastName = "Doe",
            PhoneNumber = "1234567890",
            DateOfBirth = DateTime.Today.AddDays(1)
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateClientRequest.DateOfBirth));
    }

    [Fact]
    public void Validate_FirstNameWithNumbers_ShouldHaveValidationError()
    {
        // Arrange
        var request = new CreateClientRequest
        {
            FirstName = "John123",
            LastName = "Doe",
            PhoneNumber = "1234567890"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateClientRequest.FirstName));
    }

    [Theory]
    [InlineData("John-Paul")]
    [InlineData("O'Brien")]
    [InlineData("Mary Jane")]
    public void Validate_ValidFirstNameFormats_ShouldNotHaveValidationError(string firstName)
    {
        // Arrange
        var request = new CreateClientRequest
        {
            FirstName = firstName,
            LastName = "Doe",
            PhoneNumber = "1234567890"
        };

        // Act
        var result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}

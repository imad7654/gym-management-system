using FluentValidation;
using GymManagement.Application.DTOs.User;
using GymManagement.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace GymManagement.Application.Validators;

/// <summary>
/// The password rules live in one place so an administrator setting someone's password
/// cannot quietly hold it to a lower standard than the person choosing their own.
/// </summary>
internal static class PasswordRules
{
    private const int FallbackMinimumLength = 12;

    public static int MinimumLength(IConfiguration configuration) =>
        configuration.GetValue("Validation:MinPasswordLength", FallbackMinimumLength);
}

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator(IConfiguration configuration)
    {
        var minimumLength = PasswordRules.MinimumLength(configuration);

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("A first name is required")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("A last name is required")
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("An email is required - it is what they sign in with")
            .EmailAddress().WithMessage("That does not look like an email address")
            .MaximumLength(256);

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("A password is required")
            .MinimumLength(minimumLength)
                .WithMessage($"The password must be at least {minimumLength} characters");

        RuleFor(x => x.Role)
            .Must(BeAKnownRole)
                .WithMessage("Choose either administrator or reception");
    }

    /// <summary>
    /// Only the two roles the accounts screen hands out. Trainer and Client exist in the
    /// database but are not this screen's to give - a member account is claimed by phone
    /// number, not granted here.
    /// </summary>
    internal static bool BeAKnownRole(string? role) =>
        string.Equals(role, Roles.Admin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, Roles.Staff, StringComparison.OrdinalIgnoreCase);
}

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("A first name is required")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("A last name is required")
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("An email is required - it is what they sign in with")
            .EmailAddress().WithMessage("That does not look like an email address")
            .MaximumLength(256);

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(30);

        RuleFor(x => x.Role)
            .Must(CreateUserRequestValidator.BeAKnownRole)
                .WithMessage("Choose either administrator or reception");
    }
}

/// <summary>
/// No "current password" rule here, unlike <see cref="ChangePasswordRequestValidator"/>:
/// an administrator resetting a forgotten password does not know the old one, which is the
/// entire reason the endpoint exists.
/// </summary>
public class ResetUserPasswordRequestValidator : AbstractValidator<ResetUserPasswordRequest>
{
    public ResetUserPasswordRequestValidator(IConfiguration configuration)
    {
        var minimumLength = PasswordRules.MinimumLength(configuration);

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("A new password is required")
            .MinimumLength(minimumLength)
                .WithMessage($"The password must be at least {minimumLength} characters");
    }
}

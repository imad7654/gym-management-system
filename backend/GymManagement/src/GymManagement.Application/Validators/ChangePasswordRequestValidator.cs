using FluentValidation;
using GymManagement.Application.DTOs.Auth;
using Microsoft.Extensions.Configuration;

namespace GymManagement.Application.Validators;

/// <summary>
/// Enforces the password policy when a password is being set. The minimum comes from
/// Validation:MinPasswordLength so there is one place to change it.
///
/// Note that no equivalent length rule belongs on login: checking a submitted password
/// against a policy that may have been raised since it was set only locks out the people
/// whose passwords most need changing, without making anything safer.
/// </summary>
public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    private const int FallbackMinimumLength = 12;

    public ChangePasswordRequestValidator(IConfiguration configuration)
    {
        var minimumLength = configuration.GetValue("Validation:MinPasswordLength", FallbackMinimumLength);

        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Your current password is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("A new password is required")
            .MinimumLength(minimumLength)
                .WithMessage($"Your new password must be at least {minimumLength} characters")
            .NotEqual(x => x.CurrentPassword)
                .WithMessage("Your new password must be different from your current one");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("The two passwords do not match");
    }
}

using FluentValidation;
using GymManagement.Application.DTOs.Member;
using GymManagement.Domain.Common;
using Microsoft.Extensions.Configuration;

namespace GymManagement.Application.Validators;

/// <summary>
/// Sign-up checks that can be made without touching the database. Whether the details
/// actually match a member is <c>MemberAccountService</c>'s job, and its answer is
/// deliberately vague about which half was wrong.
/// </summary>
public class RegisterMemberRequestValidator : AbstractValidator<RegisterMemberRequest>
{
    public RegisterMemberRequestValidator(IConfiguration configuration)
    {
        var minimumLength = PasswordRules.MinimumLength(configuration);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Your phone number is required - it is how the gym finds you")
            .MaximumLength(30)
            // Rejects "12345" here rather than reporting it as a failed match, which would
            // read as "the gym does not have you" when the real problem is a typo.
            .Must(phone => PhoneNumberKey.Normalize(phone) != null)
                .WithMessage("That does not look like a phone number");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Your surname is required")
            .MaximumLength(100);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("An email is required - it is what you will sign in with")
            .EmailAddress().WithMessage("That does not look like an email address")
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("A password is required")
            .MinimumLength(minimumLength)
                .WithMessage($"The password must be at least {minimumLength} characters");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("The two passwords do not match");
    }
}

/// <summary>
/// An administrator setting a member's password. Held to the same length rule as a member
/// choosing their own, so a reset cannot quietly be weaker.
/// </summary>
public class ResetMemberPasswordRequestValidator : AbstractValidator<ResetMemberPasswordRequest>
{
    public ResetMemberPasswordRequestValidator(IConfiguration configuration)
    {
        var minimumLength = PasswordRules.MinimumLength(configuration);

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("A new password is required")
            .MinimumLength(minimumLength)
                .WithMessage($"The password must be at least {minimumLength} characters");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("The two passwords do not match");
    }
}

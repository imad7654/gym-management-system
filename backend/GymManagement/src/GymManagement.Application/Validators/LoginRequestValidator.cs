using FluentValidation;
using GymManagement.Application.DTOs.Auth;

namespace GymManagement.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        // Only that something was typed. Applying a length policy here would reject the
        // password of anyone whose account predates a policy change, and tell an attacker
        // which lengths are worth trying.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");
    }
}

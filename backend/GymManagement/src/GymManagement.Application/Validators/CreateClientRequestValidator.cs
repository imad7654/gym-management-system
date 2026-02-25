using FluentValidation;
using GymManagement.Application.DTOs.Client;

namespace GymManagement.Application.Validators;

public class CreateClientRequestValidator : AbstractValidator<CreateClientRequest>
{
    public CreateClientRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters")
            .Matches(@"^[a-zA-Z\s'-]+$").WithMessage("First name can only contain letters, spaces, hyphens, and apostrophes");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters")
            .Matches(@"^[a-zA-Z\s'-]+$").WithMessage("Last name can only contain letters, spaces, hyphens, and apostrophes");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone number is required")
            .MaximumLength(20).WithMessage("Phone number must not exceed 20 characters")
            .Matches(@"^[\d\s\-\+\(\)]+$").WithMessage("Invalid phone number format");

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateTime.Today).WithMessage("Date of birth must be in the past")
            .GreaterThan(DateTime.Today.AddYears(-120)).WithMessage("Date of birth must be within the last 120 years")
            .When(x => x.DateOfBirth.HasValue);

        RuleFor(x => x.Address)
            .MaximumLength(500).WithMessage("Address must not exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Address));

        RuleFor(x => x.EmergencyContact)
            .MaximumLength(100).WithMessage("Emergency contact name must not exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.EmergencyContact));

        RuleFor(x => x.EmergencyPhone)
            .MaximumLength(20).WithMessage("Emergency phone must not exceed 20 characters")
            .Matches(@"^[\d\s\-\+\(\)]+$").WithMessage("Invalid emergency phone number format")
            .When(x => !string.IsNullOrEmpty(x.EmergencyPhone));

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes must not exceed 1000 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));

        RuleFor(x => x.MembershipStartDate)
            .LessThanOrEqualTo(DateTime.Today.AddMonths(1)).WithMessage("Membership start date cannot be more than 1 month in the future")
            .When(x => x.MembershipStartDate.HasValue && x.PackageId.HasValue);

        RuleFor(x => x.PackageId)
            .GreaterThan(0).WithMessage("Invalid package ID")
            .When(x => x.PackageId.HasValue);
    }
}

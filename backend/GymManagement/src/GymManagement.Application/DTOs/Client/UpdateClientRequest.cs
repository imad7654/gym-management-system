using System.ComponentModel.DataAnnotations;
using GymManagement.Domain.Enums;

namespace GymManagement.Application.DTOs.Client;

public class UpdateClientRequest
{
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(255)]
    public string? Email { get; set; }

    [Required]
    [Phone]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    public DateTime? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? EmergencyContact { get; set; }

    [Phone]
    [MaxLength(20)]
    public string? EmergencyPhone { get; set; }

    public string? Notes { get; set; }

    // Membership Info
    public int? PackageId { get; set; }
    public DateTime? MembershipStartDate { get; set; }
    public DateTime? MembershipEndDate { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
}

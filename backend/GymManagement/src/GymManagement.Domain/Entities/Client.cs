using GymManagement.Domain.Common;
using GymManagement.Domain.Enums;

namespace GymManagement.Domain.Entities;

public class Client : AuditableEntity, ISoftDeletable
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public string? Address { get; set; }
    public string? EmergencyContact { get; set; }
    public string? EmergencyPhone { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? Notes { get; set; }

    // Membership Info
    public int? CurrentPackageId { get; set; }
    public DateTime? MembershipStartDate { get; set; }
    public DateTime? MembershipEndDate { get; set; }
    public MembershipStatus MembershipStatus { get; set; } = MembershipStatus.Pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    // Soft Delete
    public bool IsActive { get; set; } = true;
    public DateTime? DeletedAt { get; set; }

    public string FullName => $"{FirstName} {LastName}";

    // Navigation properties
    public virtual Package? CurrentPackage { get; set; }
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<PaymentHistory> PaymentHistories { get; set; } = new List<PaymentHistory>();

    // Business logic methods
    public void UpdateMembershipStatus()
    {
        if (!MembershipStartDate.HasValue || !MembershipEndDate.HasValue)
        {
            MembershipStatus = MembershipStatus.Pending;
            return;
        }

        var today = DateTime.UtcNow.Date;
        if (today < MembershipStartDate.Value.Date)
        {
            MembershipStatus = MembershipStatus.Pending;
        }
        else if (today > MembershipEndDate.Value.Date)
        {
            MembershipStatus = MembershipStatus.Expired;
        }
        else
        {
            MembershipStatus = MembershipStatus.Active;
        }
    }

    public void SoftDelete()
    {
        IsActive = false;
        DeletedAt = DateTime.UtcNow;
        MembershipStatus = MembershipStatus.Suspended;
    }

    public void Restore()
    {
        IsActive = true;
        DeletedAt = null;
        UpdateMembershipStatus();
    }
}

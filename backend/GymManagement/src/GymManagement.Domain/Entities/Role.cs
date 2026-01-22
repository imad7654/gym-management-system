using GymManagement.Domain.Common;

namespace GymManagement.Domain.Entities;

public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Navigation properties
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Client = "Client";
    public const string Trainer = "Trainer";
    public const string Staff = "Staff";
}

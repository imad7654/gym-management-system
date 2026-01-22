using GymManagement.Domain.Common;

namespace GymManagement.Domain.Entities;

public class Package : BaseEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationDays { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; } = 0;
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public virtual ICollection<Client> Clients { get; set; } = new List<Client>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

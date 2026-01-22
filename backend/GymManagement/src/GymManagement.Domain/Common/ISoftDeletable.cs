namespace GymManagement.Domain.Common;

public interface ISoftDeletable
{
    bool IsActive { get; set; }
    DateTime? DeletedAt { get; set; }
}

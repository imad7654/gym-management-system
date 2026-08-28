namespace GymManagement.Domain.Enums;

/// <summary>
/// What somebody did. Stored as a string, so the list can grow without a migration.
/// </summary>
public enum AuditAction
{
    Created,
    Updated,

    /// <summary>Soft-deleted. Nothing in this system is ever really removed.</summary>
    Deleted,

    Restored,

    /// <summary>A payment was cancelled by a reversal row pointing back at it.</summary>
    Reversed,

    /// <summary>Members brought in from the owner's own spreadsheet, in one go.</summary>
    Imported
}

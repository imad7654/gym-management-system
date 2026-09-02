using GymManagement.Domain.Common;

namespace GymManagement.Domain.Entities;

/// <summary>
/// One outstanding "I forgot my password" request.
///
/// The token itself is never stored. Only a SHA-256 of it is kept, for the same reason a
/// password is hashed: anyone who got a copy of this table would otherwise hold a working
/// reset link for every account with a request open, and a reset link is a complete
/// takeover of that account.
///
/// Used by administrators and members alike, since both are <see cref="User"/> rows.
/// </summary>
public class PasswordResetToken : BaseEntity
{
    public int UserId { get; set; }

    /// <summary>
    /// SHA-256 of the token that was emailed, hex encoded. Looking a token up means hashing
    /// what arrived and searching for that - the original cannot be recovered from here.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When it was spent. A reset link has to be single use: an email sits in an inbox
    /// indefinitely, and one that still worked next month would be a permanent back door.
    /// </summary>
    public DateTime? UsedAt { get; set; }

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAt;
    public bool IsUsed => UsedAt != null;

    /// <summary>Whether this token can still be spent.</summary>
    public bool IsUsable(DateTime utcNow) => !IsUsed && !IsExpired(utcNow);

    // Navigation property
    public virtual User User { get; set; } = null!;
}

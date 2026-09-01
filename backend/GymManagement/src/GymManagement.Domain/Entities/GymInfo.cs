using GymManagement.Domain.Common;

namespace GymManagement.Domain.Entities;

public class GymInfo : BaseEntity
{
    public string GymName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }

    // Social Media
    public string? FacebookUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? TwitterUrl { get; set; }

    /// <summary>
    /// Free text - "Mon-Fri: 6am - 10pm" and so on. Not JSON, despite the column having
    /// once been declared that way; nothing parses it and the Settings screen edits it as
    /// an ordinary multi-line box.
    /// </summary>
    public string? OperatingHours { get; set; }

    // Homepage Content
    public string? HeroTitle { get; set; }
    public string? HeroSubtitle { get; set; }
    public string? HeroImageUrl { get; set; }
    public string? AboutTitle { get; set; }
    public string? AboutContent { get; set; }

    // SEO
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    public int? UpdatedBy { get; set; }
}

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

    // Operating Hours (stored as JSON)
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

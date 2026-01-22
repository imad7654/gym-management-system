namespace GymManagement.Application.DTOs.GymInfo;

public class GymInfoDto
{
    public int Id { get; set; }
    public string GymName { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }

    public string? FacebookUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? TwitterUrl { get; set; }

    public string? OperatingHours { get; set; }

    public string? HeroTitle { get; set; }
    public string? HeroSubtitle { get; set; }
    public string? HeroImageUrl { get; set; }
    public string? AboutTitle { get; set; }
    public string? AboutContent { get; set; }

    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
}

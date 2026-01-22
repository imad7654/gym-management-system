using System.ComponentModel.DataAnnotations;

namespace GymManagement.Application.DTOs.GymInfo;

public class UpdateGymInfoRequest
{
    [Required]
    [MaxLength(200)]
    public string GymName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    public string? Description { get; set; }
    public string? Address { get; set; }

    [Phone]
    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [EmailAddress]
    [MaxLength(255)]
    public string? Email { get; set; }

    [MaxLength(500)]
    public string? FacebookUrl { get; set; }

    [MaxLength(500)]
    public string? InstagramUrl { get; set; }

    [MaxLength(500)]
    public string? TwitterUrl { get; set; }

    public string? OperatingHours { get; set; }

    [MaxLength(255)]
    public string? HeroTitle { get; set; }

    public string? HeroSubtitle { get; set; }

    [MaxLength(500)]
    public string? HeroImageUrl { get; set; }

    [MaxLength(255)]
    public string? AboutTitle { get; set; }

    public string? AboutContent { get; set; }

    [MaxLength(255)]
    public string? MetaTitle { get; set; }

    public string? MetaDescription { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace GymManagement.Application.DTOs.Package;

public class UpdatePackageRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    [Range(1, 365 * 5)]
    public int DurationDays { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

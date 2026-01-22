using System.ComponentModel.DataAnnotations;

namespace GymManagement.Application.DTOs.Auth;

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

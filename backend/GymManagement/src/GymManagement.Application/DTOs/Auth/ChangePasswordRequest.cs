namespace GymManagement.Application.DTOs.Auth;

// Rules live in ChangePasswordRequestValidator so the password policy has one home
// and can read its minimum length from configuration.
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

namespace GymManagement.Application.DTOs.Auth;

/// <summary>Starting a reset: the address the link should go to.</summary>
public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>Finishing a reset, with the token out of the emailed link.</summary>
public class ResetPasswordWithTokenRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

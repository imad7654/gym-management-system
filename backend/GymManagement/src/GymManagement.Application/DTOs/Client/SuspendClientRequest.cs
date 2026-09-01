namespace GymManagement.Application.DTOs.Client;

/// <summary>
/// Why a membership was frozen. Optional, and only ever written to the audit trail - the
/// question months later is always "who froze this and why", and a freeze with no reason
/// attached is the one nobody can explain.
/// </summary>
public class SuspendClientRequest
{
    public string? Reason { get; set; }
}

namespace GymManagement.Application.DTOs.User;

/// <summary>
/// A person who signs in to run the gym - the owner, and anyone they trust with the admin
/// panel. Not a member: members get their own accounts later, matched to an existing record
/// by phone number, and they never appear in this list.
/// </summary>
public class UserListDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// True for the person reading the list. The screen uses it to stop them switching off
    /// their own account, which is the quickest way to be locked out.
    /// </summary>
    public bool IsYou { get; set; }

    /// <summary>
    /// True when this is the only administrator left who can still sign in. The screen greys
    /// out removing them; the server refuses it outright.
    /// </summary>
    public bool IsLastAdmin { get; set; }
}

public class CreateUserRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Chosen by the owner and handed over in person. There is no email anywhere in this
    /// system, so a generated password could not be delivered.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// "Admin" or "Staff". Staff is reception: they run the desk but cannot reverse a
    /// payment, read the audit trail, see revenue history or change what anything costs.
    /// Defaults to Admin, which is what every account made before this field existed was.
    /// </summary>
    public string Role { get; set; } = "Admin";
}

public class UpdateUserRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// "Admin" or "Staff". Changing the last administrator down to Staff is refused for
    /// the same reason switching them off is: it would leave nobody able to change it back.
    /// </summary>
    public string Role { get; set; } = "Admin";
}

/// <summary>
/// An administrator setting someone else's password, for the case the gym will actually hit:
/// a person forgets theirs and asks at the desk.
///
/// The current password is deliberately not required - the whole point is that the person
/// asking does not know it. That is safe only because this is admin-only, and it is why the
/// audit trail records every use.
/// </summary>
public class ResetUserPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

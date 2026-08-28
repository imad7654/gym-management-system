namespace GymManagement.Application.Interfaces;

/// <summary>
/// The column headings the import understands, and the spellings it accepts for each.
///
/// The owner's file was written for the owner, not for this system. Insisting on exact
/// headings would mean the first thing they do with their real records is retype the top
/// row - and every alias here costs one line, while a rejected file costs a phone call.
/// Headings arrive already normalized to lowercase letters and digits, so "Phone Number",
/// "phone_number" and "PhoneNumber" are one entry, not three.
/// </summary>
public static class MemberImportColumns
{
    /// <summary>Whole name in one column. Split into first and last on the first space.</summary>
    public static readonly string[] Name =
        { "name", "fullname", "membername", "clientname", "client", "member" };

    /// <summary>Used only when the file splits the name across two columns.</summary>
    public static readonly string[] FirstName = { "firstname", "first", "givenname" };

    public static readonly string[] LastName = { "lastname", "last", "surname", "familyname" };

    public static readonly string[] Phone =
        { "phone", "phonenumber", "mobile", "mobilenumber", "mobileno", "tel", "telephone", "contact", "contactnumber" };

    public static readonly string[] Package =
        { "package", "packagename", "plan", "membership", "membershiptype", "membershipplan", "subscription" };

    public static readonly string[] EndDate =
        { "enddate", "membershipenddate", "expiry", "expirydate", "expires", "expireson", "expiration",
          "expirationdate", "validuntil", "validtill", "renewaldate", "duedate", "finishdate" };

    /// <summary>Optional. When absent the start is worked back from the end date.</summary>
    public static readonly string[] StartDate =
        { "startdate", "membershipstartdate", "joindate", "datejoined", "joined", "since", "begindate" };

    public static readonly string[] Email = { "email", "emailaddress", "mail", "eemail" };

    public static readonly string[] Notes = { "notes", "note", "comment", "comments", "remarks" };

    /// <summary>
    /// Every heading above. Used to tell "this file uses different words for the columns"
    /// apart from "this file is not a member list", which need different messages.
    /// </summary>
    public static readonly IReadOnlySet<string> AllRecognised =
        new HashSet<string>(
            Name.Concat(FirstName).Concat(LastName).Concat(Phone).Concat(Package)
                .Concat(EndDate).Concat(StartDate).Concat(Email).Concat(Notes),
            StringComparer.Ordinal);

    /// <summary>
    /// Returns the first of <paramref name="aliases"/> present in the row, or null.
    /// </summary>
    public static string? Find(IReadOnlyDictionary<string, string> values, string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (values.TryGetValue(alias, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}

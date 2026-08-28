using System.Text;

namespace GymManagement.Domain.Common;

/// <summary>
/// Reduces a phone number to the digits that identify the person, so two spellings of the
/// same Lebanese number compare equal.
///
/// The gym's own records write one number four ways - "03 123 456", "03123456",
/// "+961 3 123 456", "0096131 23456" - and every one of them is the same member. Matching
/// on the raw text would let an import create four records for one person, and would make
/// the Phase 3 "sign up by phone" match fail for anyone who typed their number differently
/// from how reception did.
///
/// The number is only ever *stored* as the owner typed it. This key exists for comparison.
/// </summary>
public static class PhoneNumberKey
{
    /// <summary>Lebanon's country calling code, stripped so local and international forms match.</summary>
    private const string LebanonCountryCode = "961";

    /// <summary>
    /// Shortest run of digits still worth treating as a phone number. Lebanese mobiles are
    /// 7 or 8 digits after the leading zero; anything shorter is a typo, not a number.
    /// </summary>
    public const int MinimumDigits = 6;

    /// <summary>
    /// Returns the comparison key, or null when the input holds no usable number.
    /// Callers should treat null as "not a phone number" rather than as an empty match -
    /// otherwise every row with a missing number would look like a duplicate of every other.
    /// </summary>
    public static string? Normalize(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return null;

        var digits = new StringBuilder(phoneNumber.Length);
        foreach (var c in phoneNumber)
        {
            if (char.IsDigit(c)) digits.Append(c);
        }

        var value = digits.ToString();

        // "00" is the international dialling prefix; "+" has already been dropped as a
        // non-digit, so both spellings of the international form arrive here identically.
        if (value.StartsWith("00", StringComparison.Ordinal)) value = value[2..];

        // Only strip the country code when enough digits remain to still be a number.
        // Guarding on the length stops a local number that merely happens to begin 961
        // from being truncated into something that collides with a different member.
        if (value.StartsWith(LebanonCountryCode, StringComparison.Ordinal)
            && value.Length - LebanonCountryCode.Length >= MinimumDigits)
        {
            value = value[LebanonCountryCode.Length..];
        }

        value = value.TrimStart('0');

        return value.Length >= MinimumDigits ? value : null;
    }
}

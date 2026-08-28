namespace GymManagement.Application.Interfaces;

/// <summary>One row of the uploaded file, still as text.</summary>
/// <param name="RowNumber">Row number as the owner's spreadsheet shows it, header included.</param>
/// <param name="Values">Cell values keyed by normalized column name.</param>
public record MemberImportRawRow(int RowNumber, IReadOnlyDictionary<string, string> Values);

/// <summary>
/// Turns an uploaded .csv or .xlsx into rows of text, and nothing more.
///
/// Kept behind an interface because the spreadsheet library is a file-format detail that
/// belongs in Infrastructure - the import rules themselves are business logic and must stay
/// testable without writing a real workbook to disk.
/// </summary>
public interface IMemberImportFileReader
{
    /// <summary>Extensions this reader accepts, lowercase and dotted.</summary>
    IReadOnlyCollection<string> SupportedExtensions { get; }

    /// <summary>
    /// Reads the sheet. Column headers are normalized to lowercase letters and digits so
    /// "Phone Number", "phone_number" and "PhoneNumber" all arrive as "phonenumber".
    /// Real date cells are returned as yyyy-MM-dd, which removes the day/month ambiguity
    /// before it can reach the parser.
    /// </summary>
    /// <exception cref="Exceptions.BusinessException">The file could not be opened or has no readable rows.</exception>
    IReadOnlyList<MemberImportRawRow> Read(Stream stream, string fileName);
}

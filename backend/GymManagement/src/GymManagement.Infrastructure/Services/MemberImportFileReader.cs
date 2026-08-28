using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using GymManagement.Application.Exceptions;
using GymManagement.Application.Interfaces;

namespace GymManagement.Infrastructure.Services;

/// <summary>
/// Reads the owner's member list out of a .csv or .xlsx file.
///
/// Both formats are accepted because the owner's list is whatever it already is. Telling a
/// gym owner to convert their workbook before they can go live is a step where an import
/// gets abandoned - and "save as CSV" is also the step where Excel silently rewrites dates
/// into whatever the machine's locale prefers.
/// </summary>
public class MemberImportFileReader : IMemberImportFileReader
{
    private static readonly string[] Extensions = { ".csv", ".xlsx" };

    public IReadOnlyCollection<string> SupportedExtensions => Extensions;

    public IReadOnlyList<MemberImportRawRow> Read(Stream stream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".csv" => ReadCsv(stream),
            ".xlsx" => ReadXlsx(stream),
            _ => throw new BusinessException(
                $"'{fileName}' is not a spreadsheet this can read. Save the file as .xlsx or .csv and upload it again.")
        };
    }

    // ---------------------------------------------------------------- xlsx

    private static IReadOnlyList<MemberImportRawRow> ReadXlsx(Stream stream)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex)
        {
            // Almost always an .xls saved under an .xlsx name, or a file that never finished
            // downloading. Either way the owner needs to know it is the file, not their data.
            throw new BusinessException(
                "That file could not be opened as a spreadsheet. If it came from an older Excel, "
                + "open it and use Save As to make a .xlsx, then upload that.", ex);
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new BusinessException("The workbook has no sheets in it.");

            var used = sheet.RangeUsed();
            if (used == null) throw new BusinessException("The first sheet of that workbook is empty.");

            var rows = used.RowsUsed().ToList();
            if (rows.Count == 0) throw new BusinessException("The first sheet of that workbook is empty.");

            var headers = rows[0].Cells()
                .Select(c => new { Column = c.Address.ColumnNumber, Name = NormalizeHeader(c.GetFormattedString()) })
                .Where(h => h.Name.Length > 0)
                .ToList();

            RequireHeaders(headers.Select(h => h.Name));

            var result = new List<MemberImportRawRow>();

            foreach (var row in rows.Skip(1))
            {
                var values = new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (var header in headers)
                {
                    var text = CellText(row.Cell(header.Column));
                    if (text.Length > 0) values[header.Name] = text;
                }

                // A blank line in the middle of a list is formatting, not a member.
                if (values.Count == 0) continue;

                result.Add(new MemberImportRawRow(row.RowNumber(), values));
            }

            return result;
        }
    }

    /// <summary>
    /// Reads a cell as text, converting real dates to yyyy-MM-dd.
    ///
    /// A date cell holds a number; what the owner sees is a display format. Reading the
    /// formatted string would hand the parser "03/04/2026" and leave it guessing whether
    /// that is March or April - a guess that would move a membership by a month. Taking the
    /// underlying date value settles it before the ambiguity can arise.
    /// </summary>
    private static string CellText(IXLCell cell)
    {
        if (cell.IsEmpty()) return string.Empty;

        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var date))
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return cell.GetFormattedString().Trim();
    }

    // ----------------------------------------------------------------- csv

    private static IReadOnlyList<MemberImportRawRow> ReadCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var records = new List<List<string>>();
        while (ParseCsvRecord(reader) is { } record)
        {
            records.Add(record);
        }

        if (records.Count == 0) throw new BusinessException("That file is empty.");

        var headers = records[0].Select(NormalizeHeader).ToList();
        RequireHeaders(headers.Where(h => h.Length > 0));

        var result = new List<MemberImportRawRow>();

        for (var i = 1; i < records.Count; i++)
        {
            var cells = records[i];
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var c = 0; c < headers.Count && c < cells.Count; c++)
            {
                if (headers[c].Length == 0) continue;
                var text = cells[c].Trim();
                if (text.Length > 0) values[headers[c]] = text;
            }

            if (values.Count == 0) continue;

            // +1 so the number matches what the spreadsheet shows, where the header is row 1.
            result.Add(new MemberImportRawRow(i + 1, values));
        }

        return result;
    }

    /// <summary>
    /// Reads one CSV record, honouring RFC 4180 quoting. Returns null at end of file.
    ///
    /// A record is not always a line: a quoted field may contain newlines, which is exactly
    /// what a pasted address does. Splitting the file on newlines would tear those rows in
    /// half and report the halves as two broken members.
    /// </summary>
    private static List<string>? ParseCsvRecord(TextReader reader)
    {
        if (reader.Peek() < 0) return null;

        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        while (true)
        {
            var read = reader.Read();

            if (read < 0)
            {
                fields.Add(field.ToString());
                return fields;
            }

            var c = (char)read;

            if (inQuotes)
            {
                if (c == '"')
                {
                    // A doubled quote inside a quoted field is a literal quote character.
                    if (reader.Peek() == '"')
                    {
                        field.Append((char)reader.Read());
                        continue;
                    }

                    inQuotes = false;
                    continue;
                }

                field.Append(c);
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    continue;

                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    continue;

                case '\r':
                    if (reader.Peek() == '\n') reader.Read();
                    fields.Add(field.ToString());
                    return fields;

                case '\n':
                    fields.Add(field.ToString());
                    return fields;

                default:
                    field.Append(c);
                    continue;
            }
        }
    }

    // -------------------------------------------------------------- shared

    /// <summary>
    /// Strips everything but letters and digits and lowercases the rest, so the owner's own
    /// spelling of a heading - spaces, underscores, capitals, a trailing colon - does not
    /// decide whether their file can be imported.
    /// </summary>
    private static string NormalizeHeader(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return string.Empty;

        var builder = new StringBuilder(header.Length);
        foreach (var c in header)
        {
            if (char.IsLetterOrDigit(c)) builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Fails early when the sheet is not a member list at all, naming the headings that were
    /// actually found. Letting a file with no recognisable columns through would produce one
    /// identical "missing name" error on every row instead of one useful message.
    /// </summary>
    private static void RequireHeaders(IEnumerable<string> headers)
    {
        var found = headers.ToHashSet(StringComparer.Ordinal);
        if (found.Count == 0) throw new BusinessException("The first row of that file has no column headings.");

        if (!found.Overlaps(MemberImportColumns.AllRecognised))
        {
            throw new BusinessException(
                "None of the column headings in that file were recognised. The first row needs headings "
                + "such as Name, Phone, Package and End Date. Found: "
                + string.Join(", ", found.Take(12)) + ".");
        }
    }
}

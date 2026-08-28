using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GymManagement.Application.DTOs.Import;
using GymManagement.Application.Exceptions;
using GymManagement.Application.Interfaces;
using GymManagement.Domain.Common;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GymManagement.Application.Services;

public interface IMemberImportService
{
    /// <summary>Checks the file and reports what would happen. Writes nothing.</summary>
    Task<MemberImportPreviewDto> PreviewAsync(Stream file, string fileName, CancellationToken cancellationToken = default);

    /// <summary>Re-checks the file and creates the rows that pass, all or nothing.</summary>
    Task<MemberImportResultDto> CommitAsync(
        Stream file,
        string fileName,
        string expectedFileHash,
        bool acknowledgeSkipped,
        int? userId,
        CancellationToken cancellationToken = default);

    /// <summary>A starter file with the right headings and one example row.</summary>
    byte[] BuildTemplateCsv();
}

/// <summary>
/// Brings the gym's existing paper or spreadsheet member list into the system, once.
///
/// Two rules shape the whole thing.
///
/// Nothing is written until the owner has seen what will happen. The file is parsed and
/// checked in full, a report comes back, and only a second call actually creates anyone.
/// A half-finished import into a live member list is far worse than a rejected file: the
/// owner cannot tell which members are real and which are half-typed.
///
/// No payment history is invented. Imported members arrive with the end date they already
/// had and no Payment rows behind it. Fabricating the payments that must have produced
/// those end dates would put money the gym never took into every revenue figure from day
/// one, and there would be no way to tell it back out again afterwards.
/// </summary>
public class MemberImportService : IMemberImportService
{
    /// <summary>
    /// Date spellings accepted from text cells, most specific first.
    ///
    /// Day-before-month, because the gym is in Lebanon and writes 04/03/2026 for the fourth
    /// of March. The preview echoes every parsed date back in an unambiguous form so a file
    /// written the American way is caught by the owner's own eyes rather than silently
    /// moving a hundred memberships by up to eleven months.
    /// </summary>
    private static readonly string[] DateFormats =
    {
        "yyyy-MM-dd", "yyyy/MM/dd", "yyyy-M-d", "yyyy/M/d",
        "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "d.M.yyyy",
        "dd/MM/yy", "d/M/yy", "dd-MM-yy",
        "d MMM yyyy", "dd MMM yyyy", "d MMMM yyyy", "dd MMMM yyyy",
        "MMM d yyyy", "MMMM d yyyy", "MMM d, yyyy", "MMMM d, yyyy"
    };

    /// <summary>
    /// Oldest end date worth believing. Anything earlier is a mistyped year, not a member
    /// whose card ran out during the last century.
    /// </summary>
    private static readonly DateOnly EarliestSensibleDate = new(2000, 1, 1);

    /// <summary>How far ahead an end date may sit before it looks like a typo.</summary>
    private const int MaxYearsAhead = 5;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemberImportFileReader _fileReader;
    private readonly IMembershipClock _clock;

    public MemberImportService(
        IUnitOfWork unitOfWork,
        IMemberImportFileReader fileReader,
        IMembershipClock clock)
    {
        _unitOfWork = unitOfWork;
        _fileReader = fileReader;
        _clock = clock;
    }

    public async Task<MemberImportPreviewDto> PreviewAsync(
        Stream file, string fileName, CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAllBytesAsync(file, cancellationToken);
        var plan = await BuildPlanAsync(bytes, fileName, cancellationToken);

        return new MemberImportPreviewDto
        {
            FileName = fileName,
            FileHash = Hash(bytes),
            TotalRows = plan.Rows.Count,
            ReadyCount = plan.Rows.Count(r => r.Row.Status == MemberImportRowStatus.Ready),
            DuplicateCount = plan.Rows.Count(r => r.Row.Status == MemberImportRowStatus.Duplicate),
            ErrorCount = plan.Rows.Count(r => r.Row.Status == MemberImportRowStatus.Error),
            AvailablePackages = plan.PackageNames,
            Rows = plan.Rows.Select(r => r.Row).ToList()
        };
    }

    public async Task<MemberImportResultDto> CommitAsync(
        Stream file,
        string fileName,
        string expectedFileHash,
        bool acknowledgeSkipped,
        int? userId,
        CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAllBytesAsync(file, cancellationToken);

        // The confirm carries the hash the preview returned. Without it, an owner who fixed
        // their file in another window and re-uploaded would import rows nobody ever looked
        // at - which is the one thing the preview exists to prevent.
        if (!string.Equals(Hash(bytes), expectedFileHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(
                "This file is not the one that was checked. Upload it again to see what has changed "
                + "before importing.");
        }

        // Re-checked rather than trusting the preview's verdict: the preview ran against the
        // member list as it was then, and reception may have added someone in the meantime.
        var plan = await BuildPlanAsync(bytes, fileName, cancellationToken);

        var ready = plan.Rows.Where(r => r.Row.Status == MemberImportRowStatus.Ready).ToList();
        var skipped = plan.Rows.Where(r => r.Row.Status != MemberImportRowStatus.Ready).ToList();

        if (skipped.Count > 0 && !acknowledgeSkipped)
        {
            throw new BusinessException(
                $"{skipped.Count} of {plan.Rows.Count} rows cannot be imported. Fix them in the file and "
                + "upload it again, or confirm that you want to import the rest without them.");
        }

        if (ready.Count == 0)
        {
            throw new BusinessException("There is nothing to import - no row in this file passed the checks.");
        }

        foreach (var candidate in ready)
        {
            // Stamped here rather than in the row builder, which runs for the preview too and
            // must not pretend anything was created. This is the only record of where these
            // members came from, since they arrive with no payment behind them.
            candidate.Client!.CreatedBy = userId;
            await _unitOfWork.Clients.AddAsync(candidate.Client, cancellationToken);
        }

        // One SaveChangesAsync is one database transaction, so the import is all or nothing
        // without an explicit BeginTransaction - which the retrying execution strategy this
        // app configures for MySQL would refuse anyway.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new MemberImportResultDto
        {
            ImportedCount = ready.Count,
            SkippedCount = skipped.Count,
            SkippedRows = skipped.Select(r => r.Row).ToList()
        };
    }

    public byte[] BuildTemplateCsv()
    {
        var example = _clock.Today.AddDays(30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var csv = new StringBuilder()
            .AppendLine("Name,Phone,Package,End Date,Email,Notes")
            .AppendLine($"Sara Khoury,03 123 456,Monthly,{example},sara@example.com,Joined at the old desk")
            .ToString();

        // A BOM, so Excel opens the file as UTF-8 and Arabic or accented names survive the
        // round trip instead of arriving back as question marks.
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
    }

    // ------------------------------------------------------------------ plan

    /// <summary>A checked row and, when it passed, the client it would create.</summary>
    private sealed record PlannedRow(MemberImportRowDto Row, Client? Client);

    private sealed record ImportPlan(List<PlannedRow> Rows, List<string> PackageNames);

    private async Task<ImportPlan> BuildPlanAsync(
        byte[] bytes, string fileName, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var rawRows = _fileReader.Read(stream, fileName);

        if (rawRows.Count == 0)
        {
            throw new BusinessException("That file has column headings but no members under them.");
        }

        var packages = await _unitOfWork.Packages.Query()
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(cancellationToken);

        if (packages.Count == 0)
        {
            throw new BusinessException(
                "There are no packages set up yet. Add the gym's packages first - every imported "
                + "member has to be put on one.");
        }

        var packagesByName = new Dictionary<string, Package>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in packages)
        {
            packagesByName[package.Name.Trim()] = package;
        }

        // Deleted members are matched too. Re-importing someone the owner removed should
        // point at the record that already exists, not quietly create a second one that the
        // restore button will later collide with.
        var existingPhoneKeys = await _unitOfWork.Clients.QueryIncludingDeleted()
            .Select(c => c.PhoneNumber)
            .ToListAsync(cancellationToken);

        var takenKeys = existingPhoneKeys
            .Select(PhoneNumberKey.Normalize)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        var today = _clock.Today;
        var seenInFile = new Dictionary<string, int>(StringComparer.Ordinal);
        var planned = new List<PlannedRow>(rawRows.Count);

        foreach (var raw in rawRows)
        {
            planned.Add(BuildRow(raw, packagesByName, takenKeys, seenInFile, today));
        }

        return new ImportPlan(planned, packages.Select(p => p.Name).ToList());
    }

    private static PlannedRow BuildRow(
        MemberImportRawRow raw,
        IReadOnlyDictionary<string, Package> packagesByName,
        HashSet<string> takenKeys,
        Dictionary<string, int> seenInFile,
        DateOnly today)
    {
        var values = raw.Values;

        var rawName = MemberImportColumns.Find(values, MemberImportColumns.Name);
        var rawFirst = MemberImportColumns.Find(values, MemberImportColumns.FirstName);
        var rawLast = MemberImportColumns.Find(values, MemberImportColumns.LastName);
        var rawPhone = MemberImportColumns.Find(values, MemberImportColumns.Phone);
        var rawPackage = MemberImportColumns.Find(values, MemberImportColumns.Package);
        var rawEnd = MemberImportColumns.Find(values, MemberImportColumns.EndDate);
        var rawStart = MemberImportColumns.Find(values, MemberImportColumns.StartDate);
        var rawEmail = MemberImportColumns.Find(values, MemberImportColumns.Email);
        var rawNotes = MemberImportColumns.Find(values, MemberImportColumns.Notes);

        var row = new MemberImportRowDto
        {
            RowNumber = raw.RowNumber,
            RawName = rawName ?? Join(rawFirst, rawLast),
            RawPhone = rawPhone,
            RawPackage = rawPackage,
            RawEndDate = rawEnd,
            Status = MemberImportRowStatus.Ready
        };

        // --- name
        var (firstName, lastName) = SplitName(rawName, rawFirst, rawLast);
        if (string.IsNullOrWhiteSpace(firstName))
        {
            row.Problems.Add("No name in this row.");
        }
        else if (firstName.Length > 100 || lastName.Length > 100)
        {
            row.Problems.Add("The name is too long to store - shorten it to 100 characters per part.");
        }

        row.FirstName = firstName;
        row.LastName = lastName;

        // --- phone
        var phoneKey = PhoneNumberKey.Normalize(rawPhone);
        if (string.IsNullOrWhiteSpace(rawPhone))
        {
            row.Problems.Add("No phone number. Every member needs one - it is how they are found at the desk.");
        }
        else if (phoneKey == null)
        {
            row.Problems.Add($"'{rawPhone}' does not look like a phone number.");
        }
        else if (rawPhone.Length > 20)
        {
            row.Problems.Add("The phone number is too long to store - it must fit in 20 characters.");
        }

        row.PhoneNumber = rawPhone;

        // --- email, optional
        if (!string.IsNullOrWhiteSpace(rawEmail))
        {
            if (rawEmail.Length > 255 || !LooksLikeEmail(rawEmail))
            {
                row.Problems.Add($"'{rawEmail}' does not look like an email address. Clear the cell if there isn't one.");
            }
            else
            {
                row.Email = rawEmail;
            }
        }

        // --- package
        Package? package = null;
        if (string.IsNullOrWhiteSpace(rawPackage))
        {
            row.Problems.Add("No package. Say which package this member is on.");
        }
        else if (!packagesByName.TryGetValue(rawPackage.Trim(), out package))
        {
            row.Problems.Add($"There is no package called '{rawPackage}'.");
        }
        else
        {
            row.PackageId = package.Id;
            row.PackageName = package.Name;
        }

        // --- end date
        DateOnly? end = null;
        if (string.IsNullOrWhiteSpace(rawEnd))
        {
            row.Problems.Add("No membership end date. Import needs the date their current membership runs out.");
        }
        else if (TryParseDate(rawEnd, out var parsedEnd))
        {
            if (parsedEnd < EarliestSensibleDate)
            {
                row.Problems.Add($"The end date '{rawEnd}' reads as {Show(parsedEnd)}, which is too far in the past to be right.");
            }
            else if (parsedEnd > today.AddYears(MaxYearsAhead))
            {
                row.Problems.Add($"The end date '{rawEnd}' reads as {Show(parsedEnd)}, which is more than {MaxYearsAhead} years away.");
            }
            else
            {
                end = parsedEnd;
            }
        }
        else
        {
            row.Problems.Add($"The end date '{rawEnd}' could not be read. Write it as 31/12/2026 or 2026-12-31.");
        }

        // --- start date, optional
        DateOnly? start = null;
        if (!string.IsNullOrWhiteSpace(rawStart))
        {
            if (TryParseDate(rawStart, out var parsedStart))
            {
                start = parsedStart;
            }
            else
            {
                row.Problems.Add($"The start date '{rawStart}' could not be read. Write it as 31/12/2026 or 2026-12-31, or leave it empty.");
            }
        }

        if (end.HasValue && package != null)
        {
            // With no start date in the file, work back from the end date over the package's
            // own length. Both dates are inclusive - the same rule Client.ExtendMembership
            // uses - so a 30-day package spans end-29 to end.
            if (!start.HasValue)
            {
                start = end.Value.AddDays(-(package.DurationDays - 1));

                // Never derive a start date in the future. A member on a 30-day package whose
                // card runs to December - because they paid for several months at the desk -
                // would otherwise get a start date months ahead, and UpdateMembershipStatus
                // reads that as Pending: a paid-up member refused at the door on day one.
                // Their real start is unknown; what is known is that they are a member today.
                if (start > today) start = today;

                row.StartDateWasDerived = true;
            }
            else if (start > end)
            {
                row.Problems.Add($"The start date {Show(start.Value)} is after the end date {Show(end.Value)}.");
                start = null;
            }
        }

        row.MembershipStartDate = start?.ToDateTime(TimeOnly.MinValue);
        row.MembershipEndDate = end?.ToDateTime(TimeOnly.MinValue);

        if (row.Problems.Count > 0)
        {
            row.Status = MemberImportRowStatus.Error;
            return new PlannedRow(row, null);
        }

        // --- duplicates, checked only once the row is otherwise sound
        if (phoneKey != null)
        {
            if (takenKeys.Contains(phoneKey))
            {
                row.Status = MemberImportRowStatus.Duplicate;
                row.Problems.Add($"{rawPhone} already belongs to a member in the system. This row will be skipped.");
                return new PlannedRow(row, null);
            }

            if (seenInFile.TryGetValue(phoneKey, out var firstRow))
            {
                row.Status = MemberImportRowStatus.Duplicate;
                row.Problems.Add($"{rawPhone} is already on row {firstRow} of this file. This row will be skipped.");
                return new PlannedRow(row, null);
            }

            seenInFile[phoneKey] = raw.RowNumber;
        }

        var client = new Client
        {
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = rawPhone!.Trim(),
            Email = row.Email,
            Notes = Truncate(rawNotes, 1000),
            CurrentPackageId = package!.Id,
            MembershipStartDate = row.MembershipStartDate,
            MembershipEndDate = row.MembershipEndDate,

            // Marked Paid, not Pending. They did pay - to the gym, before this system
            // existed - so putting them on the who-owes-money list would be wrong on their
            // first day. No Payment row is created, so no revenue report is touched.
            PaymentStatus = PaymentStatus.Paid
        };

        client.UpdateMembershipStatus(today);
        row.MembershipStatus = client.MembershipStatus.ToString();

        return new PlannedRow(row, client);
    }

    // ------------------------------------------------------------- helpers

    /// <summary>
    /// Splits a single name column into first and last on the first space, so "Sara Khoury"
    /// and "Ali Al Hassan" both keep everything after the first word as the family name.
    /// Separate first/last columns win when the file has them.
    /// </summary>
    private static (string First, string Last) SplitName(string? full, string? first, string? last)
    {
        if (!string.IsNullOrWhiteSpace(first) || !string.IsNullOrWhiteSpace(last))
        {
            return (first?.Trim() ?? string.Empty, last?.Trim() ?? string.Empty);
        }

        if (string.IsNullOrWhiteSpace(full)) return (string.Empty, string.Empty);

        var trimmed = full.Trim();
        var space = trimmed.IndexOf(' ');

        // A single word is stored as a first name rather than rejected. Some members really
        // are on the list under one name, and refusing the row would make the owner invent
        // a surname just to get past the import.
        return space < 0
            ? (trimmed, string.Empty)
            : (trimmed[..space].Trim(), trimmed[(space + 1)..].Trim());
    }

    private static bool TryParseDate(string text, out DateOnly date)
    {
        var trimmed = text.Trim();

        if (DateTime.TryParseExact(trimmed, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
        {
            date = DateOnly.FromDateTime(parsed);
            return true;
        }

        // A date column that was never formatted as one arrives from Excel as its serial
        // number. Reading it is the difference between an import that works and one that
        // fails on every row with "45678 could not be read".
        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial)
            && serial is > 20000 and < 60000)
        {
            date = DateOnly.FromDateTime(DateTime.FromOADate(serial));
            return true;
        }

        date = default;
        return false;
    }

    /// <summary>Spelled out with the month in words, so no reader has to guess day from month.</summary>
    private static string Show(DateOnly date) =>
        date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    private static bool LooksLikeEmail(string value)
    {
        var at = value.IndexOf('@');
        return at > 0
            && at == value.LastIndexOf('@')
            && at < value.Length - 1
            && value.IndexOf('.', at) > at + 1
            && !value.EndsWith('.')
            && !value.Contains(' ');
    }

    private static string? Join(string? first, string? last)
    {
        var joined = $"{first} {last}".Trim();
        return joined.Length == 0 ? null : joined;
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null
        : value.Length <= max ? value.Trim()
        : value.Trim()[..max];

    private static async Task<byte[]> ReadAllBytesAsync(Stream file, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}

namespace GymManagement.Application.DTOs.Import;

/// <summary>What will happen to one row of the owner's spreadsheet.</summary>
public enum MemberImportRowStatus
{
    /// <summary>Passed every check. Will be created on confirm.</summary>
    Ready,

    /// <summary>This person is already in the system, or appears twice in the file. Skipped.</summary>
    Duplicate,

    /// <summary>Something in the row could not be read or matched. Skipped until the owner fixes it.</summary>
    Error
}

/// <summary>
/// One spreadsheet row, both as it was written and as it was understood.
///
/// The raw values are echoed back deliberately: when a row fails, the owner has to find it
/// in their own file, and "row 34, package 'Monthy'" is findable in a way that "row 34,
/// unknown package" is not.
/// </summary>
public class MemberImportRowDto
{
    /// <summary>Row number in the owner's file, counting the header as row 1.</summary>
    public int RowNumber { get; set; }

    public string? RawName { get; set; }
    public string? RawPhone { get; set; }
    public string? RawPackage { get; set; }
    public string? RawEndDate { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }

    public int? PackageId { get; set; }
    public string? PackageName { get; set; }

    /// <summary>
    /// Start of the current period. Taken from the file when the owner supplied one,
    /// otherwise worked back from the end date and the package length.
    /// </summary>
    public DateTime? MembershipStartDate { get; set; }

    public DateTime? MembershipEndDate { get; set; }

    /// <summary>True when the start date was derived rather than read from the file.</summary>
    public bool StartDateWasDerived { get; set; }

    /// <summary>The status this member will land in, worked out from the end date.</summary>
    public string? MembershipStatus { get; set; }

    public MemberImportRowStatus Status { get; set; }

    /// <summary>Why the row is not Ready. Empty for rows that are.</summary>
    public List<string> Problems { get; set; } = new();
}

/// <summary>The dry run. Nothing has been written when this is returned.</summary>
public class MemberImportPreviewDto
{
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Hash of the uploaded bytes. Confirming sends it back, so a file edited between
    /// previewing and confirming is rejected rather than imported unseen.
    /// </summary>
    public string FileHash { get; set; } = string.Empty;

    public int TotalRows { get; set; }
    public int ReadyCount { get; set; }
    public int DuplicateCount { get; set; }
    public int ErrorCount { get; set; }

    /// <summary>Package names the file may use, so the owner can correct a misspelling.</summary>
    public List<string> AvailablePackages { get; set; } = new();

    public List<MemberImportRowDto> Rows { get; set; } = new();
}

/// <summary>What the confirm actually did.</summary>
public class MemberImportResultDto
{
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }

    /// <summary>The rows that were not imported, with their reasons, so nothing is lost silently.</summary>
    public List<MemberImportRowDto> SkippedRows { get; set; } = new();
}

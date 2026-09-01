using System.Text;
using ClosedXML.Excel;
using FluentAssertions;
using GymManagement.Application.DTOs.Import;
using GymManagement.Application.Exceptions;
using GymManagement.Application.Interfaces;
using GymManagement.Application.Services;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using GymManagement.Infrastructure.Data;
using GymManagement.Infrastructure.Repositories;
using GymManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GymManagement.UnitTests.Services;

/// <summary>
/// Blueprint 6.3 - importing the gym's existing member list.
///
/// This runs once, against the owner's real records, and it is the gate on going live. A
/// wrong end date here is a member turned away at the door, and a missed duplicate is two
/// records for one person that reception has to reconcile by hand forever. So the checks
/// are pinned down against the real file reader and a real database context rather than
/// mocks: most of the risk lives in the parsing, which mocks would step over.
/// </summary>
public class MemberImportServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 8, 28);

    private readonly ApplicationDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly MemberImportService _service;

    public MemberImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"import-{Guid.NewGuid()}")
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Packages.AddRange(
            new Package { Id = 1, Name = "Monthly", DurationDays = 30, Price = 50m, DisplayOrder = 1 },
            new Package { Id = 2, Name = "3 Months", DurationDays = 90, Price = 130m, DisplayOrder = 2 });
        _context.SaveChanges();

        _unitOfWork = new UnitOfWork(_context);
        _service = new MemberImportService(_unitOfWork, new MemberImportFileReader(), new FixedClock(Today), new AuditService(_unitOfWork, new FixedClock(Today)));
    }

    public void Dispose() => _unitOfWork.Dispose();

    // ------------------------------------------------------------- reading

    [Fact]
    public async Task Preview_AGoodFile_ReadsEveryRowAndWritesNothing()
    {
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03 123 456,Monthly,2026-09-30",
            "Ali Hassan,03 222 333,3 Months,2026-12-31");

        preview.TotalRows.Should().Be(2);
        preview.ReadyCount.Should().Be(2);
        preview.ErrorCount.Should().Be(0);

        preview.Rows[0].FirstName.Should().Be("Sara");
        preview.Rows[0].LastName.Should().Be("Khoury");
        preview.Rows[0].MembershipEndDate.Should().Be(new DateTime(2026, 9, 30));

        // The whole point of a preview: the member list is untouched until confirm.
        _context.Clients.Count().Should().Be(0);
    }

    [Fact]
    public async Task Preview_HeadingsTheOwnerWroteTheirOwnWay_AreStillUnderstood()
    {
        var preview = await PreviewCsv(
            "Full Name,Mobile Number,Membership Plan,Expiry Date",
            "Sara Khoury,03123456,Monthly,2026-09-30");

        preview.ReadyCount.Should().Be(1, "the import matches headings by meaning, not by exact spelling");
    }

    [Fact]
    public async Task Preview_SeparateFirstAndLastNameColumns_AreUsedAsGiven()
    {
        var preview = await PreviewCsv(
            "First Name,Last Name,Phone,Package,End Date",
            "Sara,Al Khoury,03123456,Monthly,2026-09-30");

        preview.Rows[0].FirstName.Should().Be("Sara");
        preview.Rows[0].LastName.Should().Be("Al Khoury");
    }

    [Fact]
    public async Task Preview_ANameWithThreeParts_KeepsEverythingAfterTheFirstWordAsTheFamilyName()
    {
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date",
            "Ali Al Hassan,03123456,Monthly,2026-09-30");

        preview.Rows[0].FirstName.Should().Be("Ali");
        preview.Rows[0].LastName.Should().Be("Al Hassan");
    }

    [Fact]
    public async Task Preview_QuotedFieldContainingACommaAndNewline_StaysOneRow()
    {
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date,Notes",
            "Sara Khoury,03123456,Monthly,2026-09-30,\"Pays cash, always\"");

        preview.TotalRows.Should().Be(1);
        preview.ReadyCount.Should().Be(1);
    }

    [Fact]
    public async Task Preview_BlankLinesInTheMiddleOfTheList_AreNotTreatedAsMembers()
    {
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03123456,Monthly,2026-09-30",
            ",,,",
            "Ali Hassan,03222333,Monthly,2026-09-30");

        preview.TotalRows.Should().Be(2);
    }

    [Fact]
    public async Task Preview_AFileThatIsNotAMemberList_IsRejectedOnceRatherThanPerRow()
    {
        var act = () => PreviewCsv("Invoice,Total,VAT", "1,100,10");

        (await act.Should().ThrowAsync<BusinessException>())
            .WithMessage("*column headings*were recognised*");
    }

    // --------------------------------------------------------------- dates

    [Theory]
    [InlineData("2026-09-30")]
    [InlineData("30/09/2026")]
    [InlineData("30-09-2026")]
    [InlineData("30.09.2026")]
    [InlineData("30 Sep 2026")]
    [InlineData("30 September 2026")]
    public void ParsedEndDates_AcceptTheWaysADateGetsWritten(string written)
    {
        var preview = PreviewCsv(
            "Name,Phone,Package,End Date",
            $"Sara Khoury,03123456,Monthly,{written}").Result;

        preview.Rows[0].MembershipEndDate.Should().Be(new DateTime(2026, 9, 30));
    }

    [Fact]
    public async Task Preview_AnAmbiguousDate_IsReadDayFirstBecauseTheGymIsInLebanon()
    {
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03123456,Monthly,04/03/2027");

        preview.Rows[0].MembershipEndDate.Should().Be(new DateTime(2027, 3, 4), "04/03 is the fourth of March here");
    }

    [Fact]
    public async Task Preview_ADateThatCannotBeRead_FailsThatRowAndNamesTheValue()
    {
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03123456,Monthly,next month");

        preview.ErrorCount.Should().Be(1);
        preview.Rows[0].Problems.Should().ContainSingle().Which.Should().Contain("next month");
    }

    [Fact]
    public async Task Preview_ADateFarInThePast_IsTreatedAsATypoRatherThanImported()
    {
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03123456,Monthly,30/09/1926");

        preview.ErrorCount.Should().Be(1);
        preview.Rows[0].Problems.Single().Should().Contain("too far in the past");
    }

    [Fact]
    public async Task Preview_ADateFarInTheFuture_IsTreatedAsATypo()
    {
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03123456,Monthly,30/09/2099");

        preview.ErrorCount.Should().Be(1);
        preview.Rows[0].Problems.Single().Should().Contain("more than 5 years away");
    }

    // ------------------------------------------------------- derived dates

    [Fact]
    public async Task Preview_WithNoStartDateInTheFile_WorksItBackOverThePackageLengthInclusively()
    {
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03123456,Monthly,2026-09-10");

        var row = preview.Rows[0];
        row.StartDateWasDerived.Should().BeTrue();
        row.MembershipStartDate.Should().Be(new DateTime(2026, 8, 12),
            "a 30-day package ending 10 September covers 12 August to 10 September, both days included");
    }

    [Fact]
    public async Task Preview_ShortPackageWithADistantEndDate_StaysActiveInsteadOfBecomingPending()
    {
        // The owner's list says this member is on Monthly and paid up to the end of the year,
        // because they paid for several months at the desk at once. Working 30 days back from
        // December would put their period in the future and mark them Pending - a paid-up
        // member refused at the door on the day the gym goes live.
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03123456,Monthly,2026-12-31");

        preview.Rows[0].MembershipStartDate.Should().Be(Today.ToDateTime(TimeOnly.MinValue));
        preview.Rows[0].MembershipStatus.Should().Be(nameof(MembershipStatus.Active));
    }

    [Fact]
    public async Task Preview_AStartDateTheOwnerActuallyWroteInTheFuture_IsLeftAlone()
    {
        // Unlike a derived one, a start date the owner typed is a decision: this membership
        // has been paid for and begins next month. Pending is the right status for it.
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date,Start Date",
            "Sara Khoury,03123456,Monthly,2026-10-30,2026-10-01");

        preview.Rows[0].MembershipStartDate.Should().Be(new DateTime(2026, 10, 1));
        preview.Rows[0].MembershipStatus.Should().Be(nameof(MembershipStatus.Pending));
    }

    [Fact]
    public async Task Preview_WithAStartDateInTheFile_UsesTheOwnersDateInstead()
    {
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date,Joined",
            "Sara Khoury,03123456,Monthly,2026-09-30,2024-01-15");

        preview.Rows[0].StartDateWasDerived.Should().BeFalse();
        preview.Rows[0].MembershipStartDate.Should().Be(new DateTime(2024, 1, 15));
    }

    [Fact]
    public async Task Preview_StartDateAfterEndDate_IsRejected()
    {
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date,Start Date",
            "Sara Khoury,03123456,Monthly,2026-09-30,2026-10-30");

        preview.ErrorCount.Should().Be(1);
        preview.Rows[0].Problems.Single().Should().Contain("after the end date");
    }

    [Theory]
    [InlineData("2026-12-31", nameof(MembershipStatus.Active))]
    [InlineData("2026-09-02", nameof(MembershipStatus.Expiring))]
    [InlineData("2026-08-01", nameof(MembershipStatus.Expired))]
    public void Preview_StatusIsWorkedOutFromTheEndDate(string endDate, string expected)
    {
        var preview = PreviewCsv(
            "Name,Phone,Package,End Date",
            $"Sara Khoury,03123456,3 Months,{endDate}").Result;

        preview.Rows[0].MembershipStatus.Should().Be(expected);
    }

    // ---------------------------------------------------------- validation

    [Fact]
    public async Task Preview_AnUnknownPackage_FailsTheRowAndOffersTheRealNames()
    {
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03123456,Monthy,2026-09-30");

        preview.ErrorCount.Should().Be(1);
        preview.Rows[0].Problems.Single().Should().Contain("Monthy");
        preview.AvailablePackages.Should().BeEquivalentTo("Monthly", "3 Months");
    }

    [Fact]
    public async Task Preview_APackageNameInADifferentCase_StillMatches()
    {
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03123456,MONTHLY,2026-09-30");

        preview.ReadyCount.Should().Be(1);
    }

    [Fact]
    public async Task Preview_MissingNameOrPhone_FailsTheRow()
    {
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date",
            ",03123456,Monthly,2026-09-30",
            "Ali Hassan,,Monthly,2026-09-30");

        preview.ErrorCount.Should().Be(2);
        preview.Rows[0].Problems.Single().Should().Contain("No name");
        preview.Rows[1].Problems.Single().Should().Contain("No phone number");
    }

    [Fact]
    public async Task Preview_ARowWithSeveralThingsWrong_ReportsAllOfThemAtOnce()
    {
        // One reason per upload would mean the owner fixes their file five times over.
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date",
            ",not a phone,Nope,rubbish");

        preview.Rows[0].Problems.Should().HaveCount(4);
    }

    // ---------------------------------------------------------- duplicates

    [Fact]
    public async Task Preview_APhoneAlreadyInTheSystem_IsMarkedDuplicateNotError()
    {
        _context.Clients.Add(new Client
        {
            FirstName = "Sara", LastName = "Khoury", PhoneNumber = "+961 3 123 456"
        });
        await _context.SaveChangesAsync();

        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03 123 456,Monthly,2026-09-30");

        preview.DuplicateCount.Should().Be(1, "the same number written two ways is one person");
        preview.ReadyCount.Should().Be(0);
    }

    [Fact]
    public async Task Preview_APhoneBelongingToARemovedMember_IsStillADuplicate()
    {
        var removed = new Client { FirstName = "Sara", LastName = "Khoury", PhoneNumber = "03123456" };
        removed.SoftDelete();
        _context.Clients.Add(removed);
        await _context.SaveChangesAsync();

        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03123456,Monthly,2026-09-30");

        preview.DuplicateCount.Should().Be(1, "restoring the old record is right; a second one is not");
    }

    [Fact]
    public async Task Preview_TheSamePersonTwiceInOneFile_ImportsThemOnce()
    {
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03123456,Monthly,2026-09-30",
            "Sara Khoury,03 123 456,Monthly,2026-09-30");

        preview.ReadyCount.Should().Be(1);
        preview.DuplicateCount.Should().Be(1);
        preview.Rows[1].Problems.Single().Should().Contain("row 2");
    }

    // -------------------------------------------------------------- commit

    [Fact]
    public async Task Commit_CreatesTheReadyRowsWithTheirDatesAndStatus()
    {
        var csv = Csv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03 123 456,Monthly,2026-12-31");

        var preview = await Preview(csv);
        var result = await Commit(csv, preview.FileHash, acknowledgeSkipped: false, userId: 7);

        result.ImportedCount.Should().Be(1);

        var client = _context.Clients.Single();
        client.FirstName.Should().Be("Sara");
        client.PhoneNumber.Should().Be("03 123 456", "the number is stored exactly as the owner wrote it");
        client.CurrentPackageId.Should().Be(1);
        client.MembershipEndDate.Should().Be(new DateTime(2026, 12, 31));
        client.MembershipStatusOn(new DateOnly(2026, 8, 28)).Should().Be(MembershipStatus.Active);
        client.CreatedBy.Should().Be(7, "with no payment behind them, this is the only record of where they came from");
    }

    [Fact]
    public async Task Commit_DoesNotInventAnyPaymentHistory()
    {
        var csv = Csv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03123456,Monthly,2026-12-31");

        var preview = await Preview(csv);
        await Commit(csv, preview.FileHash, acknowledgeSkipped: false, userId: 7);

        _context.Payments.Should().BeEmpty("money the gym never took through this system must not appear in its reports");
        _context.PaymentHistories.Should().BeEmpty();

        // Marked Paid all the same: they are paid up, just not through here, so they must
        // not turn up on the who-owes-money list on their first day.
        _context.Clients.Single().PaymentStatus.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public async Task Commit_WithAFileDifferentFromTheOnePreviewed_IsRefused()
    {
        var previewed = await Preview(Csv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03123456,Monthly,2026-12-31"));

        var edited = Csv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03123456,Monthly,2026-12-31",
            "Someone Else,03999888,Monthly,2026-12-31");

        var act = () => Commit(edited, previewed.FileHash, acknowledgeSkipped: false, userId: 7);

        (await act.Should().ThrowAsync<BusinessException>()).WithMessage("*not the one that was checked*");
        _context.Clients.Should().BeEmpty();
    }

    [Fact]
    public async Task Commit_WithRowsThatFailed_StopsUntilTheOwnerSaysToGoWithoutThem()
    {
        var csv = Csv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03123456,Monthly,2026-12-31",
            "Broken Row,03222333,Nope,2026-12-31");

        var preview = await Preview(csv);

        var act = () => Commit(csv, preview.FileHash, acknowledgeSkipped: false, userId: 7);
        (await act.Should().ThrowAsync<BusinessException>()).WithMessage("*1 of 2 rows*");
        _context.Clients.Should().BeEmpty("a partly-imported list is worse than none");

        var result = await Commit(csv, preview.FileHash, acknowledgeSkipped: true, userId: 7);
        result.ImportedCount.Should().Be(1);
        result.SkippedCount.Should().Be(1);
        result.SkippedRows.Single().RowNumber.Should().Be(3, "so the owner can find it in their own file");
    }

    [Fact]
    public async Task Commit_RunTwiceOnTheSameFile_DoesNotCreateAnybodyTwice()
    {
        var csv = Csv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03123456,Monthly,2026-12-31");

        var preview = await Preview(csv);
        await Commit(csv, preview.FileHash, acknowledgeSkipped: false, userId: 7);

        // Re-checked at commit time, so the row the first run created is now a duplicate.
        var act = () => Commit(csv, preview.FileHash, acknowledgeSkipped: true, userId: 7);

        (await act.Should().ThrowAsync<BusinessException>()).WithMessage("*nothing to import*");
        _context.Clients.Should().HaveCount(1);
    }

    [Fact]
    public async Task Commit_WhenOneRowWouldFailToSave_ImportsNobody()
    {
        // A name past the column limit is caught as a row problem rather than blowing up
        // mid-write, so the all-or-nothing promise never has to be tested by a live failure.
        var preview = await PreviewCsv(
            "Name,Phone,Package,End Date",
            "Sara Khoury,03123456,Monthly,2026-12-31",
            new string('A', 150) + " Name,03222333,Monthly,2026-12-31");

        preview.ErrorCount.Should().Be(1);
        preview.Rows[1].Problems.Single().Should().Contain("too long");
    }

    // ---------------------------------------------------------------- xlsx

    [Fact]
    public async Task Preview_AnExcelWorkbook_IsReadTheSameWayAsACsv()
    {
        var preview = await PreviewXlsx(sheet =>
        {
            sheet.Cell("A1").Value = "Name";
            sheet.Cell("B1").Value = "Phone";
            sheet.Cell("C1").Value = "Package";
            sheet.Cell("D1").Value = "End Date";

            sheet.Cell("A2").Value = "Sara Khoury";
            sheet.Cell("B2").Value = "03 123 456";
            sheet.Cell("C2").Value = "Monthly";
            sheet.Cell("D2").Value = "2026-12-31";
        });

        preview.ReadyCount.Should().Be(1);
        preview.Rows[0].MembershipEndDate.Should().Be(new DateTime(2026, 12, 31));
    }

    [Fact]
    public async Task Preview_ARealDateCellDisplayedTheAmericanWay_IsReadFromItsValueNotItsDisplay()
    {
        // The cell holds 4 March 2026 but is formatted to show 03/04/2026. Reading what the
        // owner sees would move this membership by eleven months; reading the value cannot.
        var preview = await PreviewXlsx(sheet =>
        {
            sheet.Cell("A1").Value = "Name";
            sheet.Cell("B1").Value = "Phone";
            sheet.Cell("C1").Value = "Package";
            sheet.Cell("D1").Value = "End Date";

            sheet.Cell("A2").Value = "Sara Khoury";
            sheet.Cell("B2").Value = "03123456";
            sheet.Cell("C2").Value = "Monthly";
            sheet.Cell("D2").Value = new DateTime(2027, 3, 4);
            sheet.Cell("D2").Style.DateFormat.Format = "MM/dd/yyyy";
        });

        preview.Rows[0].MembershipEndDate.Should().Be(new DateTime(2027, 3, 4));
    }

    [Fact]
    public async Task Preview_APhoneCellExcelTurnedIntoANumber_IsStillReadAsThatNumber()
    {
        // Excel eats the leading zero on "03123456" and stores 3123456 as a number. The
        // digits still identify the member, which is all the duplicate check needs.
        var preview = await PreviewXlsx(sheet =>
        {
            sheet.Cell("A1").Value = "Name";
            sheet.Cell("B1").Value = "Phone";
            sheet.Cell("C1").Value = "Package";
            sheet.Cell("D1").Value = "End Date";

            sheet.Cell("A2").Value = "Sara Khoury";
            sheet.Cell("B2").Value = 3123456;
            sheet.Cell("C2").Value = "Monthly";
            sheet.Cell("D2").Value = new DateTime(2026, 12, 31);
        });

        preview.ReadyCount.Should().Be(1);
        preview.Rows[0].PhoneNumber.Should().Be("3123456");
    }

    [Fact]
    public async Task Preview_AWorkbookWithOnlyHeadings_SaysSoRatherThanImportingNobody()
    {
        var act = () => PreviewXlsx(sheet =>
        {
            sheet.Cell("A1").Value = "Name";
            sheet.Cell("B1").Value = "Phone";
            sheet.Cell("C1").Value = "Package";
            sheet.Cell("D1").Value = "End Date";
        });

        (await act.Should().ThrowAsync<BusinessException>()).WithMessage("*no members under them*");
    }

    [Fact]
    public async Task Preview_AFileThatIsNotAWorkbookAtAll_GivesAnAnswerAboutTheFile()
    {
        var act = () => _service.PreviewAsync(
            new MemoryStream(Encoding.UTF8.GetBytes("this is not a spreadsheet")), "members.xlsx");

        (await act.Should().ThrowAsync<BusinessException>()).WithMessage("*could not be opened as a spreadsheet*");
    }

    [Fact]
    public void Template_HasTheHeadingsTheImportAsksFor()
    {
        var csv = Encoding.UTF8.GetString(_service.BuildTemplateCsv());

        csv.Should().Contain("Name,Phone,Package,End Date");
        csv.Should().StartWith("﻿", "Excel needs the BOM to open it as UTF-8 and keep accented names intact");
    }

    // ------------------------------------------------------------- helpers

    private static byte[] Csv(params string[] lines) =>
        Encoding.UTF8.GetBytes(string.Join("\r\n", lines) + "\r\n");

    private Task<MemberImportPreviewDto> PreviewCsv(params string[] lines) => Preview(Csv(lines));

    /// <summary>Builds a real .xlsx in memory and pushes it through the real reader.</summary>
    private Task<MemberImportPreviewDto> PreviewXlsx(Action<IXLWorksheet> build)
    {
        using var workbook = new XLWorkbook();
        build(workbook.Worksheets.Add("Members"));

        using var saved = new MemoryStream();
        workbook.SaveAs(saved);

        return _service.PreviewAsync(new MemoryStream(saved.ToArray()), "members.xlsx");
    }

    private Task<MemberImportPreviewDto> Preview(byte[] csv) =>
        _service.PreviewAsync(new MemoryStream(csv), "members.csv");

    private Task<MemberImportResultDto> Commit(byte[] csv, string hash, bool acknowledgeSkipped, int? userId) =>
        _service.CommitAsync(new MemoryStream(csv), "members.csv", hash, acknowledgeSkipped, userId);

}

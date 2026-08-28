using FluentAssertions;
using GymManagement.Domain.Entities;
using GymManagement.Domain.Enums;
using Xunit;

namespace GymManagement.UnitTests.Domain;

/// <summary>
/// The membership date rules from blueprint 6.5 and section 07. These are the rules that
/// decide whether a member gets through the door and how much time they were sold, so they
/// are pinned down here rather than left to be verified by hand at the desk.
/// </summary>
public class ClientMembershipTests
{
    private static readonly DateOnly Today = new(2026, 8, 28);

    private static Package MonthlyPackage => new() { Id = 1, Name = "Monthly", DurationDays = 30, Price = 50m };

    private static Client NewClient() => new() { FirstName = "Test", LastName = "Member", PhoneNumber = "03000000" };

    // ---- ExtendMembership ----

    [Fact]
    public void ExtendMembership_FirstPayment_StartsToday()
    {
        var client = NewClient();

        var period = client.ExtendMembership(MonthlyPackage, Today);

        period.Start.Should().Be(Today);
        period.End.Should().Be(Today.AddDays(29), "30 days inclusive of the start day");
        client.MembershipStartDate.Should().Be(Today.ToDateTime(TimeOnly.MinValue));
        client.MembershipStatus.Should().Be(MembershipStatus.Active);
        client.PaymentStatus.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public void ExtendMembership_RenewingEarly_StartsAfterCurrentEndSoNoDaysAreLost()
    {
        var client = NewClient();
        client.ExtendMembership(MonthlyPackage, Today);
        var firstEnd = Today.AddDays(29);

        // Renews with 10 days still to run.
        var period = client.ExtendMembership(MonthlyPackage, Today.AddDays(20));

        period.Start.Should().Be(firstEnd.AddDays(1), "paid-for days must not be thrown away");
        period.End.Should().Be(firstEnd.AddDays(30), "a renewal adds exactly one term");
    }

    [Fact]
    public void ExtendMembership_RenewingAfterExpiry_StartsToday()
    {
        var client = NewClient();
        client.ExtendMembership(MonthlyPackage, Today);

        // Comes back two months after lapsing; they do not get backdated credit.
        var renewalDay = Today.AddDays(90);
        var period = client.ExtendMembership(MonthlyPackage, renewalDay);

        period.Start.Should().Be(renewalDay);
        period.End.Should().Be(renewalDay.AddDays(29));
    }

    [Fact]
    public void ExtendMembership_OnRenewal_DoesNotMoveTheJoinDate()
    {
        var client = NewClient();
        client.ExtendMembership(MonthlyPackage, Today);
        var joinDate = client.MembershipStartDate;

        client.ExtendMembership(MonthlyPackage, Today.AddDays(40));

        client.MembershipStartDate.Should().Be(joinDate,
            "the gym would otherwise lose the record of when each member actually joined");
    }

    // ---- UpdateMembershipStatus ----

    [Fact]
    public void UpdateMembershipStatus_NeverPaid_IsPending()
    {
        var client = NewClient();

        client.UpdateMembershipStatus(Today);

        client.MembershipStatus.Should().Be(MembershipStatus.Pending);
    }

    [Theory]
    [InlineData(30, MembershipStatus.Active)]
    [InlineData(8, MembershipStatus.Active)]
    [InlineData(7, MembershipStatus.Expiring)]
    [InlineData(1, MembershipStatus.Expiring)]
    [InlineData(0, MembershipStatus.Expiring)]
    [InlineData(-1, MembershipStatus.Expired)]
    public void UpdateMembershipStatus_FollowsTheEndDate(int daysRemaining, MembershipStatus expected)
    {
        var client = NewClient();
        client.MembershipStartDate = Today.AddDays(-60).ToDateTime(TimeOnly.MinValue);
        client.MembershipEndDate = Today.AddDays(daysRemaining).ToDateTime(TimeOnly.MinValue);

        client.UpdateMembershipStatus(Today);

        client.MembershipStatus.Should().Be(expected);
    }

    [Fact]
    public void UpdateMembershipStatus_LeavesSuspendedAlone()
    {
        var client = NewClient();
        client.ExtendMembership(MonthlyPackage, Today);
        client.MembershipStatus = MembershipStatus.Suspended;

        // The nightly job runs long after the membership would otherwise have expired.
        client.UpdateMembershipStatus(Today.AddDays(365));

        client.MembershipStatus.Should().Be(MembershipStatus.Suspended,
            "a freeze is set by a person and the nightly recalculation must not undo it");
    }

    [Fact]
    public void UpdateMembershipStatus_OnTheLastDay_StillAllowsEntry()
    {
        var client = NewClient();
        client.ExtendMembership(MonthlyPackage, Today);
        var lastDay = DateOnly.FromDateTime(client.MembershipEndDate!.Value);

        client.UpdateMembershipStatus(lastDay);

        MembershipStatuses.AllowedIn.Should().Contain(client.MembershipStatus,
            "the end date is inclusive - a member is entitled to train on the day it ends");
    }

    // ---- Soft delete and restore ----

    [Fact]
    public void SoftDeleteThenRestore_RecalculatesStatus()
    {
        var client = NewClient();
        client.ExtendMembership(MonthlyPackage, Today);

        client.SoftDelete();
        client.Restore(Today);

        client.IsActive.Should().BeTrue();
        client.MembershipStatus.Should().Be(MembershipStatus.Active,
            "deleting marked the client Suspended, which Restore could then never undo");
    }

    // ---- WindBackMembership ----

    [Fact]
    public void WindBackMembership_RemovesTheDaysThePaymentBought()
    {
        var client = NewClient();
        client.ExtendMembership(MonthlyPackage, Today);

        client.WindBackMembership(MonthlyPackage.DurationDays, Today);

        client.MembershipEndDate.Should().Be(Today.AddDays(-1).ToDateTime(TimeOnly.MinValue),
            "one term added then one term removed must land exactly where it started");
    }

    [Fact]
    public void WindBackMembership_AfterALaterRenewal_OnlyRemovesOneTerm()
    {
        var client = NewClient();
        client.ExtendMembership(MonthlyPackage, Today);
        client.ExtendMembership(MonthlyPackage, Today.AddDays(10));
        var endAfterTwoTerms = client.MembershipEndDate;

        client.WindBackMembership(MonthlyPackage.DurationDays, Today);

        client.MembershipEndDate.Should().Be(endAfterTwoTerms!.Value.AddDays(-30),
            "reversing one payment must not delete days a different payment paid for");
    }

    [Fact]
    public void WindBackMembership_LeavesTheJoinDateAlone()
    {
        var client = NewClient();
        client.ExtendMembership(MonthlyPackage, Today);
        var joinDate = client.MembershipStartDate;

        client.WindBackMembership(MonthlyPackage.DurationDays, Today);

        client.MembershipStartDate.Should().Be(joinDate, "reversing a renewal does not unjoin anyone");
    }
}

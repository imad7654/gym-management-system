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
        client.MembershipStatusOn(Today).Should().Be(MembershipStatus.Active);
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

    // ---- Status derived from the dates ----

    [Fact]
    public void Status_WhenNeverPaid_IsPending()
    {
        var client = NewClient();

        client.MembershipStatusOn(Today).Should().Be(MembershipStatus.Pending);
    }

    [Theory]
    [InlineData(30, MembershipStatus.Active)]
    [InlineData(8, MembershipStatus.Active)]
    [InlineData(7, MembershipStatus.Expiring)]
    [InlineData(1, MembershipStatus.Expiring)]
    [InlineData(0, MembershipStatus.Expiring)]
    [InlineData(-1, MembershipStatus.Expired)]
    public void Status_FollowsTheEndDate(int daysRemaining, MembershipStatus expected)
    {
        var client = NewClient();
        client.MembershipStartDate = Today.AddDays(-60).ToDateTime(TimeOnly.MinValue);
        client.MembershipEndDate = Today.AddDays(daysRemaining).ToDateTime(TimeOnly.MinValue);

        client.MembershipStatusOn(Today).Should().Be(expected);
    }

    /// <summary>
    /// The bug this whole design exists to prevent. The status used to be a stored column
    /// refreshed by a nightly job that was never written, so a membership that ran out
    /// went on reading Active indefinitely - the door would have let them in.
    /// </summary>
    [Fact]
    public void Status_GoesStaleForNobody_EvenIfNothingEverTouchesTheRecord()
    {
        var client = NewClient();
        client.ExtendMembership(MonthlyPackage, Today);

        // Nothing is called in between. No job, no edit, no save.
        client.MembershipStatusOn(Today.AddDays(400)).Should().Be(MembershipStatus.Expired,
            "a membership that ran out months ago cannot still read as current");
    }

    [Fact]
    public void Status_WhenStartDateIsInTheFuture_IsPending()
    {
        var client = NewClient();
        client.MembershipStartDate = Today.AddDays(5).ToDateTime(TimeOnly.MinValue);
        client.MembershipEndDate = Today.AddDays(35).ToDateTime(TimeOnly.MinValue);

        client.MembershipStatusOn(Today).Should().Be(MembershipStatus.Pending,
            "a membership dated to start later does not let anyone in yet");
    }

    [Fact]
    public void Status_WhenSuspended_BeatsTheDates()
    {
        var client = NewClient();
        client.ExtendMembership(MonthlyPackage, Today);

        client.Suspend();

        client.MembershipStatusOn(Today).Should().Be(MembershipStatus.Suspended,
            "a freeze is set by a person and the dates must not override it");
    }

    [Fact]
    public void Resume_PutsTheMemberStraightBackOnTheirDates()
    {
        var client = NewClient();
        client.ExtendMembership(MonthlyPackage, Today);
        client.Suspend();

        client.Resume();

        client.MembershipStatusOn(Today).Should().Be(MembershipStatus.Active,
            "lifting a freeze needs no recalculation - the dates were never changed");
    }

    [Fact]
    public void Status_OnTheLastDay_StillAllowsEntry()
    {
        var client = NewClient();
        client.ExtendMembership(MonthlyPackage, Today);
        var lastDay = DateOnly.FromDateTime(client.MembershipEndDate!.Value);

        MembershipStatuses.AllowedIn.Should().Contain(client.MembershipStatusOn(lastDay),
            "the end date is inclusive - a member is entitled to train on the day it ends");
    }

    [Fact]
    public void ExtendMembership_LiftsAFreeze()
    {
        var client = NewClient();
        client.ExtendMembership(MonthlyPackage, Today);
        client.Suspend();

        client.ExtendMembership(MonthlyPackage, Today.AddDays(10));

        client.IsSuspended.Should().BeFalse(
            "someone who paused for travel and has now renewed at the desk is back, "
            + "and leaving them frozen would turn them away at the door");
    }

    // ---- Soft delete and restore ----

    [Fact]
    public void SoftDeleteThenRestore_LeavesTheMembershipIntact()
    {
        var client = NewClient();
        client.ExtendMembership(MonthlyPackage, Today);

        client.SoftDelete();
        client.Restore();

        client.IsActive.Should().BeTrue();
        client.MembershipStatusOn(Today).Should().Be(MembershipStatus.Active,
            "removal and freezing are different things; deleting must not strand the member");
    }

    // ---- WindBackMembership ----

    [Fact]
    public void WindBackMembership_RemovesTheDaysThePaymentBought()
    {
        var client = NewClient();
        client.ExtendMembership(MonthlyPackage, Today);

        client.WindBackMembership(MonthlyPackage.DurationDays);

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

        client.WindBackMembership(MonthlyPackage.DurationDays);

        client.MembershipEndDate.Should().Be(endAfterTwoTerms!.Value.AddDays(-30),
            "reversing one payment must not delete days a different payment paid for");
    }

    [Fact]
    public void WindBackMembership_LeavesTheJoinDateAlone()
    {
        var client = NewClient();
        client.ExtendMembership(MonthlyPackage, Today);
        var joinDate = client.MembershipStartDate;

        client.WindBackMembership(MonthlyPackage.DurationDays);

        client.MembershipStartDate.Should().Be(joinDate, "reversing a renewal does not unjoin anyone");
    }

    [Fact]
    public void WindBackMembership_LeavesAFrozenMemberFrozen()
    {
        var client = NewClient();
        client.ExtendMembership(MonthlyPackage, Today);
        client.Suspend();

        client.WindBackMembership(MonthlyPackage.DurationDays);

        client.IsSuspended.Should().BeTrue("a reversal is not a reason to unfreeze anyone");
    }
}

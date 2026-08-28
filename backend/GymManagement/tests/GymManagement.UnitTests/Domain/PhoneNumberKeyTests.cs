using FluentAssertions;
using GymManagement.Domain.Common;
using Xunit;

namespace GymManagement.UnitTests.Domain;

/// <summary>
/// The rule that decides whether two written numbers are the same person. It gates the
/// member import's duplicate check today and will gate Phase 3's "sign up by phone" match,
/// so a mistake here either creates twin members or hands one member another one's account.
/// </summary>
public class PhoneNumberKeyTests
{
    [Theory]
    [InlineData("03123456")]
    [InlineData("03 123 456")]
    [InlineData("03-123-456")]
    [InlineData("+961 3 123 456")]
    [InlineData("00961 3 123 456")]
    [InlineData("(03) 123456")]
    [InlineData("961 03 123 456")]
    public void Normalize_EveryWayTheGymWritesOneNumber_GivesTheSameKey(string written)
    {
        PhoneNumberKey.Normalize(written).Should().Be("3123456");
    }

    [Fact]
    public void Normalize_DifferentNumbers_GiveDifferentKeys()
    {
        PhoneNumberKey.Normalize("03123456").Should().NotBe(PhoneNumberKey.Normalize("03123457"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no phone")]
    [InlineData("12345")]
    public void Normalize_WhenThereIsNoUsableNumber_ReturnsNull(string? written)
    {
        // Null rather than empty string, so rows with no number are never matched against
        // each other - an empty key would make every blank row a duplicate of the first.
        PhoneNumberKey.Normalize(written).Should().BeNull();
    }

    [Fact]
    public void Normalize_ShortNumberBeginning961_KeepsItsDigits()
    {
        // Stripping "961" here would leave four digits and could collide with a real member.
        PhoneNumberKey.Normalize("961234").Should().Be("961234");
    }
}

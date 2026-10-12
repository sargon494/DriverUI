using Core.Services;
using FluentAssertions;
using Xunit;

namespace Core.Tests;

public sealed class VersionComparerTests
{
    [Theory]
    [InlineData("2.0", "1.0", true)]
    [InlineData("1.0", "2.0", false)]
    [InlineData("31.0.101.5600", "31.0.101.5590", true)]
    [InlineData("31.0.101.5590", "31.0.101.5600", false)]
    [InlineData("10.0.1", "10.0", true)]
    [InlineData("10.0.0.1", "10.0.0", true)]
    [InlineData("2.0.0.0", "1.999.999.999", true)]
    public void IsNewer_ReturnsExpectedResult_ForVersionPairs(string candidate, string current, bool expected)
    {
        var result = VersionComparer.IsNewer(candidate, current);
        result.Should().Be(expected);
    }

    [Fact]
    public void Compare_TreatsOmittedTrailingZeroSegmentsAsEqual()
    {
        VersionComparer.Compare("10.0", "10.0.0.0").Should().Be(0);
        VersionComparer.Compare("10.0.0.0", "10.0").Should().Be(0);
        VersionComparer.Compare("1.0.0", "1.0").Should().Be(0);
    }

    [Fact]
    public void Compare_ReturnsZero_WhenVersionsAreIdentical()
    {
        VersionComparer.Compare("30.0.100.1234", "30.0.100.1234").Should().Be(0);
    }

    [Fact]
    public void Compare_ReturnsNegative_WhenFirstIsLower()
    {
        VersionComparer.Compare("1.2.3.4", "1.2.3.5").Should().BeNegative();
    }

    [Fact]
    public void Compare_ReturnsPositive_WhenFirstIsHigher()
    {
        VersionComparer.Compare("5.0.0", "4.9.9").Should().BePositive();
    }

    [Fact]
    public void Compare_ThrowsArgumentNullException_WhenAnyVersionIsNull()
    {
        var act1 = () => VersionComparer.Compare(null, "1.0");
        act1.Should().Throw<ArgumentNullException>();

        var act2 = () => VersionComparer.Compare("1.0", null);
        act2.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("", "1.0")]
    [InlineData("1.0", "")]
    [InlineData("   ", "1.0")]
    public void Compare_ThrowsArgumentException_WhenVersionIsEmptyOrWhitespace(string v1, string v2)
    {
        var act = () => VersionComparer.Compare(v1, v2);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("1.0.beta")]
    [InlineData("v2.0")]
    [InlineData("1.0.0.abc")]
    public void Compare_ThrowsFormatException_WhenVersionContainsNonNumericSegments(string invalidVersion)
    {
        var act = () => VersionComparer.Compare(invalidVersion, "1.0");
        act.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("31.0.101.5590", true)]
    [InlineData("10", true)]
    [InlineData("1.0.beta", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_IdentifiesSyntacticallyValidVersions(string? version, bool expected)
    {
        VersionComparer.IsValid(version).Should().Be(expected);
    }

    [Fact]
    public void DefaultComparer_SortsCollectionsAscending()
    {
        var versions = new[] { "3.0", "1.0", "2.1", "1.0.1" };
        var sorted = versions.OrderBy(v => v, VersionComparer.Default).ToList();

        sorted.Should().Equal("1.0", "1.0.1", "2.1", "3.0");
    }
}

using Core.Models;
using FluentAssertions;
using Xunit;

namespace Core.Tests;

public sealed class HardwareIdTests
{
    [Fact]
    public void Matches_ReturnsTrue_ForExactMatch()
    {
        var id1 = new HardwareId { Value = "PCI\\VEN_8086&DEV_9A49" };
        var id2 = new HardwareId { Value = "PCI\\VEN_8086&DEV_9A49" };

        id1.Matches(id2).Should().BeTrue();
    }

    [Fact]
    public void Matches_ReturnsTrue_IgnoringCaseDifferences()
    {
        var id1 = new HardwareId { Value = "pci\\ven_8086&dev_9a49" };
        var id2 = new HardwareId { Value = "PCI\\VEN_8086&DEV_9A49" };

        id1.Matches(id2).Should().BeTrue();
        id1.Matches("PCI\\VEN_8086&DEV_9A49").Should().BeTrue();
    }

    [Fact]
    public void Matches_ReturnsFalse_ForDifferentIds()
    {
        var id1 = new HardwareId { Value = "PCI\\VEN_8086&DEV_9A49" };
        var id2 = new HardwareId { Value = "PCI\\VEN_10DE&DEV_2484" };

        id1.Matches(id2).Should().BeFalse();
        id1.Matches("PCI\\VEN_10DE&DEV_2484").Should().BeFalse();
    }

    [Fact]
    public void Matches_ThrowsArgumentNullException_WhenOtherIsNull()
    {
        var id = new HardwareId { Value = "PCI\\VEN_8086&DEV_9A49" };

        var act1 = () => id.Matches((HardwareId)null!);
        act1.Should().Throw<ArgumentNullException>();

        var act2 = () => id.Matches((string)null!);
        act2.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToString_ReturnsUnderlyingValue()
    {
        var raw = "PCI\\VEN_8086&DEV_9A49";
        var id = new HardwareId { Value = raw };

        id.ToString().Should().Be(raw);
    }
}

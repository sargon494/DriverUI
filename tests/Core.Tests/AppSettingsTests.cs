using Core.Models;
using Core.Services;
using FluentAssertions;
using Xunit;

namespace Core.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void Normalize_RemovesBlankAndDuplicateIdsIgnoringCase()
    {
        var settings = new AppSettings
        {
            ExcludedHardwareIds = ["PCI\\VEN_8086", "  ", "pci\\ven_8086", "", "ACPI\\X"]
        };

        var normalized = settings.Normalize();

        normalized.ExcludedHardwareIds.Should().BeEquivalentTo(["PCI\\VEN_8086", "ACPI\\X"]);
    }

    [Fact]
    public void Normalize_RejectsControlCharactersAndOverlongIds()
    {
        var settings = new AppSettings
        {
            ExcludedHardwareIds = ["bad\nid", new string('a', 300), "ok\\id"]
        };

        settings.Normalize().ExcludedHardwareIds.Should().Equal("ok\\id");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(24, 24)]
    [InlineData(168, 168)]
    [InlineData(5, 0)]
    [InlineData(-1, 0)]
    public void Normalize_OnlyKeepsAllowedIntervals(int requested, int expected)
    {
        new AppSettings { CheckIntervalHours = requested }.Normalize().CheckIntervalHours.Should().Be(expected);
    }

    [Fact]
    public void Normalize_CapsNumberOfExcludedIds()
    {
        var ids = Enumerable.Range(0, 1000).Select(i => $"ID\\{i}").ToList();

        new AppSettings { ExcludedHardwareIds = ids }.Normalize().ExcludedHardwareIds.Should().HaveCount(500);
    }
}

public sealed class OemSupportCatalogTests
{
    [Fact]
    public void Find_ReturnsOfficialHttpsUrlForKnownVendor()
    {
        var entry = OemSupportCatalog.Find(new MotherboardInfo { Manufacturer = "Gigabyte", Product = "B760M DS3H DDR4" });

        entry.Should().NotBeNull();
        entry!.SupportUrl.Scheme.Should().Be("https");
        entry.SupportUrl.Host.Should().EndWith("gigabyte.com");
    }

    [Fact]
    public void Find_ReturnsNullForUnknownVendorOrMissingBoard()
    {
        OemSupportCatalog.Find(new MotherboardInfo { Manufacturer = "Acme", Product = "X" }).Should().BeNull();
        OemSupportCatalog.Find(null).Should().BeNull();
    }
}

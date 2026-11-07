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

    [Fact]
    public void Default_HasOnlyWhqlEnabled()
    {
        var settings = new AppSettings();
        settings.OnlyWhql.Should().BeTrue();
        settings.Normalize().OnlyWhql.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Normalize_PreservesOnlyWhqlSetting(bool onlyWhql)
    {
        var settings = new AppSettings { OnlyWhql = onlyWhql };
        settings.Normalize().OnlyWhql.Should().Be(onlyWhql);
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
        entry.SupportUrl.AbsoluteUri.Should().Be(
            "https://www.gigabyte.com/search?keyword=B760M%20DS3H%20DDR4&type=All");
    }

    [Fact]
    public void Find_RecognizesGigabyteManufacturerReportedByWmi()
    {
        var entry = OemSupportCatalog.Find(new MotherboardInfo
        {
            Manufacturer = "Gigabyte Technology Co., Ltd.",
            Product = "B760M DS3H DDR4"
        });

        entry.Should().NotBeNull();
        entry!.Vendor.Should().Be("Gigabyte");
        entry.SupportUrl.AbsoluteUri.Should().Be(
            "https://www.gigabyte.com/search?keyword=B760M%20DS3H%20DDR4&type=All");
    }

    [Fact]
    public void Find_SearchesAsusOfficialSiteForExactModel()
    {
        var entry = OemSupportCatalog.Find(new MotherboardInfo
        {
            Manufacturer = "ASUSTeK COMPUTER INC.",
            Product = "TUF GAMING B650-PLUS"
        });

        entry.Should().NotBeNull();
        entry!.Vendor.Should().Be("ASUS");
        entry.SupportUrl.AbsoluteUri.Should().Be(
            "https://www.asus.com/searchresult?searchType=products&searchKey=TUF%20GAMING%20B650-PLUS&page=1");
    }

    [Theory]
    [InlineData("MSI", "MAG B650 TOMAHAWK WIFI", "www.msi.com")]
    [InlineData("ASRock", "B650M Pro RS", "www.asrock.com")]
    public void Find_IncludesExactModelInOfficialVendorSearch(string manufacturer, string model, string host)
    {
        var entry = OemSupportCatalog.Find(new MotherboardInfo
        {
            Manufacturer = manufacturer,
            Product = model
        });

        entry.Should().NotBeNull();
        entry!.SupportUrl.Scheme.Should().Be("https");
        entry.SupportUrl.Host.Should().Be(host);
        Uri.UnescapeDataString(entry.SupportUrl.Query).Should().Contain(model);
    }

    [Fact]
    public void Find_ReturnsNullForUnknownVendorOrMissingBoard()
    {
        OemSupportCatalog.Find(new MotherboardInfo { Manufacturer = "Acme", Product = "X" }).Should().BeNull();
        OemSupportCatalog.Find(new MotherboardInfo { Manufacturer = "Gigabyte", Product = " " }).Should().BeNull();
        OemSupportCatalog.Find(null).Should().BeNull();
    }
}

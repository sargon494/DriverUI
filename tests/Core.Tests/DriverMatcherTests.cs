using Core.Models;
using Core.Services;
using FluentAssertions;
using Xunit;

namespace Core.Tests;

public sealed class DriverMatcherTests
{
    private static InstalledDriver CreateInstalledDriver(
        string deviceId = "PCI\\VEN_8086&DEV_9A49",
        string driverVersion = "30.0.100.1000",
        params string[] hardwareIds)
    {
        var hwIds = hardwareIds.Length > 0
            ? hardwareIds.Select(id => new HardwareId { Value = id }).ToList()
            : new List<HardwareId> { new() { Value = "PCI\\VEN_8086&DEV_9A49&SUBSYS_12348086" } };

        return new InstalledDriver
        {
            DeviceId = deviceId,
            DeviceName = "Intel Iris Xe Graphics",
            Manufacturer = "Intel Corporation",
            DriverVersion = driverVersion,
            DriverDate = new DateTime(2023, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            InfName = "oem42.inf",
            HardwareIds = hwIds,
            Status = DriverStatus.Ok,
            DeviceClass = "Display"
        };
    }

    private static DriverUpdate CreateUpdate(
        string updateId,
        string hardwareId,
        string newVersion,
        DateTime? releaseDate = null)
    {
        return new DriverUpdate
        {
            UpdateId = updateId,
            Title = $"Intel Graphics Driver {newVersion}",
            Description = "Display driver update",
            NewVersion = newVersion,
            CurrentVersion = "30.0.100.1000",
            MatchedHardwareId = new HardwareId { Value = hardwareId },
            DeviceName = "Intel Iris Xe Graphics",
            DownloadSizeBytes = 500_000_000,
            IsMandatory = false,
            ReleaseDate = releaseDate ?? new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    [Fact]
    public void FindBestMatch_ReturnsNull_WhenCandidatesListIsEmptyOrNull()
    {
        var installed = CreateInstalledDriver();

        DriverMatcher.FindBestMatch(installed, Enumerable.Empty<DriverUpdate>()).Should().BeNull();
        DriverMatcher.FindBestMatch(installed, null).Should().BeNull();
    }

    [Fact]
    public void FindBestMatch_ReturnsCandidate_WhenSingleNewerCandidateMatchesHardwareId()
    {
        var installed = CreateInstalledDriver("PCI\\DEV_1", "30.0.0", "PCI\\VEN_8086&DEV_9A49");
        var update = CreateUpdate("upd-1", "PCI\\VEN_8086&DEV_9A49", "31.0.0");

        var best = DriverMatcher.FindBestMatch(installed, new[] { update });

        best.Should().NotBeNull();
        best!.UpdateId.Should().Be("upd-1");
    }

    [Fact]
    public void FindBestMatch_ReturnsHighestVersion_WhenMultipleValidCandidatesExist()
    {
        var installed = CreateInstalledDriver("PCI\\DEV_1", "30.0.0", "PCI\\VEN_8086&DEV_9A49");
        var update1 = CreateUpdate("upd-1", "PCI\\VEN_8086&DEV_9A49", "31.0.100");
        var update2 = CreateUpdate("upd-2", "PCI\\VEN_8086&DEV_9A49", "32.0.0");
        var update3 = CreateUpdate("upd-3", "PCI\\VEN_8086&DEV_9A49", "31.5.0");

        var best = DriverMatcher.FindBestMatch(installed, new[] { update1, update2, update3 });

        best.Should().NotBeNull();
        best!.UpdateId.Should().Be("upd-2");
        best.NewVersion.Should().Be("32.0.0");
    }

    [Fact]
    public void FindBestMatch_ReturnsNull_WhenCandidateVersionIsOlderOrEqualToCurrent()
    {
        var installed = CreateInstalledDriver("PCI\\DEV_1", "31.0.0", "PCI\\VEN_8086&DEV_9A49");
        var updateOld = CreateUpdate("upd-old", "PCI\\VEN_8086&DEV_9A49", "30.0.0");
        var updateSame = CreateUpdate("upd-same", "PCI\\VEN_8086&DEV_9A49", "31.0.0");

        var best = DriverMatcher.FindBestMatch(installed, new[] { updateOld, updateSame });

        best.Should().BeNull();
    }

    [Fact]
    public void FindBestMatch_MatchesHardwareId_CaseInsensitively()
    {
        var installed = CreateInstalledDriver("PCI\\DEV_1", "10.0.0", "pci\\ven_8086&dev_9a49");
        var update = CreateUpdate("upd-1", "PCI\\VEN_8086&DEV_9A49", "11.0.0");

        var best = DriverMatcher.FindBestMatch(installed, new[] { update });

        best.Should().NotBeNull();
        best!.UpdateId.Should().Be("upd-1");
    }

    [Fact]
    public void FindBestMatch_ReturnsNull_WhenCandidateHardwareIdDoesNotMatchAnyInstalled()
    {
        var installed = CreateInstalledDriver("PCI\\DEV_1", "10.0.0", "PCI\\VEN_8086&DEV_9A49");
        var updateOtherHardware = CreateUpdate("upd-other", "PCI\\VEN_10DE&DEV_2484", "12.0.0");

        var best = DriverMatcher.FindBestMatch(installed, new[] { updateOtherHardware });

        best.Should().BeNull();
    }

    [Fact]
    public void FindBestMatch_BreaksTieByLatestReleaseDate()
    {
        var installed = CreateInstalledDriver("PCI\\DEV_1", "10.0.0", "PCI\\VEN_8086&DEV_9A49");
        var updateOlderDate = CreateUpdate("upd-old-date", "PCI\\VEN_8086&DEV_9A49", "11.0.0", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var updateNewerDate = CreateUpdate("upd-new-date", "PCI\\VEN_8086&DEV_9A49", "11.0.0", new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var best = DriverMatcher.FindBestMatch(installed, new[] { updateOlderDate, updateNewerDate });

        best.Should().NotBeNull();
        best!.UpdateId.Should().Be("upd-new-date");
    }

    [Fact]
    public void FindAllMatches_MatchesMultipleDriversCorrectly()
    {
        var driver1 = CreateInstalledDriver("DEV1", "1.0", "HW1");
        var driver2 = CreateInstalledDriver("DEV2", "2.0", "HW2");
        var driver3 = CreateInstalledDriver("DEV3", "5.0", "HW3");

        var updates = new[]
        {
            CreateUpdate("upd-hw1", "HW1", "1.5"),
            CreateUpdate("upd-hw2", "HW2", "3.0"),
            CreateUpdate("upd-hw3-old", "HW3", "4.0"), // Older than driver3's 5.0
        };

        var matches = DriverMatcher.FindAllMatches(new[] { driver1, driver2, driver3 }, updates);

        matches.Should().HaveCount(2);
        matches[driver1].UpdateId.Should().Be("upd-hw1");
        matches[driver2].UpdateId.Should().Be("upd-hw2");
        matches.ContainsKey(driver3).Should().BeFalse();
    }
}

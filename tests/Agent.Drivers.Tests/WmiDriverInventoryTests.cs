using Agent.Drivers.Inventory;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Agent.Drivers.Tests;

[Trait("Category", "Integration")]
public sealed class WmiDriverInventoryTests
{
    [Fact]
    public async Task GetInstalledDriversAsync_ReturnsNonEmptyDriverList_OnWindows()
    {
        var inventory = new WmiDriverInventory(NullLogger<WmiDriverInventory>.Instance);

        var drivers = await inventory.GetInstalledDriversAsync(CancellationToken.None);

        drivers.Should().NotBeNull();
        drivers.Should().NotBeEmpty();

        var first = drivers[0];
        first.DeviceId.Should().NotBeNullOrWhiteSpace();
        first.DeviceName.Should().NotBeNullOrWhiteSpace();
        first.HardwareIds.Should().NotBeNull();
    }

    [Fact]
    public async Task GetInstalledDriversAsync_ThrowsOperationCanceledException_WhenCancelled()
    {
        var inventory = new WmiDriverInventory(NullLogger<WmiDriverInventory>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await inventory.GetInstalledDriversAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

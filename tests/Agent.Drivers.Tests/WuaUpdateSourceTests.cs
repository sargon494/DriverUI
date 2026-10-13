using Agent.Drivers.Inventory;
using Agent.Drivers.Updates;
using Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Agent.Drivers.Tests;

/// <summary>
/// Tests de integración y unitarios para <see cref="WuaUpdateSource"/>.
/// Los tests marcados con [Trait("Category", "Integration")] requieren un equipo Windows con WUA activo.
/// </summary>
public sealed class WuaUpdateSourceTests
{
    private readonly WuaUpdateSource _sut;

    public WuaUpdateSourceTests()
    {
        var logger = Substitute.For<ILogger<WuaUpdateSource>>();
        _sut = new WuaUpdateSource(logger);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task FindUpdatesAsync_Should_Return_Without_Throwing()
    {
        // Arrange — obtener inventario real de drivers instalados
        var inventoryLogger = Substitute.For<ILogger<WmiDriverInventory>>();
        var inventory = new WmiDriverInventory(inventoryLogger);
        var installedDrivers = await inventory.GetInstalledDriversAsync();

        // Act
        var updates = new List<DriverUpdate>();
        await foreach (var update in _sut.FindUpdatesAsync(installedDrivers))
        {
            updates.Add(update);
        }

        // Assert — es válido tener 0 actualizaciones si el equipo está al día
        updates.Should().NotBeNull();
        updates.Should().AllSatisfy(u =>
        {
            u.UpdateId.Should().NotBeNullOrWhiteSpace();
            u.Title.Should().NotBeNullOrWhiteSpace();
            u.MatchedHardwareId.Should().NotBeNull();
            u.MatchedHardwareId.Value.Should().NotBeNullOrWhiteSpace();
            u.NewVersion.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task FindUpdatesAsync_Should_Propagate_Cancellation()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var installedDrivers = Array.Empty<InstalledDriver>() as IReadOnlyList<InstalledDriver>;

        // Act & Assert
        var act = async () =>
        {
            await foreach (var _ in _sut.FindUpdatesAsync(installedDrivers, cts.Token))
            {
                // No debe llegar aquí
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task FindUpdatesAsync_Should_Throw_When_InstalledDrivers_Is_Null()
    {
        // Act & Assert
        var act = async () =>
        {
            await foreach (var _ in _sut.FindUpdatesAsync(null!, CancellationToken.None))
            {
                // No debe llegar aquí
            }
        };

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // --- Tests unitarios para la extracción de versión del título ---

    [Theory]
    [InlineData("Intel - Net - 12.18.9.10", "12.18.9.10")]
    [InlineData("NVIDIA - Display - 31.0.15.5190", "31.0.15.5190")]
    [InlineData("Realtek - MEDIA - 6.0.9600.17", "6.0.9600.17")]
    [InlineData("Advanced Micro Devices, Inc. - Display - 31.0.14051.5006", "31.0.14051.5006")]
    [InlineData("Intel(R) Wireless Bluetooth(R) - Bluetooth - 23.60.0.4", "23.60.0.4")]
    [InlineData("Intel Corporation - System - 10.1.37.6", "10.1.37.6")]
    public void ExtractVersionFromTitle_Should_Extract_Standard_Format(string title, string expected)
    {
        var result = WuaUpdateSource.ExtractVersionFromTitle(title);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void ExtractVersionFromTitle_Should_Return_Null_For_Empty_Input(string? title)
    {
        var result = WuaUpdateSource.ExtractVersionFromTitle(title);
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractVersionFromTitle_Should_Return_Null_When_No_Version_Found()
    {
        var result = WuaUpdateSource.ExtractVersionFromTitle("Driver update for Generic Device");
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractVersionFromTitle_Should_Handle_Fallback_Version_In_Text()
    {
        // Versión como token suelto (no separada por " - ")
        var result = WuaUpdateSource.ExtractVersionFromTitle("Update containing version 1.2.3.4 in text");
        result.Should().Be("1.2.3.4");
    }
}

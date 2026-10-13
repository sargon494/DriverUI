using Agent.Drivers.History;
using Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Agent.Drivers.Tests;

public sealed class SqliteOperationHistoryTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqliteOperationHistory _history;

    public SqliteOperationHistoryTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"driver_history_test_{Guid.NewGuid():N}.db");
        _history = new SqliteOperationHistory(_tempDbPath, NullLogger<SqliteOperationHistory>.Instance);
    }

    public void Dispose()
    {
        if (File.Exists(_tempDbPath))
        {
            try
            {
                File.Delete(_tempDbPath);
            }
            catch
            {
                // Ignorar en cleanup si el archivo está momentáneamente bloqueado
            }
        }
    }

    [Fact]
    public async Task GetAllAsync_Should_Return_Empty_When_No_Records()
    {
        var records = await _history.GetAllAsync();

        records.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordAsync_Should_Persist_And_Retrieve_Operation()
    {
        var entry = new OperationLogEntry
        {
            Id = 0,
            OperationType = "Install",
            UpdateId = "update-123",
            DeviceName = "Intel Wi-Fi 6 AX200",
            HardwareId = @"PCI\VEN_8086&DEV_2723",
            DriverTitle = "Intel Corporation - Net - 22.100.0.3",
            PreviousVersion = "22.90.0.1",
            NewVersion = "22.100.0.3",
            Success = true,
            ErrorMessage = null,
            Timestamp = DateTime.UtcNow
        };

        await _history.RecordAsync(entry);

        var allRecords = await _history.GetAllAsync();
        allRecords.Should().HaveCount(1);

        var retrieved = allRecords[0];
        retrieved.Id.Should().BeGreaterThan(0);
        retrieved.OperationType.Should().Be("Install");
        retrieved.UpdateId.Should().Be("update-123");
        retrieved.DeviceName.Should().Be("Intel Wi-Fi 6 AX200");
        retrieved.HardwareId.Should().Be(@"PCI\VEN_8086&DEV_2723");
        retrieved.DriverTitle.Should().Be("Intel Corporation - Net - 22.100.0.3");
        retrieved.PreviousVersion.Should().Be("22.90.0.1");
        retrieved.NewVersion.Should().Be("22.100.0.3");
        retrieved.Success.Should().BeTrue();
        retrieved.ErrorMessage.Should().BeNull();
        retrieved.Timestamp.Should().BeCloseTo(entry.Timestamp, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Correct_Record()
    {
        var entry1 = new OperationLogEntry
        {
            Id = 0,
            OperationType = "Install",
            UpdateId = "upd-1",
            DeviceName = "Device 1",
            HardwareId = "HID1",
            DriverTitle = "Title 1",
            PreviousVersion = "1.0",
            NewVersion = "1.1",
            Success = true,
            ErrorMessage = null,
            Timestamp = DateTime.UtcNow.AddMinutes(-5)
        };

        var entry2 = new OperationLogEntry
        {
            Id = 0,
            OperationType = "Rollback",
            UpdateId = "upd-2",
            DeviceName = "Device 2",
            HardwareId = "HID2",
            DriverTitle = "Title 2",
            PreviousVersion = "2.1",
            NewVersion = "2.0",
            Success = false,
            ErrorMessage = "Rollback failed: device busy",
            Timestamp = DateTime.UtcNow
        };

        await _history.RecordAsync(entry1);
        await _history.RecordAsync(entry2);

        var records = await _history.GetAllAsync();
        records.Should().HaveCount(2);

        var item2 = records.First(r => r.DeviceName == "Device 2");
        var retrieved = await _history.GetByIdAsync(item2.Id);

        retrieved.Should().NotBeNull();
        retrieved!.OperationType.Should().Be("Rollback");
        retrieved.Success.Should().BeFalse();
        retrieved.ErrorMessage.Should().Be("Rollback failed: device busy");
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Null_For_NonExistent_Id()
    {
        var record = await _history.GetByIdAsync(99999);

        record.Should().BeNull();
    }

    [Fact]
    public async Task RecordAsync_Should_Throw_When_Entry_Is_Null()
    {
        var act = () => _history.RecordAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}

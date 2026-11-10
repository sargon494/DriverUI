using Agent.Drivers.History;
using Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Integration.Tests;

/// <summary>
/// Historial SQLite real en un archivo temporal: esquema, orden y persistencia.
/// </summary>
[Trait("Category", "Integration")]
public class OperationHistoryIntegrationTests
{
    [Fact]
    public async Task Should_Persist_Entries_Across_History_Instances()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Root, "history.db");

        var writer = CreateHistory(path);
        await writer.RecordAsync(CreateEntry("Install", "upd-1", success: true));

        var reader = CreateHistory(path);
        var all = await reader.GetAllAsync();

        all.Should().ContainSingle().Which.UpdateId.Should().Be("upd-1");
    }

    [Fact]
    public async Task Should_Return_Newest_Entries_First()
    {
        using var temp = new TempDirectory();
        var history = CreateHistory(Path.Combine(temp.Root, "history.db"));
        var older = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc);

        await history.RecordAsync(CreateEntry("Install", "old", timestamp: older));
        await history.RecordAsync(CreateEntry("Install", "new", timestamp: newer));

        var all = await history.GetAllAsync();

        all.Select(e => e.UpdateId).Should().Equal("new", "old");
    }

    [Fact]
    public async Task Should_Round_Trip_Failure_Details_By_Id()
    {
        using var temp = new TempDirectory();
        var history = CreateHistory(Path.Combine(temp.Root, "history.db"));
        await history.RecordAsync(CreateEntry("Install", "upd-fail", success: false, error: "Firma no válida"));

        var stored = (await history.GetAllAsync()).Single();
        var fetched = await history.GetByIdAsync(stored.Id);

        fetched.Should().NotBeNull();
        fetched!.Success.Should().BeFalse();
        fetched.ErrorMessage.Should().Be("Firma no válida");
        fetched.OperationType.Should().Be("Install");
    }

    private static SqliteOperationHistory CreateHistory(string path) =>
        new(path, NullLogger<SqliteOperationHistory>.Instance);

    private static OperationLogEntry CreateEntry(
        string operationType,
        string updateId,
        bool success = true,
        string? error = null,
        DateTime? timestamp = null) => new()
    {
        Id = 0,
        OperationType = operationType,
        UpdateId = updateId,
        DeviceName = "Dispositivo de prueba",
        HardwareId = "PCI\\VEN_1000&DEV_0001",
        DriverTitle = "Controlador de prueba",
        PreviousVersion = "1.0.0.0",
        NewVersion = "2.0.0.0",
        Success = success,
        ErrorMessage = error,
        Timestamp = timestamp ?? DateTime.UtcNow
    };
}

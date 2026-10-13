using System.Globalization;
using Core.Interfaces;
using Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Agent.Drivers.History;

/// <summary>
/// Implementación de <see cref="IOperationHistory"/> respaldada por SQLite.
/// Persiste el historial de operaciones de instalación, rollback y escaneo
/// en un archivo de base de datos local.
/// </summary>
/// <remarks>
/// <para>Ruta predeterminada: <c>%ProgramData%\DriverUpdater\history.db</c>.</para>
/// <para>La tabla se crea automáticamente en la primera inicialización.</para>
/// <para>Esquema: <c>Id</c> (PK autoincrement), <c>OperationType</c>, <c>UpdateId</c>,
/// <c>DeviceName</c>, <c>HardwareId</c>, <c>DriverTitle</c>, <c>PreviousVersion</c>,
/// <c>NewVersion</c>, <c>Success</c>, <c>ErrorMessage</c>, <c>Timestamp</c> (ISO 8601).</para>
/// </remarks>
public sealed class SqliteOperationHistory : IOperationHistory
{
    private const string TableName = "operations";

    private readonly string _connectionString;
    private readonly ILogger<SqliteOperationHistory> _logger;

    /// <summary>
    /// Crea una nueva instancia del historial SQLite.
    /// El directorio del archivo se crea automáticamente si no existe.
    /// </summary>
    /// <param name="databasePath">Ruta absoluta al archivo SQLite.</param>
    /// <param name="logger">Logger para diagnóstico.</param>
    public SqliteOperationHistory(string databasePath, ILogger<SqliteOperationHistory> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        InitializeDatabase();
    }

    /// <inheritdoc/>
    public async Task RecordAsync(OperationLogEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO {TableName}
                (OperationType, UpdateId, DeviceName, HardwareId, DriverTitle,
                 PreviousVersion, NewVersion, Success, ErrorMessage, Timestamp)
            VALUES
                (@operationType, @updateId, @deviceName, @hardwareId, @driverTitle,
                 @previousVersion, @newVersion, @success, @errorMessage, @timestamp)
            """;

        command.Parameters.AddWithValue("@operationType", entry.OperationType);
        command.Parameters.AddWithValue("@updateId", entry.UpdateId);
        command.Parameters.AddWithValue("@deviceName", entry.DeviceName);
        command.Parameters.AddWithValue("@hardwareId", entry.HardwareId);
        command.Parameters.AddWithValue("@driverTitle", entry.DriverTitle);
        command.Parameters.AddWithValue("@previousVersion", entry.PreviousVersion);
        command.Parameters.AddWithValue("@newVersion", entry.NewVersion);
        command.Parameters.AddWithValue("@success", entry.Success ? 1 : 0);
        command.Parameters.AddWithValue("@errorMessage", (object?)entry.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("@timestamp", entry.Timestamp.ToString("O", CultureInfo.InvariantCulture));

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Operación registrada: {OperationType} — {DeviceName} ({Result})",
                entry.OperationType, entry.DeviceName, entry.Success ? "Éxito" : "Error");
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OperationLogEntry>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {TableName} ORDER BY Timestamp DESC";

        var results = new List<OperationLogEntry>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);

        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(MapEntry(reader));
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<OperationLogEntry?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {TableName} WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);

        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);

        if (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return MapEntry(reader);
        }

        return null;
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {TableName} (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                OperationType   TEXT    NOT NULL,
                UpdateId        TEXT    NOT NULL DEFAULT '',
                DeviceName      TEXT    NOT NULL,
                HardwareId      TEXT    NOT NULL DEFAULT '',
                DriverTitle     TEXT    NOT NULL,
                PreviousVersion TEXT    NOT NULL,
                NewVersion      TEXT    NOT NULL,
                Success         INTEGER NOT NULL,
                ErrorMessage    TEXT,
                Timestamp       TEXT    NOT NULL
            )
            """;
        command.ExecuteNonQuery();

        _logger.LogInformation("Base de datos de historial inicializada correctamente.");
    }

    private static OperationLogEntry MapEntry(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(reader.GetOrdinal("Id")),
        OperationType = reader.GetString(reader.GetOrdinal("OperationType")),
        UpdateId = reader.GetString(reader.GetOrdinal("UpdateId")),
        DeviceName = reader.GetString(reader.GetOrdinal("DeviceName")),
        HardwareId = reader.GetString(reader.GetOrdinal("HardwareId")),
        DriverTitle = reader.GetString(reader.GetOrdinal("DriverTitle")),
        PreviousVersion = reader.GetString(reader.GetOrdinal("PreviousVersion")),
        NewVersion = reader.GetString(reader.GetOrdinal("NewVersion")),
        Success = reader.GetInt64(reader.GetOrdinal("Success")) == 1,
        ErrorMessage = reader.IsDBNull(reader.GetOrdinal("ErrorMessage"))
            ? null
            : reader.GetString(reader.GetOrdinal("ErrorMessage")),
        Timestamp = DateTime.Parse(
            reader.GetString(reader.GetOrdinal("Timestamp")),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind)
    };
}

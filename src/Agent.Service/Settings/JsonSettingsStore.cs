using System.Text.Json;
using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Service.Settings;

/// <summary>
/// Guarda los ajustes en <c>%ProgramData%\DriverUpdater\config.json</c>.
/// La escritura es atómica (fichero temporal + reemplazo), así que un corte de energía
/// no deja el fichero a medias. Si el fichero está dañado, se usan los valores por defecto.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;
    private readonly ILogger<JsonSettingsStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonSettingsStore(ILogger<JsonSettingsStore> logger, string? path = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "DriverUpdater",
            "config.json");

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
    }

    public async Task<AppSettings> GetAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await LoadAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = settings.Normalize();

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var temporary = _path + ".tmp";
            await using (var stream = File.Create(temporary))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, ct).ConfigureAwait(false);
            }

            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<AppSettings> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
        {
            return new AppSettings().Normalize();
        }

        try
        {
            await using var stream = File.OpenRead(_path);
            var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, ct).ConfigureAwait(false);
            return (loaded ?? new AppSettings()).Normalize();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogWarning(ex, "config.json no es válido; se usan los ajustes por defecto.");
            return new AppSettings().Normalize();
        }
    }

    public void Dispose() => _gate.Dispose();
}

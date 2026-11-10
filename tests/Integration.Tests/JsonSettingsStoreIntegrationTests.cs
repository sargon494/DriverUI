using Agent.Service.Settings;
using Core.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Integration.Tests;

/// <summary>
/// Ajustes reales escritos en disco. Comprueba persistencia entre instancias
/// y que la carga nunca deja el servicio sin configuración válida.
/// </summary>
[Trait("Category", "Integration")]
public class JsonSettingsStoreIntegrationTests
{
    [Fact]
    public async Task Should_Return_Secure_Defaults_When_Config_File_Is_Missing()
    {
        using var temp = new TempDirectory();
        using var store = CreateStore(temp);

        var settings = await store.GetAsync();

        settings.OnlyWhql.Should().BeTrue("la política por defecto solo permite WHQL");
        settings.ExcludedHardwareIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Persist_Settings_Across_Store_Instances()
    {
        using var temp = new TempDirectory();
        var requested = new AppSettings
        {
            CheckIntervalHours = 24,
            OnlyWhql = false,
            ExcludedHardwareIds = ["PCI\\VEN_1000&DEV_0001"]
        };

        using (var writer = CreateStore(temp))
        {
            await writer.SaveAsync(requested);
        }

        using var reader = CreateStore(temp);
        var loaded = await reader.GetAsync();

        loaded.CheckIntervalHours.Should().Be(24);
        loaded.OnlyWhql.Should().BeFalse();
        loaded.ExcludedHardwareIds.Should().ContainSingle().Which.Should().Be("PCI\\VEN_1000&DEV_0001");
    }

    [Fact]
    public async Task Should_Normalize_Unknown_Interval_And_Blank_Exclusions_On_Save()
    {
        using var temp = new TempDirectory();
        using var store = CreateStore(temp);

        await store.SaveAsync(new AppSettings
        {
            CheckIntervalHours = 5,
            ExcludedHardwareIds = ["   ", "PCI\\VEN_1", "pci\\ven_1"]
        });

        var loaded = await store.GetAsync();

        loaded.CheckIntervalHours.Should().Be(0, "5 h no es un intervalo permitido");
        loaded.ExcludedHardwareIds.Should().ContainSingle("los duplicados y vacíos se eliminan");
    }

    [Fact]
    public async Task Should_Fall_Back_To_Defaults_When_Config_File_Is_Corrupt()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Root, "config.json");
        await File.WriteAllTextAsync(path, "{ esto no es json");
        using var store = CreateStore(temp);

        var settings = await store.GetAsync();

        settings.OnlyWhql.Should().BeTrue();
        settings.ExcludedHardwareIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Not_Leave_Temporary_File_After_Save()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Root, "config.json");
        using var store = CreateStore(temp);

        await store.SaveAsync(new AppSettings { CheckIntervalHours = 168 });

        File.Exists(path).Should().BeTrue();
        File.Exists(path + ".tmp").Should().BeFalse("la escritura atómica debe renombrar el temporal");
    }

    private static JsonSettingsStore CreateStore(TempDirectory temp) =>
        new(NullLogger<JsonSettingsStore>.Instance, Path.Combine(temp.Root, "config.json"));
}

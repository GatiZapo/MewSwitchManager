using System.Text.Json;
using MewSwitchManager.Models;

namespace MewSwitchManager.Infrastructure;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static AppConfig Load(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, "appsettings.json");
        if (!File.Exists(path)) return new AppConfig();

        try
        {
            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), Options) ?? new AppConfig();
            Normalize(config);
            return config;
        }
        catch
        {
            return new AppConfig();
        }
    }

    private static void Normalize(AppConfig config)
    {
        config.LinuxImage ??= new LinuxImageConfig();
        config.Storage ??= new StorageConfig();
        config.Safety ??= new SafetyConfig();
        config.Dependencies ??= new DependencyConfig();
        config.Ui ??= new UiConfig();
        config.Updates ??= new UpdateConfig();

        if (string.IsNullOrWhiteSpace(config.AppVersion))
            config.AppVersion = "0.3.0-alpha";
        if (string.IsNullOrWhiteSpace(config.LinuxImage.Url))
            config.LinuxImage.Url = new LinuxImageConfig().Url;
        if (string.IsNullOrWhiteSpace(config.LinuxImage.FileName))
            config.LinuxImage.FileName = new LinuxImageConfig().FileName;
        if (config.LinuxImage.ExpectedSizeBytes <= 0)
            config.LinuxImage.ExpectedSizeBytes = new LinuxImageConfig().ExpectedSizeBytes;
        if (string.IsNullOrWhiteSpace(config.LinuxImage.LinuxDistroVersion))
            config.LinuxImage.LinuxDistroVersion = new LinuxImageConfig().LinuxDistroVersion;
        if (string.IsNullOrWhiteSpace(config.Updates.Repository))
            config.Updates.Repository = "GatiZapo/MewSwitchManager";
    }
}

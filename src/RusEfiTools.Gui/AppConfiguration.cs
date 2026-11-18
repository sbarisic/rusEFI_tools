using System.Text.Json;

namespace RusEfiTools.Gui;

internal sealed class AppConfiguration
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    // Log-setting names and values can be added without inventing a schema now.
    public GuiConfiguration Gui { get; init; } = new();

    public Dictionary<string, JsonElement> Logs { get; init; } = [];

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "data_app", "Config.json");

    public static ConfigurationLoadResult Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new ConfigurationLoadResult(new AppConfiguration(), null);
            }
            return new ConfigurationLoadResult(Parse(File.ReadAllText(FilePath)), null);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return new ConfigurationLoadResult(new AppConfiguration(),
                $"Could not load data_app/Config.json: {exception.Message} Using defaults.");
        }
    }

    internal static AppConfiguration Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AppConfiguration();
        }

        var configuration = JsonSerializer.Deserialize<AppConfiguration>(json, JsonOptions)
            ?? throw new JsonException("Configuration must be a JSON object.");
        if (configuration.Logs == null)
        {
            throw new JsonException("Logs must be a JSON object, not null.");
        }
        if (configuration.Gui == null)
        {
            throw new JsonException("Gui must be an object, not null.");
        }
        configuration.Gui.Validate();
        return configuration;
    }
}

internal sealed record ConfigurationLoadResult(AppConfiguration Configuration, string? Error);

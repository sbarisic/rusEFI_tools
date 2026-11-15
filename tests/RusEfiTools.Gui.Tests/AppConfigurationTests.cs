using Xunit;

namespace RusEfiTools.Gui.Tests;

public sealed class AppConfigurationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("  \n ")]
    [InlineData("{}")]
    [InlineData("{\"_comment\":\"empty\"}")]
    public void EmptyConfigurationUsesDefaults(string json)
    {
        Assert.Empty(AppConfiguration.Parse(json).Logs);
    }

    [Fact]
    public void RetainsLogSettingsIncludingNestedValues()
    {
        var configuration = AppConfiguration.Parse("""
            {
                // Settings can be populated later.
                "logs": {
                    "example": { "enabled": true, "channels": ["RPM", "MAP"] },
                },
            }
            """);
        var example = configuration.Logs["example"];
        Assert.True(example.GetProperty("enabled").GetBoolean());
        Assert.Equal("MAP", example.GetProperty("channels")[1].GetString());
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"Logs\":null}")]
    [InlineData("{\"Logs\":42}")]
    public void RejectsMalformedSettings(string json)
    {
        Assert.Throws<System.Text.Json.JsonException>(() => AppConfiguration.Parse(json));
    }

    [Fact]
    public void ConfigurationPathIsRelativeToExecutableDirectory()
    {
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "data_app", "Config.json"), AppConfiguration.FilePath);
    }
}

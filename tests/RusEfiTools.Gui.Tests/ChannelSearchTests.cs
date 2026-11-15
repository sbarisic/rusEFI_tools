using Xunit;

namespace RusEfiTools.Gui.Tests;

public sealed class ChannelSearchTests
{
    [Theory]
    [InlineData("Air: Flow estimate", "air flow")]
    [InlineData("sd_airflow", "AIR FLOW")]
    [InlineData("tunedMassAirFlow", "air flow")]
    [InlineData("Air: Throttle flow estimate", "air flow")]
    [InlineData("Air: Flow estimate", "aflow")]
    [InlineData("Air: Flow estimate", "  AiR-FLOW  ")]
    [InlineData("RPM", "   ")]
    public void MatchesIgnoringCaseSeparatorsAndGaps(string name, string query)
    {
        Assert.NotNull(ChannelSearch.Score(name, query));
    }

    [Theory]
    [InlineData("Fuel: Flow rate", "air flow")]
    [InlineData("RPM", "air flow")]
    [InlineData("Flow Air", "air flow")]
    [InlineData("RPM", "%")]
    public void RejectsUnrelatedNames(string name, string query)
    {
        Assert.Null(ChannelSearch.Score(name, query));
    }

    [Fact]
    public void RanksContiguousMatchesAheadOfAbbreviations()
    {
        Assert.True(ChannelSearch.Score("Air: Flow estimate", "air flow")
            < ChannelSearch.Score("Air: Throttle flow estimate", "air flow"));
    }
}

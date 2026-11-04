using FluentAssertions;
using FormFlow.Data.Services;

namespace FormFlow.Data.Tests.Services;

public class ShareCodesTests
{
    [Theory]
    [InlineData("k7m2p9qa", "k7m2p9qa")]
    [InlineData("K7M2P9QA", "k7m2p9qa")]
    [InlineData("  k7m2 p9qa ", "k7m2p9qa")]
    [InlineData("K7M2-P9QA", "k7m2p9qa")]
    [InlineData("https://formflow.example/s/K7M2P9QA", "k7m2p9qa")]
    [InlineData("formflow.example/s/k7m2p9qa?utm=qr", "k7m2p9qa")]
    [InlineData("http://localhost:3000/#/s/k7m2p9qa", "k7m2p9qa")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Normalize_GivesTheStoredFormOfWhatWasTyped(string? typed, string expected)
    {
        ShareCodes.Normalize(typed).Should().Be(expected);
    }

    [Theory]
    [InlineData("K7M2 P9QA", true)]
    [InlineData("k7m2p9q", false)]
    [InlineData("k7m2p9qaa", false)]
    [InlineData("k7m2p9q0", false)] // no zero, it reads like an o
    [InlineData("k7m2p9ql", false)] // no l, it reads like a 1
    public void LooksValid_ChecksLengthAndAlphabet(string typed, bool valid)
    {
        ShareCodes.LooksValid(typed).Should().Be(valid);
    }

    [Fact]
    public void Display_UsesCapitalsInTwoGroups()
    {
        ShareCodes.Display("k7m2p9qa").Should().Be("K7M2 P9QA");
        ShareCodes.Display("short").Should().Be("SHORT");
    }

    [Fact]
    public void TheAlphabet_HasNoCharactersThatLookAlike()
    {
        ShareCodes.Alphabet.Should().NotContainAny("0", "o", "1", "l", "i");
        ShareCodes.Alphabet.Should().Be(ShareCodes.Alphabet.ToLowerInvariant());
    }
}

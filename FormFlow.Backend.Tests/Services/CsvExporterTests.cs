using FluentAssertions;
using FormFlow.Backend.Services;

namespace FormFlow.Backend.Tests.Services
{
    public class CsvExporterTests
    {
        [Theory]
        [InlineData("plain", "plain")]
        [InlineData("a,b", "\"a,b\"")]
        [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
        [InlineData("line\nbreak", "\"line\nbreak\"")]
        [InlineData("=SUM(A1)", "'=SUM(A1)")]
        [InlineData("-5", "-5")]
        [InlineData("", "")]
        public void Escape_QuotesAndNeutralizesFormulas(string input, string expected)
        {
            CsvExporter.Escape(input).Should().Be(expected);
        }
    }
}

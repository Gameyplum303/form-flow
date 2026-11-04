using FormFlow.Data.Models;

namespace FormFlow.Blazor.Components.Shared
{
    /// <summary>One bar of <see cref="OptionBars"/>: an answer, how often it was picked, and that as a percentage.</summary>
    public record OptionBar(string Label, int Count, double Percent)
    {
        /// <summary>Bars for option counts, each as a percentage of <paramref name="total"/> (none when it is zero).</summary>
        public static IEnumerable<OptionBar> Of(IEnumerable<OptionCount> options, int total) =>
            options.Select(o => new OptionBar(o.Label, o.Count, total == 0 ? 0 : o.Count * 100.0 / total));

        /// <summary>Bars for the summary respondents see, with the percentages the API worked out.</summary>
        public static IEnumerable<OptionBar> Of(IEnumerable<RespondentOptionResult> options) =>
            options.Select(o => new OptionBar(o.Label, o.Count, o.Percent));
    }
}

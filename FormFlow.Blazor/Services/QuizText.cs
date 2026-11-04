using System.Globalization;

namespace FormFlow.Blazor.Services
{
    /// <summary>How the quiz pages write points: "7.5 of 10 points", never "7.50" or "1 points".</summary>
    public static class QuizText
    {
        /// <summary>A number of points without trailing zeros, such as "2", "7.5" or "0.33".</summary>
        public static string Points(decimal points) => points.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>Points with their unit, such as "2 points" or "1 point".</summary>
        public static string PointsLabel(decimal points) => $"{Points(points)} {(points == 1 ? "point" : "points")}";

        /// <summary>A score out of a maximum, such as "8 of 10 points" or "1 of 1 point".</summary>
        public static string Score(decimal score, decimal max) => $"{Points(score)} of {PointsLabel(max)}";
    }
}

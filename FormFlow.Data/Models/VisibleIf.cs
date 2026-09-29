namespace FormFlow.Data.Models
{
    /// <summary>Shows a question only when the yes/no question named by <see cref="Key"/> has the answer <see cref="ShouldEqual"/>.</summary>
    public class VisibleIf
    {
        public required string Key { get; set; }

        public bool ShouldEqual { get; set; }
    }
}
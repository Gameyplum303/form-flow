namespace FormFlow.Data.Services
{
    /// <summary>
    /// The short codes in share links (/s/{code}), which students can also type on the Join page.
    /// </summary>
    public static class ShareCodes
    {
        /// <summary>
        /// The characters a code is made of. There is no 0, o, 1, l or i, so a code read off a projector
        /// or copied by hand comes out right.
        /// </summary>
        public const string Alphabet = "23456789abcdefghjkmnpqrstuvwxyz";

        public const int Length = 8;

        /// <summary>
        /// Turns what someone typed or pasted into the stored form of a code: lowercase, without spaces or
        /// dashes, and without the rest of a share link around it. Returns an empty string for nothing.
        /// </summary>
        public static string Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var text = input.Trim();
            var linkStart = text.LastIndexOf("/s/", StringComparison.OrdinalIgnoreCase);
            if (linkStart >= 0)
            {
                text = text[(linkStart + 3)..];
            }
            var end = text.IndexOfAny(['/', '?', '#']);
            if (end >= 0)
            {
                text = text[..end];
            }

            return new string(text.Where(c => !char.IsWhiteSpace(c) && c != '-').Select(char.ToLowerInvariant).ToArray());
        }

        /// <summary>Whether the text, once normalized, has the length and characters of a share code.</summary>
        public static bool LooksValid(string? input)
        {
            var code = Normalize(input);
            return code.Length == Length && code.All(Alphabet.Contains);
        }

        /// <summary>A code as people see it: in capitals, in two groups of four, as in "K7M2 P9QA".</summary>
        public static string Display(string? code)
        {
            var normalized = Normalize(code).ToUpperInvariant();
            return normalized.Length == Length ? $"{normalized[..4]} {normalized[4..]}" : normalized;
        }
    }
}

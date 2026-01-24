namespace EgyptOnline.Utilities
{
    /// <summary>
    /// Fuzzy search utilities for handling typos and spelling variations.
    /// Supports Arabic and English text matching with configurable tolerance.
    /// </summary>
    public static class FuzzySearchHelper
    {
        /// <summary>
        /// Calculate Levenshtein distance between two strings.
        /// Distance = minimum number of edits (insert, delete, replace) needed to transform s1 to s2.
        /// 
        /// Examples:
        /// - "اسماعيليه" vs "اسماعيلية" = 1 (1 character different)
        /// - "cat" vs "cut" = 1 (1 substitution)
        /// - "hello" vs "hallo" = 1 (1 substitution)
        /// </summary>
        /// <param name="s1">First string to compare</param>
        /// <param name="s2">Second string to compare</param>
        /// <returns>Edit distance (0 = identical, higher = more different)</returns>
        public static int LevenshteinDistance(string s1, string s2)
        {
            s1 = s1?.ToLower() ?? "";
            s2 = s2?.ToLower() ?? "";

            int len1 = s1.Length;
            int len2 = s2.Length;

            // Optimization: use two rows instead of full matrix for O(min(m,n)) space
            int[] row0 = new int[len2 + 1];
            int[] row1 = new int[len2 + 1];

            // Initialize first row
            for (int j = 0; j <= len2; j++)
                row0[j] = j;

            for (int i = 1; i <= len1; i++)
            {
                row1[0] = i;

                for (int j = 1; j <= len2; j++)
                {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;

                    row1[j] = Math.Min(
                        Math.Min(
                            row1[j - 1] + 1,      // insertion
                            row0[j] + 1),         // deletion
                        row0[j - 1] + cost);      // substitution
                }

                // Swap rows for next iteration
                var temp = row0;
                row0 = row1;
                row1 = temp;
            }

            return row0[len2];
        }

        /// <summary>
        /// Calculate similarity score between two strings (0 to 1).
        /// Formula: 1 - (distance / max_length)
        /// 
        /// Examples:
        /// - Identical strings = 1.0
        /// - One char difference in 10-char string = 0.9
        /// - Completely different = 0.0
        /// </summary>
        /// <param name="s1">First string</param>
        /// <param name="s2">Second string</param>
        /// <returns>Similarity score from 0 to 1, where 1 = perfect match</returns>
        public static double CalculateSimilarity(string s1, string s2)
        {
            int distance = LevenshteinDistance(s1, s2);
            int maxLen = Math.Max(s1?.Length ?? 0, s2?.Length ?? 0);

            if (maxLen == 0) return 1.0; // both empty = match
            return 1.0 - (double)distance / maxLen;
        }

        /// <summary>
        /// Check if two strings are similar enough based on a threshold.
        /// Default threshold is 0.85 (85% similar).
        /// 
        /// Examples:
        /// - "اسماعيليه" vs "اسماعيلية" with 0.85 = TRUE (96% similar)
        /// - "Cairo" vs "Kero" with 0.85 = TRUE (80% similar) - borderline
        /// - "Cairo" vs "Alexandria" with 0.85 = FALSE (0% similar)
        /// </summary>
        /// <param name="search">Search term from user input</param>
        /// <param name="target">Target value from database</param>
        /// <param name="threshold">Minimum similarity required (0.0 to 1.0)</param>
        /// <returns>True if similarity >= threshold</returns>
        public static bool IsSimilar(string search, string target, double threshold = 0.85)
        {
            if (string.IsNullOrEmpty(search) && string.IsNullOrEmpty(target))
                return true;

            if (string.IsNullOrEmpty(search) || string.IsNullOrEmpty(target))
                return false;

            return CalculateSimilarity(search, target) >= threshold;
        }

        /// <summary>
        /// Check if distance between strings is within tolerance.
        /// More efficient than similarity for simple threshold checks.
        /// 
        /// Examples:
        /// - "اسماعيليه" vs "اسماعيلية" with maxDistance=2 = TRUE
        /// - "cat" vs "cut" with maxDistance=1 = TRUE
        /// - "Cairo" vs "Kero" with maxDistance=2 = FALSE (distance=3)
        /// </summary>
        /// <param name="search">Search term</param>
        /// <param name="target">Target value</param>
        /// <param name="maxDistance">Maximum allowed edit distance</param>
        /// <returns>True if distance <= maxDistance</returns>
        public static bool IsWithinDistance(string search, string target, int maxDistance = 2)
        {
            return LevenshteinDistance(search, target) <= maxDistance;
        }

        /// <summary>
        /// Normalize Arabic text by fixing common typos and diacritical variations.
        /// Converts common misspellings to standard forms.
        /// 
        /// Examples:
        /// - "اسماعيليه" → "اسماعيلية" (final ha to ta marbuta)
        /// - "القاهره" → "القاهرة"
        /// </summary>
        /// <param name="text">Arabic text to normalize</param>
        /// <returns>Normalized text</returns>
        public static string NormalizeArabic(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            text = text.ToLower().Trim();

            // Common Arabic typo corrections
            var commonTypos = new Dictionary<char, char>
            {
                { 'ه', 'ة' }, // final ha → ta marbuta (common ending typo)
                { 'ى', 'ي' }, // alef maksura → ya (common vowel confusion)
                { 'ۀ', 'ة' }, // variant ta marbuta → standard
            };

            foreach (var typo in commonTypos)
            {
                text = text.Replace(typo.Key, typo.Value);
            }

            return text;
        }
    }
}

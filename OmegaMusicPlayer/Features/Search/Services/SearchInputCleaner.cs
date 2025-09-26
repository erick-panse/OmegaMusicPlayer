using System.Text.RegularExpressions;

namespace OmegaMusicPlayer.Features.Search.Services
{
    /// <summary>
    /// Service to clean and validate search input to prevent security issues
    /// </summary>
    public class SearchInputCleaner
    {
        private const int MAX_SEARCH_LENGTH = 100;
        private const int MIN_SEARCH_LENGTH = 1;

        // Regex to match potentially dangerous patterns
        private static readonly Regex DangerousPatterns = new(
            @"[<>""'&\x00-\x1f\x7f-\x9f]|" + // HTML/XML chars and control chars
            @"(script|javascript|vbscript|onload|onerror|eval|expression)" + // Script-related terms
            @"|(drop|delete|insert|update|union|select|exec|sp_|xp_)", // SQL terms (extra protection)
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Regex for excessive special characters that might cause performance issues
        private static readonly Regex ExcessiveSpecialChars = new(
            @"[^\w\s\-'.()[\]{}!@#$%^&*+=|\\:;,?/~`]{3,}",
            RegexOptions.Compiled);

        /// <summary>
        /// Cleans search input to prevent various security issues
        /// </summary>
        /// <param name="input">Raw search input</param>
        /// <returns>Cleaned search string, or null if input is invalid</returns>
        public string CleanSearchInput(string input)
        {
            // Handle null or empty input
            if (string.IsNullOrEmpty(input))
                return null;

            // Trim whitespace
            string cleaned = input.Trim();

            // Check length constraints
            if (cleaned.Length < MIN_SEARCH_LENGTH || cleaned.Length > MAX_SEARCH_LENGTH)
                return null;

            // Remove dangerous patterns
            if (DangerousPatterns.IsMatch(cleaned))
            {
                // Instead of rejecting, remove dangerous parts
                cleaned = DangerousPatterns.Replace(cleaned, "");
                cleaned = cleaned.Trim();

                // Check if anything meaningful remains
                if (cleaned.Length < MIN_SEARCH_LENGTH)
                    return null;
            }

            // Handle excessive special characters
            if (ExcessiveSpecialChars.IsMatch(cleaned))
            {
                cleaned = ExcessiveSpecialChars.Replace(cleaned, " ");
                cleaned = cleaned.Trim();
            }

            // Normalize whitespace (replace multiple spaces with single space)
            cleaned = Regex.Replace(cleaned, @"\s+", " ");

            // Final length check after cleaning
            if (cleaned.Length < MIN_SEARCH_LENGTH)
                return null;

            return cleaned;
        }

        /// <summary>
        /// Validates if a search query is potentially safe and meaningful
        /// </summary>
        /// <param name="query">Search query to validate</param>
        /// <returns>True if the query appears safe and meaningful</returns>
        public bool IsValidSearchQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return false;

            var cleaned = CleanSearchInput(query);
            return !string.IsNullOrEmpty(cleaned);
        }

        /// <summary>
        /// Escapes special characters for display purposes (prevents XSS in UI)
        /// </summary>
        /// <param name="input">Input to escape</param>
        /// <returns>HTML-escaped string safe for display</returns>
        public string EscapeForDisplay(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        /// <summary>
        /// Creates a safe search summary for logging/display
        /// </summary>
        /// <param name="originalQuery">Original search query</param>
        /// <param name="cleanedQuery">Cleaned query</param>
        /// <returns>Safe summary string</returns>
        public string CreateSearchSummary(string originalQuery, string cleanedQuery)
        {
            if (string.IsNullOrEmpty(cleanedQuery))
                return "Invalid search query";

            var escapedQuery = EscapeForDisplay(cleanedQuery);

            if (originalQuery?.Length != cleanedQuery?.Length)
                return $"Search: '{escapedQuery}' (cleaned)";

            return $"Search: '{escapedQuery}'";
        }
    }
}
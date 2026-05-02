using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.Inglenook.Providers;

/// <summary>
/// Shared helpers for remote metadata providers.
/// </summary>
internal static partial class RemoteProviderHelper
{
    /// <summary>
    /// Gets the dynamic order for a provider based on the RemoteProviderPriority configuration.
    /// </summary>
    public static int GetProviderOrder(string providerKey)
    {
        var priority = Plugin.Instance?.Configuration.RemoteProviderPriority ?? "audnexus,googlebooks";
        var parts = priority.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.Equals(parts[i], providerKey, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 100; // Not listed = low priority
    }

    /// <summary>
    /// Strips HTML tags from a string.
    /// </summary>
    public static string StripHtml(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        return HtmlTagRegex().Replace(html, string.Empty).Trim();
    }

    /// <summary>
    /// Parses a year from a date string like "2021-05-04" or "2021".
    /// </summary>
    public static int? ParseYear(string? dateString)
    {
        if (string.IsNullOrEmpty(dateString))
        {
            return null;
        }

        if (dateString.Length >= 4 && int.TryParse(dateString[..4], out var year) && year > 0 && year < 3000)
        {
            return year;
        }

        return null;
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();
}

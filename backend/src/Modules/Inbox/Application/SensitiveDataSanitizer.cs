using System.Text.RegularExpressions;

namespace Nordiska.Modules.Inbox.Application;

/// <summary>
/// Utility to mask and sanitize sensitive customer information (Swedish personal identity numbers / personnummer)
/// from support inquiries and chat messages.
/// </summary>
public static partial class SensitiveDataSanitizer
{
    /// <summary>
    /// Sanitizes the provided text by masking the last four digits of Swedish personal identity numbers.
    /// Returns the original text if empty or null.
    /// </summary>
    public static string Sanitize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text ?? string.Empty;
        }

        var result = text;

        // 1. Mask 12-digit Swedish Personal Identity Numbers (19YYMMDD-XXXX or 20YYMMDD-XXXX)
        result = Personnummer12Pattern().Replace(result, "$1$2$3-****");

        // 2. Mask 10-digit Swedish Personal Identity Numbers (YYMMDD-XXXX)
        result = Personnummer10Pattern().Replace(result, "$1$2$3-****");

        return result;
    }

    [GeneratedRegex(@"\b(19\d\d|20\d\d)(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])[- ]?(\d{4})\b")]
    private static partial Regex Personnummer12Pattern();

    [GeneratedRegex(@"\b(\d{2})(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])[- ]?(\d{4})\b")]
    private static partial Regex Personnummer10Pattern();
}

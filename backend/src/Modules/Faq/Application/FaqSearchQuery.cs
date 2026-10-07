using System.Text.RegularExpressions;

namespace Nordiska.Modules.Faq.Application;

public static class FaqSearchQuery
{
    private static readonly Regex Email = new(
        @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
        RegexOptions.Compiled);

    // 10 or 12 digits, with or without - or +, e.g. 900101-1234 or 199001011234
    private static readonly Regex PersonalNumber = new(
        @"(?<!\d)(\d{2})?\d{6}[-+]?\d{4}(?!\d)",
        RegexOptions.Compiled);

    // Swedish mobile/landline, e.g. 070-123 45 67, 0701234567 or +46 70 123 45 67
    private static readonly Regex PhoneNumber = new(
        @"(?<!\d)(\+46|0)[\s-]?\d{1,3}([\s-]?\d){5,8}(?!\d)",
        RegexOptions.Compiled);

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    // A phone number written as 10 digits in a row also matches the personal number pattern, it's masked either way
    public static string Mask(string query)
    {
        var masked = Email.Replace(query, "[email]");
        masked = PersonalNumber.Replace(masked, "[personnummer]");
        masked = PhoneNumber.Replace(masked, "[telefon]");
        return masked;
    }

    public static string Normalize(string query)
        => Whitespace.Replace(query.Trim(), " ").ToLowerInvariant();
}

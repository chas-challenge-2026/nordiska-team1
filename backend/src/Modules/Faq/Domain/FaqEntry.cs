namespace Nordiska.Modules.Faq.Domain;

public sealed class FaqEntry
{
    public int Id { get; private set; }
    public string Language { get; private set; } = "sv";
    public string Question { get; private set; } = string.Empty;
    public string Answer { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Keywords { get; private set; } = string.Empty;
    public int HelpfulCount { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; private set; }

    private FaqEntry() { }

    public static FaqEntry Create(
        string question,
        string answer,
        string? category = null,
        string? keywords = null,
        string? language = "sv")
    {
        var entry = new FaqEntry
        {
            CreatedAt = DateTime.UtcNow
        };
        entry.SetLanguage(language);
        entry.ReviseEntry(question, answer, category, keywords);
        return entry;
    }

    public void SetLanguage(string? language)
    {
        var lang = string.IsNullOrWhiteSpace(language) ? "sv" : language.Trim().ToLowerInvariant();
        if (lang.Length > 10)
        {
            throw new ArgumentException("Language code cannot exceed 10 characters.", nameof(language));
        }
        Language = lang;
    }

    public void ReviseEntry(
        string question,
        string answer,
        string? category,
        string? keywords,
        string? language = null)
    {
        var validQuestion = ValidateText(
            question, 5, 500, nameof(question));

        var validAnswer = ValidateText(
            answer, 1, 2000, nameof(answer));

        var validCategory = ValidateText(
            category, 0, 200, nameof(category));

        var validKeywords = ValidateText(
            keywords, 0, 500, nameof(keywords));

        if (language != null)
        {
            SetLanguage(language);
        }

        Question = validQuestion;
        Answer = validAnswer;
        Category = validCategory;
        Keywords = validKeywords;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkHelpful()
    {
        HelpfulCount = checked(HelpfulCount + 1);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UnmarkHelpful()
    {
        HelpfulCount = Math.Max(0, HelpfulCount - 1);
        UpdatedAt = DateTime.UtcNow;
    }

    private static string ValidateText(
        string? value,
        int minimumLength,
        int maximumLength,
        string parameterName)
    {
        var text = (value ?? string.Empty).Trim();

        if (text.Length < minimumLength ||
            text.Length > maximumLength)
        {
            throw new ArgumentException(
                $"Must contain between {minimumLength} and " +
                $"{maximumLength} characters.",
                parameterName);
        }

        return text;
    }
}
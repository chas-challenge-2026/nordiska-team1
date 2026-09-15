namespace Nordiska.Modules.Faq.Domain;

public sealed class FaqEntry
{
    public int Id { get; private set; }

    public string Question { get; private set; } = string.Empty;
    public string Answer { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Keywords { get; private set; } = string.Empty;

    public int HelpfulCount { get; private set; }

    private FaqEntry(){}

    public static FaqEntry Create
    (
        string question,
        string answer,
        string? category = null,
        string? keywords = null
    )
    {
        var entry = new FaqEntry();
        entry.ReviseEntry(question, answer, category, keywords);
        return entry;
        
    }

    public void ReviseEntry
    (
        string question,
        string answer,
        string? category,
        string? keywords
    )
    {
        var validQuestion = ValidateText(
            question, 5, 500, nameof(question));

        var validAnswer = ValidateText(
            answer, 1, 2000, nameof(answer));

        var validCategory = ValidateText(
            category, 0, 200, nameof(category));

        var validKeywords = ValidateText(
            keywords, 0, 500, nameof(keywords));

        Question = validQuestion;
        Answer = validAnswer;
        Category = validCategory;
        Keywords = validKeywords;
    }

    public void MarkHelpful()
    {
        HelpfulCount = checked(HelpfulCount + 1);
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
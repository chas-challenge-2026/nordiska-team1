using Nordiska.Modules.Faq.Application;

namespace Nordiska.Modules.Faq.Tests;

public class FaqSearchQueryTests
{
    [Theory]
    [InlineData("ränta 900101-1234", "ränta [personnummer]")]
    [InlineData("ränta 9001011234", "ränta [personnummer]")]
    [InlineData("ränta 19900101-1234", "ränta [personnummer]")]
    [InlineData("ränta 199001011234", "ränta [personnummer]")]
    [InlineData("pension 400101+1234", "pension [personnummer]")]
    public void Mask_PersonalNumber_IsReplaced(string query, string expected)
    {
        Assert.Equal(expected, FaqSearchQuery.Mask(query));
    }

    [Fact]
    public void Mask_Email_IsReplaced()
    {
        Assert.Equal("glömt lösenord [email]", FaqSearchQuery.Mask("glömt lösenord anna.svensson@example.se"));
    }

    [Theory]
    [InlineData("ring mig 070-123 45 67")]
    [InlineData("ring mig 070-1234567")]
    [InlineData("ring mig +46 70 123 45 67")]
    [InlineData("ring mig +46701234567")]
    public void Mask_PhoneNumber_IsReplaced(string query)
    {
        var masked = FaqSearchQuery.Mask(query);

        Assert.StartsWith("ring mig [", masked);
        Assert.DoesNotMatch(@"\d{3}", masked);
    }

    [Theory]
    [InlineData("hur mycket ränta får jag")]
    [InlineData("spärra kort")]
    [InlineData("ränta 2026")]
    [InlineData("insättning 5000 kr")]
    public void Mask_NormalSearch_IsLeftAlone(string query)
    {
        Assert.Equal(query, FaqSearchQuery.Mask(query));
    }

    [Fact]
    public void Normalize_TrimsLowercasesAndCollapsesWhitespace()
    {
        Assert.Equal("hur får jag ränta", FaqSearchQuery.Normalize("  Hur   FÅR\tjag Ränta "));
    }
}

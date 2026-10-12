using Nordiska.Modules.Inbox.Application;
using Xunit;

namespace Nordiska.Modules.Inbox.Tests;

public sealed class SensitiveDataSanitizerTests
{
    [Theory]
    [InlineData("Mitt personnummer är 19850512-1234 kan ni hjälpa mig?", "Mitt personnummer är 19850512-**** kan ni hjälpa mig?")]
    [InlineData("Kolla upp 199012311234 tack", "Kolla upp 19901231-**** tack")]
    [InlineData("Pnr 850512-1234 här", "Pnr 850512-**** här")]
    [InlineData("Pnr 8505121234 här", "Pnr 850512-**** här")]
    public void Sanitize_ShouldMaskPersonalIdentityNumbers(string input, string expected)
    {
        var result = SensitiveDataSanitizer.Sanitize(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Sanitize_ShouldLeaveRegularTextUntouched()
    {
        var normalText = "Hej! Jag vill öppna ett sparkonto med kontonummer 1234-56789 och undrar vad räntan är.";
        var result = SensitiveDataSanitizer.Sanitize(normalText);
        Assert.Equal(normalText, result);
    }
}

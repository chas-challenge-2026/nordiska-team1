using System.Text.Json.Nodes;
using Nordiska.FrontendApi.Filters;
using Xunit;

namespace Nordiska.FrontendApi.IntegrationTests.Filters;

public class AuditActionFilterTests
{
    [Fact]
    public void SanitizeArguments_ShouldRedactNestedSensitiveFields()
    {
        // Arrange
        var requestDto = new
        {
            email = "anna@example.com",
            password = "SecretPassword123!",
            personalNum = "198202116050",
            nested = new
            {
                token = "secret-token-xyz",
                pinCode = "1234",
                regularField = "VisibleValue"
            }
        };

        var arguments = new Dictionary<string, object?>
        {
            ["request"] = requestDto,
            ["otherParam"] = "SafeData"
        };

        // Act
        var sanitized = AuditActionFilter.SanitizeArguments(arguments);

        // Assert
        Assert.NotNull(sanitized);
        var obj = sanitized as JsonObject;
        Assert.NotNull(obj);

        var requestObj = obj["request"] as JsonObject;
        Assert.NotNull(requestObj);

        Assert.Equal("anna@example.com", requestObj["email"]?.ToString());
        Assert.Equal("[REDACTED]", requestObj["password"]?.ToString());
        Assert.Equal("[REDACTED]", requestObj["personalNum"]?.ToString());

        var nestedObj = requestObj["nested"] as JsonObject;
        Assert.NotNull(nestedObj);
        Assert.Equal("[REDACTED]", nestedObj["token"]?.ToString());
        Assert.Equal("[REDACTED]", nestedObj["pinCode"]?.ToString());
        Assert.Equal("VisibleValue", nestedObj["regularField"]?.ToString());

        Assert.Equal("SafeData", obj["otherParam"]?.ToString());
    }
}

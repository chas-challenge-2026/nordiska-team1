using Nordiska.BuildingBlocks.Database.Errors;

namespace Nordiska.BuildingBlocks.Database.Tests.Errors;

public class ConflictExceptionTests
{
    [Fact]
    public void Ctor_SetsStatusCodeAndTitle()
    {
        var ex = new ConflictException("Account is already closed");

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal("Otillåten operation", ex.Title);
        Assert.Equal("Account is already closed", ex.Message);
    }
}

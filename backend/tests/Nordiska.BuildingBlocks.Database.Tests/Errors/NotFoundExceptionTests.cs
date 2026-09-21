using Nordiska.BuildingBlocks.Database.Errors;

namespace Nordiska.BuildingBlocks.Database.Tests.Errors;

public class NotFoundExceptionTests
{
    [Fact]
    public void Ctor_SetsStatusCodeAndTitle()
    {
        var ex = new NotFoundException("Customer 42 not found");

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal("Resurs hittades inte", ex.Title);
        Assert.Equal("Customer 42 not found", ex.Message);
    }
}

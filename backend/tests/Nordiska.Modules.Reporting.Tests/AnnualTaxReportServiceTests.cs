using Moq;
using Nordiska.Modules.Reporting.Application;
using Xunit;

namespace Nordiska.Modules.Reporting.Tests;

public sealed class AnnualTaxReportServiceTests
{
    [Fact]
    public async Task RequestAsync_CurrentOrFutureYear_IsRejectedBeforeReadingData()
    {
        var query = new Mock<IAnnualTaxCalculationQuery>();
        var repository = new Mock<IAnnualTaxReportRepository>();
        var service = new AnnualTaxReportService(
            query.Object,
            repository.Object,
            Mock.Of<ITaxReportPayloadHasher>(),
            new FixedTimeProvider(new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero)));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.RequestAsync(42, 2026, CancellationToken.None));

        query.VerifyNoOtherCalls();
        repository.VerifyNoOtherCalls();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

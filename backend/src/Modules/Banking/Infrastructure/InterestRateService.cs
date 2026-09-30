using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.Modules.Banking.Infrastructure;

/// <summary>
/// Service for cached interest rate operations. Delegates to IAccountTypeConfigService.
/// </summary>
public class InterestRateService : IInterestRateService
{
    private readonly IAccountTypeConfigService _configService;

    public InterestRateService(IAccountTypeConfigService configService)
    {
        _configService = configService;
    }

    public Task<IEnumerable<AccountTypeConfigResponse>> GetAllAsync(CancellationToken cancellationToken = default)
        => _configService.GetAllAsync(cancellationToken);
}

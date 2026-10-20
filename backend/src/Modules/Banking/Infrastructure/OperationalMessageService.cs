using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Mappers;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.Modules.Banking.Infrastructure;

public sealed class OperationalMessageService : IOperationalMessageService
{
    private const string ActiveCacheKey = "operational-messages:active";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly IOperationalMessageRepository _repo;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OperationalMessageService> _logger;

    public OperationalMessageService(
        IOperationalMessageRepository repo,
        IMemoryCache cache,
        ILogger<OperationalMessageService> logger)
    {
        _repo = repo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<OperationalMessageResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var messages = await _repo.GetAllAsync(cancellationToken);
        return messages.Select(x => x.ToResponse()).ToList();
    }

    public async Task<IEnumerable<OperationalMessageResponse>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(ActiveCacheKey, out List<OperationalMessageResponse>? cached) && cached is not null)
        {
            return cached;
        }

        var activeMessages = await _repo.GetActiveAsync(cancellationToken);
        var responses = activeMessages.Select(x => x.ToResponse()).ToList();

        _cache.Set(ActiveCacheKey, responses, CacheDuration);
        _logger.LogInformation("Cached {Count} active operational messages for {Duration}", responses.Count, CacheDuration);

        return responses;
    }

    public async Task<OperationalMessageResponse> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var message = await _repo.GetByIdAsync(id, cancellationToken);
        if (message is null)
            throw new NotFoundException($"Operational message with ID {id} was not found.");

        return message.ToResponse();
    }

    public async Task<OperationalMessageResponse> CreateAsync(CreateOperationalMessageRequest request, CancellationToken cancellationToken = default)
    {
        var entity = request.ToDomain();
        await _repo.CreateAsync(entity, cancellationToken);
        _cache.Remove(ActiveCacheKey);
        _logger.LogInformation("Created operational message {Id} '{TitleSv}' (cache invalidated)", entity.Id, entity.TitleSv);
        return entity.ToResponse();
    }

    public async Task<OperationalMessageResponse> UpdateAsync(long id, UpdateOperationalMessageRequest request, CancellationToken cancellationToken = default)
    {
        var message = await _repo.GetByIdAsync(id, cancellationToken);
        if (message is null)
            throw new NotFoundException($"Operational message with ID {id} was not found.");

        message.ApplyUpdate(request);
        await _repo.UpdateAsync(message, cancellationToken);
        _cache.Remove(ActiveCacheKey);
        _logger.LogInformation("Updated operational message {Id} '{TitleSv}' (cache invalidated)", message.Id, message.TitleSv);
        return message.ToResponse();
    }

    public async Task<OperationalMessageResponse> PatchAsync(long id, PatchOperationalMessageRequest request, CancellationToken cancellationToken = default)
    {
        var message = await _repo.GetByIdAsync(id, cancellationToken);
        if (message is null)
            throw new NotFoundException($"Operational message with ID {id} was not found.");

        message.ApplyPatch(request);
        await _repo.UpdateAsync(message, cancellationToken);
        _cache.Remove(ActiveCacheKey);
        _logger.LogInformation("Patched operational message {Id} (isActive={IsActive}) (cache invalidated)", message.Id, message.IsActive);
        return message.ToResponse();
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var deleted = await _repo.DeleteAsync(id, cancellationToken);
        if (!deleted)
            throw new NotFoundException($"Operational message with ID {id} was not found.");

        _cache.Remove(ActiveCacheKey);
        _logger.LogInformation("Deleted operational message {Id} (cache invalidated)", id);
    }
}

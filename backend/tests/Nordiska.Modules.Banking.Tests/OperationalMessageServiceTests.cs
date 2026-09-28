using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Domain;
using Nordiska.Modules.Banking.Infrastructure;
using Xunit;

namespace Nordiska.Modules.Banking.Tests;

public class OperationalMessageServiceTests
{
    private sealed class FakeOperationalMessageRepo : IOperationalMessageRepository
    {
        private readonly List<OperationalMessage> _store = new();
        private long _nextId = 1;

        public int GetActiveCalls { get; private set; }
        public int GetAllCalls { get; private set; }

        public FakeOperationalMessageRepo(IEnumerable<OperationalMessage>? initial = null)
        {
            if (initial != null)
            {
                foreach (var item in initial)
                {
                    if (item.Id == 0) item.Id = _nextId++;
                    _store.Add(item);
                }
            }
        }

        public Task<IEnumerable<OperationalMessage>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            GetActiveCalls++;
            var now = DateTime.UtcNow;
            var active = _store
                .Where(m => m.IsActive
                            && (m.StartDate == null || m.StartDate <= now)
                            && (m.EndDate == null || m.EndDate >= now))
                .OrderByDescending(m => m.Priority)
                .ThenByDescending(m => m.CreatedAt)
                .ToList();

            return Task.FromResult<IEnumerable<OperationalMessage>>(active);
        }

        public Task<IEnumerable<OperationalMessage>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            GetAllCalls++;
            var all = _store
                .OrderByDescending(m => m.Priority)
                .ThenByDescending(m => m.CreatedAt)
                .ToList();

            return Task.FromResult<IEnumerable<OperationalMessage>>(all);
        }

        public Task<OperationalMessage?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            var match = _store.FirstOrDefault(m => m.Id == id);
            return Task.FromResult(match);
        }

        public Task CreateAsync(OperationalMessage entity, CancellationToken cancellationToken = default)
        {
            if (entity.Id == 0) entity.Id = _nextId++;
            _store.Add(entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(OperationalMessage entity, CancellationToken cancellationToken = default)
        {
            var idx = _store.FindIndex(m => m.Id == entity.Id);
            if (idx >= 0) _store[idx] = entity;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            var count = _store.RemoveAll(m => m.Id == id);
            return Task.FromResult(count > 0);
        }
    }

    [Fact]
    public async Task GetActive_ReturnsOnlyActiveAndDateValid_OrderedByPriorityDesc()
    {
        var now = DateTime.UtcNow;
        var seed = new[]
        {
            new OperationalMessage
            {
                Id = 1,
                TitleSv = "Låg prio",
                TitleEn = "Low priority",
                MessageSv = "Info",
                MessageEn = "Info",
                Priority = 1,
                IsActive = true,
                CreatedAt = now.AddHours(-2)
            },
            new OperationalMessage
            {
                Id = 2,
                TitleSv = "Hög prio",
                TitleEn = "High priority",
                MessageSv = "Kritisk",
                MessageEn = "Critical",
                Priority = 10,
                IsActive = true,
                CreatedAt = now.AddHours(-1)
            },
            new OperationalMessage
            {
                Id = 3,
                TitleSv = "Inaktiv",
                TitleEn = "Inactive",
                MessageSv = "Inaktiv",
                MessageEn = "Inactive",
                Priority = 5,
                IsActive = false
            },
            new OperationalMessage
            {
                Id = 4,
                TitleSv = "Utgånget",
                TitleEn = "Expired",
                MessageSv = "Utgånget",
                MessageEn = "Expired",
                Priority = 8,
                IsActive = true,
                EndDate = now.AddDays(-1)
            }
        };

        var repo = new FakeOperationalMessageRepo(seed);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new OperationalMessageService(repo, cache, new TestLogger<OperationalMessageService>());

        var activeMessages = (await service.GetActiveAsync()).ToList();

        Assert.Equal(2, activeMessages.Count);
        Assert.Equal(2, activeMessages[0].Id); // Priority 10 first
        Assert.Equal(1, activeMessages[1].Id); // Priority 1 second
        Assert.Equal("Hög prio", activeMessages[0].Title.Sv);
        Assert.Equal("High priority", activeMessages[0].Title.En);
    }

    [Fact]
    public async Task GetActive_CachesResult_AndSubsequentCallsDoNotHitRepo()
    {
        var seed = new[]
        {
            new OperationalMessage { Id = 1, TitleSv = "T1", TitleEn = "E1", MessageSv = "M1", MessageEn = "ME1", IsActive = true }
        };

        var repo = new FakeOperationalMessageRepo(seed);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new OperationalMessageService(repo, cache, new TestLogger<OperationalMessageService>());

        var first = (await service.GetActiveAsync()).ToList();
        var second = (await service.GetActiveAsync()).ToList();

        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal(1, repo.GetActiveCalls); // Cached
    }

    [Fact]
    public async Task Create_InvalidatesCache_AndPersistsNewMessage()
    {
        var repo = new FakeOperationalMessageRepo();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new OperationalMessageService(repo, cache, new TestLogger<OperationalMessageService>());

        // Populate cache
        await service.GetActiveAsync();
        Assert.Equal(1, repo.GetActiveCalls);

        var request = new CreateOperationalMessageRequest(
            "Nytt meddelande",
            "New message",
            "Text sv",
            "Text en",
            "warning",
            true,
            5,
            null,
            null
        );

        var created = await service.CreateAsync(request);

        Assert.True(created.Id > 0);
        Assert.Equal("Nytt meddelande", created.Title.Sv);
        Assert.Equal("New message", created.Title.En);
        Assert.Equal("warning", created.Severity);
        Assert.Equal(5, created.Priority);

        // Next GetActive must re-fetch repo due to invalidation
        var active = (await service.GetActiveAsync()).ToList();
        Assert.Equal(2, repo.GetActiveCalls);
        Assert.Single(active);
    }

    [Fact]
    public async Task Update_ModifiesEntity_AndInvalidatesCache()
    {
        var seed = new[]
        {
            new OperationalMessage
            {
                Id = 1,
                TitleSv = "Gammal titel",
                TitleEn = "Old title",
                MessageSv = "Gammalt meddelande",
                MessageEn = "Old message",
                Severity = "info",
                IsActive = true,
                Priority = 0
            }
        };

        var repo = new FakeOperationalMessageRepo(seed);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new OperationalMessageService(repo, cache, new TestLogger<OperationalMessageService>());

        // Populate cache
        await service.GetActiveAsync();
        Assert.Equal(1, repo.GetActiveCalls);

        var updateReq = new UpdateOperationalMessageRequest(
            "Uppdaterad titel",
            "Updated title",
            "Uppdaterat meddelande",
            "Updated message",
            "critical",
            true,
            10,
            null,
            null
        );

        var updated = await service.UpdateAsync(1, updateReq);

        Assert.Equal("Uppdaterad titel", updated.Title.Sv);
        Assert.Equal("critical", updated.Severity);
        Assert.Equal(10, updated.Priority);

        // Verify cache invalidation
        var active = (await service.GetActiveAsync()).ToList();
        Assert.Equal(2, repo.GetActiveCalls);
        Assert.Equal("Uppdaterad titel", active[0].Title.Sv);
    }

    [Fact]
    public async Task Update_ThrowsNotFoundException_WhenEntityDoesNotExist()
    {
        var repo = new FakeOperationalMessageRepo();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new OperationalMessageService(repo, cache, new TestLogger<OperationalMessageService>());

        var updateReq = new UpdateOperationalMessageRequest(
            "Title", "Title", "Msg", "Msg", "info", true, 0, null, null);

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(999, updateReq));
    }

    [Fact]
    public async Task PatchStatus_UpdatesActiveAndPriority_AndInvalidatesCache()
    {
        var seed = new[]
        {
            new OperationalMessage
            {
                Id = 1,
                TitleSv = "Titel",
                TitleEn = "Title",
                MessageSv = "Meddelande",
                MessageEn = "Message",
                IsActive = true,
                Priority = 0
            }
        };

        var repo = new FakeOperationalMessageRepo(seed);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new OperationalMessageService(repo, cache, new TestLogger<OperationalMessageService>());

        var patchReq = new PatchOperationalMessageRequest(IsActive: false, Priority: 15);
        var patched = await service.PatchAsync(1, patchReq);

        Assert.False(patched.IsActive);
        Assert.Equal(15, patched.Priority);

        // GetActive should now return empty because IsActive is false
        var active = (await service.GetActiveAsync()).ToList();
        Assert.Empty(active);
    }

    [Fact]
    public async Task Delete_RemovesEntity_AndInvalidatesCache()
    {
        var seed = new[]
        {
            new OperationalMessage
            {
                Id = 1,
                TitleSv = "Titel",
                TitleEn = "Title",
                MessageSv = "Meddelande",
                MessageEn = "Message",
                IsActive = true
            }
        };

        var repo = new FakeOperationalMessageRepo(seed);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new OperationalMessageService(repo, cache, new TestLogger<OperationalMessageService>());

        await service.DeleteAsync(1);

        var all = (await service.GetAllAsync()).ToList();
        Assert.Empty(all);

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(1));
    }
}

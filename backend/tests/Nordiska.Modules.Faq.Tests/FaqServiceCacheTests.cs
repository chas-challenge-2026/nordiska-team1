using Microsoft.Extensions.Caching.Memory;
using Nordiska.Modules.Faq.Application;
using Nordiska.Modules.Faq.Contracts.Requests;
using Nordiska.Modules.Faq.Contracts.Responses;
using Nordiska.Modules.Faq.Domain;

namespace Nordiska.Modules.Faq.Tests;

public class FaqServiceCacheTests
{
    private class FakeFaqRepo : IFaqRepository
    {
        private readonly List<FaqEntryResponse> _store = new();
        private int _next = 1;

        public int GetByIdCalls { get; private set; }
        public int SearchCalls { get; private set; }

        public FakeFaqRepo(IEnumerable<FaqEntryResponse>? seed = null)
        {
            if (seed != null)
            {
                _store.AddRange(seed);
                _next = (_store.MaxBy(e => e.Id)?.Id ?? 0) + 1;
            }
        }

        public Task<FaqEntryResponse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            GetByIdCalls++;
            return Task.FromResult(_store.FirstOrDefault(e => e.Id == id));
        }

        public Task<int> CreateAsync(FaqEntry entry, CancellationToken cancellationToken = default)
        {
            var id = _next++;
            _store.Add(new FaqEntryResponse(id, entry.Question, entry.Answer, entry.Category, 0, entry.Keywords));
            return Task.FromResult(id);
        }

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.RemoveAll(e => e.Id == id) > 0);

        public Task<IReadOnlyCollection<FaqEntryResponse>> SearchAsync(SearchFaqRequest request, CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            IReadOnlyCollection<FaqEntryResponse> result = _store.ToList();
            return Task.FromResult(result);
        }
    }

    private static FaqEntryResponse Entry(int id) => new(id, $"Question {id}?", $"Answer {id}", "General", 0, "");

    private static FaqService CreateService(FakeFaqRepo repo)
        => new(repo, new MemoryCache(new MemoryCacheOptions()), new FaqCacheInvalidator());

    [Fact]
    public async Task GetById_SecondCall_IsServedFromCache()
    {
        var repo = new FakeFaqRepo(new[] { Entry(1) });
        var service = CreateService(repo);

        var first = await service.GetByIdAsync(1);
        var second = await service.GetByIdAsync(1);

        Assert.Equal(first, second);
        Assert.Equal(1, repo.GetByIdCalls);
    }

    [Fact]
    public async Task GetById_MissingEntry_IsNotCached()
    {
        var repo = new FakeFaqRepo();
        var service = CreateService(repo);

        await service.GetByIdAsync(1);
        await service.GetByIdAsync(1);

        Assert.Equal(2, repo.GetByIdCalls);
    }

    [Fact]
    public async Task Search_SameRequest_IsServedFromCache()
    {
        var repo = new FakeFaqRepo(new[] { Entry(1) });
        var service = CreateService(repo);

        await service.SearchAsync(new SearchFaqRequest("konto"));
        await service.SearchAsync(new SearchFaqRequest(" Konto "));

        Assert.Equal(1, repo.SearchCalls);
    }

    [Fact]
    public async Task Search_DifferentRequests_UseSeparateCacheEntries()
    {
        var repo = new FakeFaqRepo(new[] { Entry(1) });
        var service = CreateService(repo);

        await service.SearchAsync(new SearchFaqRequest("konto"));
        await service.SearchAsync(new SearchFaqRequest("konto", "Sparande"));
        await service.SearchAsync(new SearchFaqRequest(Keyword: "konto"));

        Assert.Equal(3, repo.SearchCalls);
    }

    [Fact]
    public async Task Create_InvalidatesCachedSearches()
    {
        var repo = new FakeFaqRepo(new[] { Entry(1) });
        var service = CreateService(repo);

        await service.SearchAsync(new SearchFaqRequest());
        await service.CreateAsync("How do I open an account?", "Log in and click open account.");
        var result = await service.SearchAsync(new SearchFaqRequest());

        Assert.Equal(2, repo.SearchCalls);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Delete_InvalidatesCachedEntriesAndSearches()
    {
        var repo = new FakeFaqRepo(new[] { Entry(1), Entry(2) });
        var service = CreateService(repo);

        await service.GetByIdAsync(1);
        await service.SearchAsync(new SearchFaqRequest());

        var deleted = await service.DeleteAsync(1);
        var entry = await service.GetByIdAsync(1);
        var result = await service.SearchAsync(new SearchFaqRequest());

        Assert.True(deleted);
        Assert.Null(entry);
        Assert.Equal(2, repo.GetByIdCalls);
        Assert.Equal(2, repo.SearchCalls);
        Assert.Single(result);
    }
}

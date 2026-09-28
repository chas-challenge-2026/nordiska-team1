using Microsoft.Extensions.Caching.Memory;
using Nordiska.BuildingBlocks.Database;
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
        public int QueryPagedCalls { get; private set; }
        public int GetCategoriesCalls { get; private set; }
        public int AdjustHelpfulCalls { get; private set; }
        public int PatchCalls { get; private set; }

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
            _store.Add(new FaqEntryResponse(
                id,
                entry.Question,
                entry.Answer,
                entry.Category,
                0,
                entry.Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                entry.Language,
                entry.CreatedAt,
                entry.UpdatedAt));
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

        public Task<PagedResult<FaqEntryResponse>> QueryPagedAsync(FaqQueryParameters parameters, CancellationToken cancellationToken = default)
        {
            QueryPagedCalls++;
            var query = _store.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(parameters.Lang))
            {
                query = query.Where(e => string.Equals(e.Lang, parameters.Lang, StringComparison.OrdinalIgnoreCase));
            }
            var items = query.ToList();
            return Task.FromResult(PagedResult<FaqEntryResponse>.Create(items, items.Count, parameters.Page, parameters.PageSize));
        }

        public Task<IReadOnlyList<string>> GetCategoriesAsync(string language, CancellationToken cancellationToken = default)
        {
            GetCategoriesCalls++;
            IReadOnlyList<string> categories = _store
                .Where(e => string.Equals(e.Lang, language, StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Category)
                .Distinct()
                .ToList();
            return Task.FromResult(categories);
        }

        public Task<FaqEntryResponse?> AdjustHelpfulAsync(int id, int delta, CancellationToken cancellationToken = default)
        {
            AdjustHelpfulCalls++;
            var index = _store.FindIndex(e => e.Id == id);
            if (index < 0) return Task.FromResult<FaqEntryResponse?>(null);

            var curr = _store[index];
            var newCount = Math.Max(0, curr.HelpfulCount + delta);
            var updated = curr with { HelpfulCount = newCount, UpdatedAt = DateTime.UtcNow };
            _store[index] = updated;
            return Task.FromResult<FaqEntryResponse?>(updated);
        }

        public Task<FaqEntryResponse?> PatchAsync(int id, PatchFaqRequest request, CancellationToken cancellationToken = default)
        {
            PatchCalls++;
            var index = _store.FindIndex(e => e.Id == id);
            if (index < 0) return Task.FromResult<FaqEntryResponse?>(null);

            var curr = _store[index];
            var updated = curr with
            {
                Question = request.Question ?? request.Title ?? curr.Question,
                Answer = request.Answer ?? curr.Answer,
                Category = request.Category ?? curr.Category,
                Lang = request.Lang ?? curr.Lang,
                UpdatedAt = DateTime.UtcNow
            };
            _store[index] = updated;
            return Task.FromResult<FaqEntryResponse?>(updated);
        }
    }

    private static FaqEntryResponse Entry(int id, string lang = "sv", string category = "General") =>
        new(id, $"Question {id}?", $"Answer {id}", category, 0, new[] { "tag1" }, lang, DateTime.UtcNow);

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
    public async Task GetByLanguagePaged_CachesResult()
    {
        var repo = new FakeFaqRepo(new[] { Entry(1, "sv"), Entry(2, "en") });
        var service = CreateService(repo);

        var res1 = await service.GetByLanguagePagedAsync(new FaqQueryParameters(Lang: "sv"));
        var res2 = await service.GetByLanguagePagedAsync(new FaqQueryParameters(Lang: "sv"));

        Assert.Equal(1, repo.QueryPagedCalls);
        Assert.Single(res1.Items);
        Assert.Equal("sv", res1.Items.First().Lang);
    }

    [Fact]
    public async Task GetCategories_CachesResult()
    {
        var repo = new FakeFaqRepo(new[] { Entry(1, "sv", "Ränta"), Entry(2, "sv", "Konto") });
        var service = CreateService(repo);

        var cats1 = await service.GetCategoriesAsync("sv");
        var cats2 = await service.GetCategoriesAsync("sv");

        Assert.Equal(1, repo.GetCategoriesCalls);
        Assert.Equal(2, cats1.Count);
    }

    [Fact]
    public async Task AdjustHelpful_IncreasesAndDecreases_WithNonNegativeGuard()
    {
        var repo = new FakeFaqRepo(new[] { Entry(1) });
        var service = CreateService(repo);

        var inc = await service.AdjustHelpfulAsync(1, 1);
        Assert.NotNull(inc);
        Assert.Equal(1, inc.HelpfulCount);

        var dec = await service.AdjustHelpfulAsync(1, -1);
        Assert.NotNull(dec);
        Assert.Equal(0, dec.HelpfulCount);

        // Decrease again below 0 should clamp to 0
        var dec2 = await service.AdjustHelpfulAsync(1, -1);
        Assert.NotNull(dec2);
        Assert.Equal(0, dec2.HelpfulCount);
    }

    [Fact]
    public async Task Create_InvalidatesCachedSearchesAndPaged()
    {
        var repo = new FakeFaqRepo(new[] { Entry(1) });
        var service = CreateService(repo);

        await service.SearchAsync(new SearchFaqRequest());
        await service.GetByLanguagePagedAsync(new FaqQueryParameters(Lang: "sv"));

        await service.CreateAsync("How do I open an account?", "Log in and click open account.", language: "en");

        var result = await service.SearchAsync(new SearchFaqRequest());
        var paged = await service.GetByLanguagePagedAsync(new FaqQueryParameters(Lang: "sv"));

        Assert.Equal(2, repo.SearchCalls);
        Assert.Equal(2, repo.QueryPagedCalls);
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

    [Fact]
    public void DomainModel_ReviseEntry_And_HelpfulnessGuards()
    {
        var faq = FaqEntry.Create("Vad är ränta?", "Ränta är avkastning.", "Ränta", "ränta, pengar", "sv");
        Assert.Equal("sv", faq.Language);
        Assert.Equal(0, faq.HelpfulCount);

        faq.MarkHelpful();
        Assert.Equal(1, faq.HelpfulCount);

        faq.UnmarkHelpful();
        Assert.Equal(0, faq.HelpfulCount);

        // Guard against negative
        faq.UnmarkHelpful();
        Assert.Equal(0, faq.HelpfulCount);

        faq.ReviseEntry("What is interest?", "Interest is yield.", "Interest", "interest, yield", "en");
        Assert.Equal("en", faq.Language);
        Assert.Equal("What is interest?", faq.Question);
        Assert.NotNull(faq.UpdatedAt);
    }
}


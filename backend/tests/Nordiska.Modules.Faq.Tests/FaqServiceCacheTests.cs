using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        public int GetRelatedCalls { get; private set; }
        public int GetPopularCalls { get; private set; }
        public Dictionary<Guid, List<Guid>> Relations { get; } = new();

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
                entry.RelationId,
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
            var newCount = curr.HelpfulCount + delta;
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

        public Task<IReadOnlyCollection<FaqEntryResponse>> GetByRelationIdAsync(Guid relationId, CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<FaqEntryResponse> result = _store.Where(e => e.RelationId == relationId).ToList();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<RelatedFaqResponse>> GetExplicitRelatedAsync(Guid relationId, string language, CancellationToken cancellationToken = default)
        {
            GetRelatedCalls++;
            var relatedIds = Relations.TryGetValue(relationId, out var ids) ? ids : new List<Guid>();
            IReadOnlyList<RelatedFaqResponse> result = relatedIds
                .Select(id => _store.FirstOrDefault(e => e.RelationId == id && e.Lang == language))
                .Where(e => e is not null)
                .Select(e => new RelatedFaqResponse(e!.Id, e.Question, e.Category))
                .ToList();
            return Task.FromResult(result);
        }

        // No view log in the fake, so popular is just helpful count
        public Task<IReadOnlyList<RelatedFaqResponse>> GetPopularInCategoryAsync(string language, string category, int excludeId, DateTime viewsSince, int count, CancellationToken cancellationToken = default)
        {
            GetPopularCalls++;
            IReadOnlyList<RelatedFaqResponse> result = _store
                .Where(e => e.Lang == language && e.Category == category && e.Id != excludeId)
                .OrderByDescending(e => e.HelpfulCount)
                .Take(count)
                .Select(e => new RelatedFaqResponse(e.Id, e.Question, e.Category))
                .ToList();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<Guid>> GetRelatedRelationIdsAsync(Guid relationId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Guid> result = Relations.TryGetValue(relationId, out var ids) ? ids.ToList() : new List<Guid>();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyCollection<Guid>> GetExistingRelationIdsAsync(IReadOnlyCollection<Guid> relationIds, CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<Guid> result = _store.Select(e => e.RelationId).Where(relationIds.Contains).Distinct().ToList();
            return Task.FromResult(result);
        }

        public Task SetRelatedAsync(Guid relationId, IReadOnlyList<Guid> relatedRelationIds, CancellationToken cancellationToken = default)
        {
            Relations[relationId] = relatedRelationIds.ToList();
            return Task.CompletedTask;
        }
    }

    private static FaqEntryResponse Entry(int id, string lang = "sv", string category = "General", Guid? relationId = null, int helpful = 0) =>
        new(id, $"Question {id}?", $"Answer {id}", category, helpful, new[] { "tag1" }, relationId ?? Guid.NewGuid(), lang, DateTime.UtcNow);

    private class FakeSearchLogRepo : IFaqSearchLogRepository
    {
        public Task AddRangeAsync(IReadOnlyCollection<FaqSearchLog> logs, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<FaqContentGapResponse>> GetContentGapsAsync(string? language, DateTime since, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FaqContentGapResponse>>(new List<FaqContentGapResponse>());

        public Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private static FaqSearchLogQueue CreateQueue()
        => new(new MemoryCache(new MemoryCacheOptions()), Options.Create(new FaqSearchLogOptions { Salt = "test-salt" }), NullLogger<FaqSearchLogQueue>.Instance);

    private static FaqViewLogQueue CreateViewQueue()
        => new(new MemoryCache(new MemoryCacheOptions()), Options.Create(new FaqSearchLogOptions { Salt = "test-salt" }), NullLogger<FaqViewLogQueue>.Instance);

    private static FaqService CreateService(FakeFaqRepo repo, FaqSearchLogQueue? queue = null, FaqViewLogQueue? viewQueue = null)
        => new(repo, new MemoryCache(new MemoryCacheOptions()), new FaqCacheInvalidator(), queue ?? CreateQueue(), new FakeSearchLogRepo(), viewQueue ?? CreateViewQueue());

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
    public async Task AdjustHelpful_IncreasesAndDecreases_AllowsNegativeCount()
    {
        var repo = new FakeFaqRepo(new[] { Entry(1) });
        var service = CreateService(repo);

        var inc = await service.AdjustHelpfulAsync(1, 1);
        Assert.NotNull(inc);
        Assert.Equal(1, inc.HelpfulCount);

        var dec = await service.AdjustHelpfulAsync(1, -1);
        Assert.NotNull(dec);
        Assert.Equal(0, dec.HelpfulCount);

        // Decrease again below 0 allows negative count
        var dec2 = await service.AdjustHelpfulAsync(1, -1);
        Assert.NotNull(dec2);
        Assert.Equal(-1, dec2.HelpfulCount);
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
    public async Task GetByLanguagePaged_CachedSearch_IsStillLogged()
    {
        var repo = new FakeFaqRepo(new[] { Entry(1, "sv") });
        var queue = CreateQueue();
        var service = CreateService(repo, queue);

        await service.GetByLanguagePagedAsync(new FaqQueryParameters(SearchTerm: "ränta", Lang: "sv"), "session-1");
        await service.GetByLanguagePagedAsync(new FaqQueryParameters(SearchTerm: "ränta", Lang: "sv"), "session-2");

        Assert.Equal(1, repo.QueryPagedCalls);
        Assert.Equal(2, queue.Reader.Count);
    }

    [Fact]
    public async Task GetByLanguagePaged_LaterPageOrNoSearch_IsNotLogged()
    {
        var repo = new FakeFaqRepo(new[] { Entry(1, "sv") });
        var queue = CreateQueue();
        var service = CreateService(repo, queue);

        await service.GetByLanguagePagedAsync(new FaqQueryParameters(Page: 2, SearchTerm: "ränta", Lang: "sv"), "session-1");
        await service.GetByLanguagePagedAsync(new FaqQueryParameters(Lang: "sv"), "session-1");

        Assert.Equal(0, queue.Reader.Count);
    }

    [Fact]
    public async Task GetWithRelated_ExplicitRelations_AreReturnedInOrderWithoutFallback()
    {
        var articleA = Guid.NewGuid();
        var articleB = Guid.NewGuid();
        var articleC = Guid.NewGuid();
        var repo = new FakeFaqRepo(new[]
        {
            Entry(1, relationId: articleA),
            Entry(2, relationId: articleB),
            Entry(3, relationId: articleC),
            Entry(4, helpful: 10)
        });
        repo.Relations[articleA] = new List<Guid> { articleC, articleB };
        var service = CreateService(repo);

        var entry = await service.GetWithRelatedAsync(1, "sv");

        Assert.NotNull(entry);
        Assert.Equal(new[] { 3, 2 }, entry.RelatedFaqs!.Select(r => r.Id));
        Assert.Equal(0, repo.GetPopularCalls);
    }

    [Fact]
    public async Task GetWithRelated_RelatedArticleMissingInLanguage_IsSkipped()
    {
        var articleA = Guid.NewGuid();
        var onlyEnglish = Guid.NewGuid();
        var both = Guid.NewGuid();
        var repo = new FakeFaqRepo(new[]
        {
            Entry(1, relationId: articleA),
            Entry(2, "en", relationId: onlyEnglish),
            Entry(3, relationId: both),
            Entry(4, "en", relationId: both)
        });
        repo.Relations[articleA] = new List<Guid> { onlyEnglish, both };
        var service = CreateService(repo);

        var entry = await service.GetWithRelatedAsync(1, "sv");

        Assert.Equal(new[] { 3 }, entry!.RelatedFaqs!.Select(r => r.Id));
    }

    [Fact]
    public async Task GetWithRelated_NoExplicitRelations_FallsBackToPopularInSameCategory()
    {
        var repo = new FakeFaqRepo(new[]
        {
            Entry(1, category: "Kort"),
            Entry(2, category: "Kort", helpful: 1),
            Entry(3, category: "Kort", helpful: 5),
            Entry(4, category: "Kort", helpful: 3),
            Entry(5, category: "Kort", helpful: 0),
            Entry(6, category: "Lån", helpful: 50),
            Entry(7, "en", category: "Kort", helpful: 50)
        });
        var service = CreateService(repo);

        var entry = await service.GetWithRelatedAsync(1, null);

        Assert.Equal(new[] { 3, 4, 2 }, entry!.RelatedFaqs!.Select(r => r.Id));
    }

    [Fact]
    public async Task GetWithRelated_NoCategory_ReturnsEmptyList()
    {
        var repo = new FakeFaqRepo(new[] { Entry(1, category: ""), Entry(2, category: "", helpful: 5) });
        var service = CreateService(repo);

        var entry = await service.GetWithRelatedAsync(1, "sv");

        Assert.Empty(entry!.RelatedFaqs!);
        Assert.Equal(0, repo.GetPopularCalls);
    }

    [Fact]
    public async Task GetWithRelated_WrongLanguage_ReturnsNull()
    {
        var repo = new FakeFaqRepo(new[] { Entry(1, "en") });
        var service = CreateService(repo);

        Assert.Null(await service.GetWithRelatedAsync(1, "sv"));
        Assert.NotNull(await service.GetWithRelatedAsync(1, "EN"));
    }

    [Fact]
    public async Task GetWithRelated_SecondCall_IsServedFromCacheUntilRelationsChange()
    {
        var articleA = Guid.NewGuid();
        var articleB = Guid.NewGuid();
        var repo = new FakeFaqRepo(new[] { Entry(1, relationId: articleA), Entry(2, relationId: articleB) });
        var service = CreateService(repo);

        await service.GetWithRelatedAsync(1, "sv");
        await service.GetWithRelatedAsync(1, "sv");
        Assert.Equal(1, repo.GetRelatedCalls);

        await service.SetRelatedAsync(articleA, new[] { articleB });
        var entry = await service.GetWithRelatedAsync(1, "sv");

        Assert.Equal(2, repo.GetRelatedCalls);
        Assert.Equal(new[] { 2 }, entry!.RelatedFaqs!.Select(r => r.Id));
    }

    [Fact]
    public async Task SetRelated_InvalidIds_Throw()
    {
        var articleA = Guid.NewGuid();
        var articleB = Guid.NewGuid();
        var repo = new FakeFaqRepo(new[] { Entry(1, relationId: articleA), Entry(2, relationId: articleB) });
        var service = CreateService(repo);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetRelatedAsync(articleA, new[] { articleA }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SetRelatedAsync(articleA, new[] { articleB, articleB }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SetRelatedAsync(articleA, new[] { Guid.NewGuid() }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SetRelatedAsync(articleA,
            Enumerable.Range(0, FaqService.MaxRelatedFaqs + 1).Select(_ => Guid.NewGuid()).ToList()));

        Assert.Empty(repo.Relations);
    }

    [Fact]
    public async Task SetRelated_UnknownArticle_ReturnsFalse()
    {
        var articleB = Guid.NewGuid();
        var repo = new FakeFaqRepo(new[] { Entry(1, relationId: articleB) });
        var service = CreateService(repo);

        Assert.False(await service.SetRelatedAsync(Guid.NewGuid(), new[] { articleB }));
        Assert.Null(await service.GetRelatedRelationIdsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RegisterView_QueuesExistingEntryOnly()
    {
        var repo = new FakeFaqRepo(new[] { Entry(1) });
        var viewQueue = CreateViewQueue();
        var service = CreateService(repo, viewQueue: viewQueue);

        Assert.True(await service.RegisterViewAsync(1, "session-1"));
        Assert.False(await service.RegisterViewAsync(2, "session-1"));

        Assert.Equal(1, viewQueue.Reader.Count);
    }

    [Fact]
    public void DomainModel_ReviseEntry_And_AllowsNegativeHelpfulCount()
    {
        var faq = FaqEntry.Create("Vad är ränta?", "Ränta är avkastning.", "Ränta", "ränta, pengar", "sv");
        Assert.Equal("sv", faq.Language);
        Assert.Equal(0, faq.HelpfulCount);

        faq.MarkHelpful();
        Assert.Equal(1, faq.HelpfulCount);

        faq.UnmarkHelpful();
        Assert.Equal(0, faq.HelpfulCount);

        // Can decrease to negative
        faq.UnmarkHelpful();
        Assert.Equal(-1, faq.HelpfulCount);

        faq.ReviseEntry("What is interest?", "Interest is yield.", "Interest", "interest, yield", "en");
        Assert.Equal("en", faq.Language);
        Assert.Equal("What is interest?", faq.Question);
        Assert.NotNull(faq.UpdatedAt);
    }
}


using ArturRios.Output.Tests.Mock;

namespace ArturRios.Output.Tests;

[Trait("Category", "Unit")]
public class PaginatedOutputExtensionsTests
{
    [Fact]
    public void GivenQueryableData_WhenPaginating_ThenReturnsPaginatedResult()
    {
        var data = new List<Item>
        {
            new() { Id = 2, Value = "b" }, new() { Id = 1, Value = "a" }, new() { Id = 3, Value = "c" }
        };

        var result = data.AsQueryable().Paginate(1, 2);

        Assert.NotNull(result);
        Assert.Equal(1, result.PageNumber);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(2, result.Data.First().Id);
    }

    [Fact]
    public void GivenQueryableData_WhenPaginatingWithOrderBy_ThenReturnsOrderedPaginatedResult()
    {
        var data = new List<Item>
        {
            new() { Id = 2, Value = "b" }, new() { Id = 1, Value = "a" }, new() { Id = 3, Value = "c" }
        };

        var result = data.AsQueryable().Paginate(1, 2, x => x.Id);

        Assert.NotNull(result);
        Assert.Equal(1, result.PageNumber);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(1, result.Data.First().Id);
    }

    [Fact]
    public void GivenZeroPageNumberAndPageSize_WhenPaginating_ThenNormalizesToAtLeastOne()
    {
        var data = Enumerable.Range(1, 3).Select(i => new Item { Id = i }).ToList();

        var result = data.AsQueryable().Paginate(0, 0);

        Assert.NotNull(result);
        Assert.Equal(1, result.PageNumber);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.Count >= 0);
    }

    [Fact]
    public void GivenPartialLastPage_WhenPaginating_ThenTotalPagesUsesRequestedPageSize()
    {
        var data = Enumerable.Range(1, 23).Select(i => new Item { Id = i }).ToList();

        var result = data.AsQueryable().Paginate(3, 10);

        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.Count);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(23, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GivenPartialLastPage_WhenPaginatingAsync_ThenTotalPagesUsesRequestedPageSize()
    {
        var data = Enumerable.Range(1, 23).Select(i => new Item { Id = i }).ToList();

        var result = await data.AsQueryable().PaginateAsync(3, 10);

        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.Count);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(23, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GivenQueryableData_WhenPaginatingAsync_ThenReturnsPaginatedResult()
    {
        var data = new List<Item>
        {
            new() { Id = 2, Value = "b" }, new() { Id = 1, Value = "a" }, new() { Id = 3, Value = "c" }
        };

        var result = await data.AsQueryable().PaginateAsync(1, 2);

        Assert.NotNull(result);
        Assert.Equal(1, result.PageNumber);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(2, result.Data.First().Id);
    }

    [Fact]
    public async Task GivenQueryableData_WhenPaginatingAsyncWithOrderBy_ThenReturnsOrderedPaginatedResult()
    {
        var data = new List<Item>
        {
            new() { Id = 2, Value = "b" }, new() { Id = 1, Value = "a" }, new() { Id = 3, Value = "c" }
        };

        var result = await data.AsQueryable().PaginateAsync(1, 2, x => x.Id);

        Assert.NotNull(result);
        Assert.Equal(1, result.PageNumber);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(1, result.Data.First().Id);
    }

    [Fact]
    public async Task GivenZeroPageNumberAndPageSize_WhenPaginatingAsync_ThenNormalizesToAtLeastOne()
    {
        var data = Enumerable.Range(1, 3).Select(i => new Item { Id = i }).ToList();

        var result = await data.AsQueryable().PaginateAsync(0, 0);

        Assert.NotNull(result);
        Assert.Equal(1, result.PageNumber);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.Count >= 0);
    }

    [Fact]
    public void GivenNullQuery_WhenPaginating_ThenArgumentNullExceptionIsThrown()
    {
        IQueryable<Item> query = null!;

        Assert.Throws<ArgumentNullException>(() => query.Paginate(1, 10));
    }

    [Fact]
    public async Task GivenNullQuery_WhenPaginatingAsync_ThenArgumentNullExceptionIsThrown()
    {
        IQueryable<Item> query = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(() => query.PaginateAsync(1, 10));
    }

    [Fact]
    public void GivenNegativePageNumber_WhenPaginating_ThenPageNumberIsClampedToOne()
    {
        var data = Enumerable.Range(1, 5).Select(i => new Item { Id = i }).ToList();

        var result = data.AsQueryable().Paginate(-7, 2);

        Assert.Equal(1, result.PageNumber);
        Assert.NotNull(result.Data);
        Assert.Equal(new[] { 1, 2 }, result.Data.Select(x => x.Id));
    }

    [Fact]
    public async Task GivenNegativePageNumber_WhenPaginatingAsync_ThenPageNumberIsClampedToOne()
    {
        var data = Enumerable.Range(1, 5).Select(i => new Item { Id = i }).ToList();

        var result = await data.AsQueryable().PaginateAsync(-7, 2);

        Assert.Equal(1, result.PageNumber);
        Assert.NotNull(result.Data);
        Assert.Equal(new[] { 1, 2 }, result.Data.Select(x => x.Id));
    }

    [Fact]
    public void GivenNegativePageSize_WhenPaginating_ThenPageSizeIsClampedToOne()
    {
        var data = Enumerable.Range(1, 5).Select(i => new Item { Id = i }).ToList();

        var result = data.AsQueryable().Paginate(1, -3);

        Assert.Equal(1, result.PageSize);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(5, result.TotalPages);
    }

    [Fact]
    public void GivenEmptySource_WhenPaginating_ThenDataIsEmptyAndTotalPagesIsZero()
    {
        var data = new List<Item>();

        var result = data.AsQueryable().Paginate(1, 10);

        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
        Assert.Equal(0, result.TotalItems);
        Assert.Equal(0, result.TotalPages);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task GivenEmptySource_WhenPaginatingAsync_ThenDataIsEmptyAndTotalPagesIsZero()
    {
        var data = new List<Item>();

        var result = await data.AsQueryable().PaginateAsync(1, 10);

        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
        Assert.Equal(0, result.TotalItems);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public void GivenPageBeyondTheLastPage_WhenPaginating_ThenDataIsEmptyButMetadataIsPreserved()
    {
        var data = Enumerable.Range(1, 5).Select(i => new Item { Id = i }).ToList();

        var result = data.AsQueryable().Paginate(99, 2);

        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
        Assert.Equal(99, result.PageNumber);
        Assert.Equal(5, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task GivenPageBeyondTheLastPage_WhenPaginatingAsync_ThenDataIsEmptyButMetadataIsPreserved()
    {
        var data = Enumerable.Range(1, 5).Select(i => new Item { Id = i }).ToList();

        var result = await data.AsQueryable().PaginateAsync(99, 2);

        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
        Assert.Equal(99, result.PageNumber);
        Assert.Equal(5, result.TotalItems);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public void GivenSuppliedTotalCount_WhenPaginating_ThenSuppliedCountIsReported()
    {
        var data = Enumerable.Range(1, 5).Select(i => new Item { Id = i }).ToList();

        var result = data.AsQueryable().Paginate(1, 2, totalCount: 42);

        Assert.Equal(42, result.TotalItems);
        Assert.Equal(21, result.TotalPages);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
    }

    [Fact]
    public async Task GivenSuppliedTotalCount_WhenPaginatingAsync_ThenSuppliedCountIsReported()
    {
        var data = Enumerable.Range(1, 5).Select(i => new Item { Id = i }).ToList();

        var result = await data.AsQueryable().PaginateAsync(1, 2, totalCount: 42);

        Assert.Equal(42, result.TotalItems);
        Assert.Equal(21, result.TotalPages);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
    }

    [Fact]
    public void GivenNegativeSuppliedTotalCount_WhenPaginating_ThenTotalItemsIsClampedToZero()
    {
        var data = Enumerable.Range(1, 5).Select(i => new Item { Id = i }).ToList();

        var result = data.AsQueryable().Paginate(1, 2, totalCount: -10);

        Assert.Equal(0, result.TotalItems);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task GivenNegativeSuppliedTotalCount_WhenPaginatingAsync_ThenTotalItemsIsClampedToZero()
    {
        var data = Enumerable.Range(1, 5).Select(i => new Item { Id = i }).ToList();

        var result = await data.AsQueryable().PaginateAsync(1, 2, totalCount: -10);

        Assert.Equal(0, result.TotalItems);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public void GivenSuppliedTotalCountOfZero_WhenPaginating_ThenThePageIsStillMaterialised()
    {
        var data = Enumerable.Range(1, 5).Select(i => new Item { Id = i }).ToList();

        var result = data.AsQueryable().Paginate(1, 2, totalCount: 0);

        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(0, result.TotalItems);
    }

    [Fact]
    public async Task GivenSuppliedTotalCountOfZero_WhenPaginatingAsync_ThenThePageIsStillMaterialised()
    {
        var data = Enumerable.Range(1, 5).Select(i => new Item { Id = i }).ToList();

        var result = await data.AsQueryable().PaginateAsync(1, 2, totalCount: 0);

        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(0, result.TotalItems);
    }

    [Fact]
    public void GivenReferenceTypeOrderKey_WhenPaginating_ThenResultsAreOrderedByThatKey()
    {
        var data = new List<Item>
        {
            new() { Id = 1, Value = "c" }, new() { Id = 2, Value = "a" }, new() { Id = 3, Value = "b" }
        };

        var result = data.AsQueryable().Paginate(1, 3, x => x.Value);

        Assert.NotNull(result.Data);
        Assert.Equal(new[] { "a", "b", "c" }, result.Data.Select(x => x.Value));
    }

    [Fact]
    public async Task GivenReferenceTypeOrderKey_WhenPaginatingAsync_ThenResultsAreOrderedByThatKey()
    {
        var data = new List<Item>
        {
            new() { Id = 1, Value = "c" }, new() { Id = 2, Value = "a" }, new() { Id = 3, Value = "b" }
        };

        var result = await data.AsQueryable().PaginateAsync(1, 3, x => x.Value);

        Assert.NotNull(result.Data);
        Assert.Equal(new[] { "a", "b", "c" }, result.Data.Select(x => x.Value));
    }

    [Fact]
    public void GivenValueTypeOrderKey_WhenPaginating_ThenTheConvertToObjectNodeIsUnwrapped()
    {
        var source = new CapturingQueryable<Item>(Enumerable.Range(1, 3).Select(i => new Item { Id = i }));

        _ = source.Paginate(1, 3, x => x.Id);

        Assert.NotNull(source.LastExpression);
        Assert.DoesNotContain("Convert(", source.LastExpression!.ToString());
    }

    [Fact]
    public async Task GivenValueTypeOrderKey_WhenPaginatingAsync_ThenTheConvertToObjectNodeIsUnwrapped()
    {
        var source = new CapturingQueryable<Item>(Enumerable.Range(1, 3).Select(i => new Item { Id = i }));

        _ = await source.PaginateAsync(1, 3, x => x.Id);

        Assert.NotNull(source.LastExpression);
        Assert.DoesNotContain("Convert(", source.LastExpression!.ToString());
    }
}

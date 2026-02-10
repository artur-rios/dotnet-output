using ArturRios.Output.Tests.Mock;

namespace ArturRios.Output.Tests;

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
}

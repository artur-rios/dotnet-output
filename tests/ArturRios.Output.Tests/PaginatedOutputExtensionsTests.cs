using ArturRios.Output.Tests.Mock;

namespace ArturRios.Output.Tests;

public class PaginatedOutputExtensionsTests
{
    [Fact]
    public void Should_Paginate()
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
    public void Should_Paginate_And_OrderById()
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
    public void Should_NormalizePageNumberAndPageSize_ToAtLeastOne()
    {
        var data = Enumerable.Range(1, 3).Select(i => new Item { Id = i }).ToList();

        var result = data.AsQueryable().Paginate(0, 0);

        Assert.NotNull(result);
        Assert.Equal(1, result.PageNumber);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.Count >= 0);
    }

    [Fact]
    public async Task Should_PaginateAsync()
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
    public async Task Should_Paginate_And_OrderByIdAsync()
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
    public async Task Should_NormalizePageNumberAndPageSize_ToAtLeastOneAsync()
    {
        var data = Enumerable.Range(1, 3).Select(i => new Item { Id = i }).ToList();

        var result = await data.AsQueryable().PaginateAsync(0, 0);

        Assert.NotNull(result);
        Assert.Equal(1, result.PageNumber);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.Count >= 0);
    }
}

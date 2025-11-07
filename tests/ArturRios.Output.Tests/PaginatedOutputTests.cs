namespace ArturRios.Output.Tests;

public class PaginatedOutputTests
{
    [Fact]
    public void Should_AddItems_And_CalculatePagination()
    {
        var output = PaginatedOutput<int>.New.WithEmptyData().WithPagination(2, 50);

        output.AddItem(1);
        output.AddItems([2, 3]);

        Assert.NotNull(output.Data);
        Assert.Equal(3, output.Data.Count);
        Assert.Equal(2, output.PageNumber);
        Assert.Equal(50, output.TotalItems);
    }

    [Fact]
    public void Should_CreateWithData()
    {
        var output = PaginatedOutput<string>.New.WithData("a");

        Assert.NotNull(output.Data);
        Assert.Single(output.Data);
        Assert.Equal("a", output.Data.First());
    }

    [Fact]
    public void Should_AddData()
    {
        var output = PaginatedOutput<string>.New.WithData(["a"]);

        Assert.NotNull(output.Data);
        Assert.Single(output.Data);

        output.WithData("b");

        Assert.Equal(2, output.Data.Count);
        Assert.Equal("a", output.Data[0]);
        Assert.Equal("b", output.Data[1]);
    }

    [Fact]
    public void Should_ReplaceData()
    {
        var output = PaginatedOutput<string>.New.WithData(["a"]);

        Assert.NotNull(output.Data);
        Assert.Single(output.Data);
        Assert.Equal("a", output.Data.First());

        output.WithData(["b"]);

        Assert.NotNull(output.Data);
        Assert.Single(output.Data);
        Assert.Equal("b", output.Data.First());
    }

    [Fact]
    public void Should_CalculatePageSize_And_TotalPages()
    {
        var output = PaginatedOutput<int>.New.WithData([1, 2, 3]).WithPagination(1, 7);

        Assert.NotNull(output.Data);
        Assert.Equal(3, output.PageSize);
        Assert.Equal(3, output.Data.Count);
        Assert.Equal(3, output.PageSize);
        Assert.Equal((int)Math.Ceiling(7d / 3d), output.TotalPages);
    }

    [Fact]
    public void Should_CreateWithMessage()
    {
        var output = PaginatedOutput<string>.New
            .WithData(["a"])
            .WithMessage("Hello world");

        Assert.NotNull(output.Data);
        Assert.Single(output.Data);
        Assert.Equal("a", output.Data[0]);
        Assert.Equal("Hello world", output.Messages.First());
    }

    [Fact]
    public void Should_CreateWithMessages_And_IgnoreEmpty()
    {
        var output = PaginatedOutput<string>.New
            .WithData(["a"])
            .WithMessages(["One", string.Empty, "  ", "Two"]);

        Assert.NotNull(output.Data);
        Assert.Single(output.Data);
        Assert.Equal("a", output.Data[0]);
        Assert.Equal(2, output.Messages.Count);
        Assert.Contains("One", output.Messages);
        Assert.Contains("Two", output.Messages);
    }

    [Fact]
    public void Should_CreateWithError()
    {
        var output = PaginatedOutput<string>.New
            .WithData(["a"])
            .WithError("Something went wrong");

        Assert.NotNull(output.Data);
        Assert.Single(output.Data);
        Assert.Equal("a", output.Data[0]);
        Assert.Equal("Something went wrong", output.Errors.First());
    }

    [Fact]
    public void Should_CreateWithErrors_And_IgnoreEmpty()
    {
        var output = PaginatedOutput<string>.New
            .WithData(["a"])
            .WithErrors(["One", string.Empty, "  ", "Two"]);

        Assert.NotNull(output.Data);
        Assert.Single(output.Data);
        Assert.Equal("a", output.Data[0]);
        Assert.Equal(2, output.Errors.Count);
        Assert.Contains("One", output.Errors);
        Assert.Contains("Two", output.Errors);
    }
}

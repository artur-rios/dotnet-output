namespace ArturRios.Output.Tests;

public class PaginatedOutputTests
{
    [Fact]
    public void GivenPaginatedOutput_WhenAddingItems_ThenPaginationIsCalculated()
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
    public void GivenSingleItem_WhenCreatingWithData_ThenDataContainsSingleItem()
    {
        var output = PaginatedOutput<string>.New.WithData("a");

        Assert.NotNull(output.Data);
        Assert.Single(output.Data);
        Assert.Equal("a", output.Data.First());
    }

    [Fact]
    public void GivenExistingData_WhenAddingData_ThenDataIsAppended()
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
    public void GivenExistingData_WhenReplacingWithArray_ThenDataIsReplaced()
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
    public void GivenDataAndPagination_WhenCalculating_ThenPageSizeAndTotalPagesAreCorrect()
    {
        var output = PaginatedOutput<int>.New.WithData([1, 2, 3]).WithPagination(1, 7);

        Assert.NotNull(output.Data);
        Assert.Equal(3, output.PageSize);
        Assert.Equal(3, output.Data.Count);
        Assert.Equal(3, output.PageSize);
        Assert.Equal((int)Math.Ceiling(7d / 3d), output.TotalPages);
    }

    [Fact]
    public void GivenDataAndMessage_WhenCreating_ThenOutputContainsDataAndMessage()
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
    public void GivenMessagesWithEmptyStrings_WhenCreating_ThenEmptyMessagesAreIgnored()
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
    public void GivenDataAndError_WhenCreating_ThenOutputContainsDataAndError()
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
    public void GivenErrorsWithEmptyStrings_WhenCreating_ThenEmptyErrorsAreIgnored()
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

namespace ArturRios.Output.Tests;

public class DataOutputTests
{
    [Fact]
    public void Should_CreateWithData()
    {
        var output = DataOutput<string>.New.WithData("Hello world");

        Assert.NotNull(output);
        Assert.Equal("Hello world", output.Data);
        Assert.True(output.Success);
    }

    [Fact]
    public void Should_AddData()
    {
        var output = DataOutput<string>.New;
        output.AddData("Hello world");

        Assert.NotNull(output);
        Assert.Equal("Hello world", output.Data);
        Assert.True(output.Success);
    }

    [Fact]
    public void Should_AddError_And_SetSuccessFalse()
    {
        var output = DataOutput<int>.New.WithData(5).WithError("Something went wrong");

        Assert.False(output.Success);
        Assert.Contains("Something went wrong", output.Errors);
        Assert.Equal(5, output.Data);
    }

    [Fact]
    public void Should_FilterEmpty_And_AddErrors()
    {
        var errors = new[] { "err1", "", "  ", "err2" };

        var output = DataOutput<object>.New.WithErrors(errors);

        Assert.False(output.Success);
        Assert.Equal(2, output.Errors.Count);
        Assert.Contains("err1", output.Errors);
        Assert.Contains("err2", output.Errors);
    }

    [Fact]
    public void Should_AddMessage()
    {
        var output = DataOutput<bool>.New.WithMessage("Ok");

        Assert.True(output.Success);
        Assert.Contains("Ok", output.Messages);
    }

    [Fact]
    public void Should_FilterEmpty_And_AddMessages()
    {
        var messages = new List<string> { "m1", "", "m2" };

        var output = DataOutput<string>.New.WithMessages(messages);

        Assert.Equal(2, output.Messages.Count);
        Assert.Contains("m1", output.Messages);
        Assert.Contains("m2", output.Messages);
    }

    [Fact]
    public void Should_AllowNullData_For_NullableTypes()
    {
        var output = DataOutput<string?>.New.WithData(null);

        Assert.Null(output.Data);
        Assert.True(output.Success);
    }
}

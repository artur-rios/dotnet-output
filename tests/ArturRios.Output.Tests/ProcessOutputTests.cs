namespace ArturRios.Output.Tests;

public class ProcessOutputTests
{
    [Fact]
    public void Should_AddError_FilterEmpty_And_SetSuccessFalse()
    {
        var output = ProcessOutput.New;

        output.AddError(null!);
        output.AddError("   ");
        output.AddError("err1");

        Assert.Single(output.Errors);
        Assert.False(output.Success);
        Assert.Equal(DateTime.UtcNow.Date, output.Timestamp.Date);
    }

    [Fact]
    public void ShouldNot_AddError()
    {
        var output = ProcessOutput.New;

        output.AddError(string.Empty);
        output.AddError("   ");
        output.AddError(null!);

        Assert.Empty(output.Errors);
        Assert.True(output.Success);
        Assert.Equal(DateTime.UtcNow.Date, output.Timestamp.Date);
    }

    [Fact]
    public void Should_AddErrors_And_FilterEmpty()
    {
        var output = ProcessOutput.New;

        output.AddErrors(["e1", string.Empty, "e2"]);

        Assert.Equal(2, output.Errors.Count);
        Assert.Equal(DateTime.UtcNow.Date, output.Timestamp.Date);
    }

    [Fact]
    public void ShouldNot_AddErrors()
    {
        var output = ProcessOutput.New;

        output.AddErrors([string.Empty, "   ", null!]);

        Assert.Empty(output.Errors);
        Assert.True(output.Success);
        Assert.Equal(DateTime.UtcNow.Date, output.Timestamp.Date);
    }

    [Fact]
    public void Should_AddMessage_And_FilterEmpty()
    {
        var output = ProcessOutput.New;

        output.AddMessage(string.Empty);
        output.AddMessage("   ");
        output.AddMessage(null!);
        output.AddMessage("m1");

        Assert.Single(output.Messages);
        Assert.Equal("m1", output.Messages.First());
        Assert.True(output.Success);
        Assert.Equal(DateTime.UtcNow.Date, output.Timestamp.Date);
    }

    [Fact]
    public void ShouldNot_AddMessage()
    {
        var output = ProcessOutput.New;

        output.AddMessage(string.Empty);
        output.AddMessage("   ");
        output.AddMessage(null!);

        Assert.Empty(output.Messages);
        Assert.True(output.Success);
        Assert.Equal(DateTime.UtcNow.Date, output.Timestamp.Date);
    }

    [Fact]
    public void Should_AddMessages_And_FilterEmpty()
    {
        var output = ProcessOutput.New;

        output.AddMessages(["m1", string.Empty, "m2"]);

        Assert.Equal(2, output.Messages.Count);
        Assert.True(output.Success);
        Assert.Equal(DateTime.UtcNow.Date, output.Timestamp.Date);
    }

    [Fact]
    public void ShouldNot_AddMessages()
    {
        var output = ProcessOutput.New;

        output.AddMessages([string.Empty, "   ", null!]);

        Assert.Empty(output.Messages);
        Assert.True(output.Success);
        Assert.Equal(DateTime.UtcNow.Date, output.Timestamp.Date);
    }

    [Fact]
    public void Should_AddProperties()
    {
        var output = ProcessOutput.New
            .WithMessage("m")
            .WithError("e")
            .WithMessages(["m2"])
            .WithErrors(["e2"]);

        Assert.Equal(2, output.Messages.Count);
        Assert.Equal(2, output.Errors.Count);
        Assert.Equal(DateTime.UtcNow.Date, output.Timestamp.Date);
    }
}

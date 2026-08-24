namespace ArturRios.Output.Tests;

[Trait("Category", "Unit")]
public class ProcessOutputTests
{
    [Fact]
    public void GivenNullAssignedToCollections_WhenReadingThem_ThenTheyFallBackToEmptyLists()
    {
        var output = ProcessOutput.New;

        output.Messages = null!;
        output.Errors = null!;

        Assert.Empty(output.Messages);
        Assert.Empty(output.Errors);
        Assert.True(output.Success);
    }

    [Fact]
    public void GivenNullAssignedToErrors_WhenAddingError_ThenErrorIsStillCollected()
    {
        var output = ProcessOutput.New;

        output.Errors = null!;
        output.AddError("err1");

        Assert.Single(output.Errors);
        Assert.False(output.Success);
    }

    [Fact]
    public void GivenErrorsWithEmptyAndValid_WhenAddingError_ThenOnlyValidErrorIsAddedAndSuccessIsFalse()
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
    public void GivenOnlyEmptyErrors_WhenAddingError_ThenNoErrorIsAdded()
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
    public void GivenErrorsArrayWithEmptyStrings_WhenAddingErrors_ThenOnlyValidErrorsAreAdded()
    {
        var output = ProcessOutput.New;

        output.AddErrors(["e1", string.Empty, "e2"]);

        Assert.Equal(2, output.Errors.Count);
        Assert.Equal(DateTime.UtcNow.Date, output.Timestamp.Date);
    }

    [Fact]
    public void GivenOnlyEmptyErrorsArray_WhenAddingErrors_ThenNoErrorIsAdded()
    {
        var output = ProcessOutput.New;

        output.AddErrors([string.Empty, "   ", null!]);

        Assert.Empty(output.Errors);
        Assert.True(output.Success);
        Assert.Equal(DateTime.UtcNow.Date, output.Timestamp.Date);
    }

    [Fact]
    public void GivenMessagesWithEmptyAndValid_WhenAddingMessage_ThenOnlyValidMessageIsAdded()
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
    public void GivenOnlyEmptyMessages_WhenAddingMessage_ThenNoMessageIsAdded()
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
    public void GivenMessagesArrayWithEmptyStrings_WhenAddingMessages_ThenOnlyValidMessagesAreAdded()
    {
        var output = ProcessOutput.New;

        output.AddMessages(["m1", string.Empty, "m2"]);

        Assert.Equal(2, output.Messages.Count);
        Assert.True(output.Success);
        Assert.Equal(DateTime.UtcNow.Date, output.Timestamp.Date);
    }

    [Fact]
    public void GivenOnlyEmptyMessagesArray_WhenAddingMessages_ThenNoMessageIsAdded()
    {
        var output = ProcessOutput.New;

        output.AddMessages([string.Empty, "   ", null!]);

        Assert.Empty(output.Messages);
        Assert.True(output.Success);
        Assert.Equal(DateTime.UtcNow.Date, output.Timestamp.Date);
    }

    [Fact]
    public void GivenMessagesAndErrors_WhenAddingProperties_ThenAllPropertiesAreAdded()
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

    [Fact]
    public void GivenNullErrorCollection_WhenAddingErrors_ThenTheCallIsIgnored()
    {
        var output = ProcessOutput.New;

        output.AddErrors(null);

        Assert.Empty(output.Errors);
        Assert.True(output.Success);
    }

    [Fact]
    public void GivenNullMessageCollection_WhenAddingMessages_ThenTheCallIsIgnored()
    {
        var output = ProcessOutput.New;

        output.AddMessages(null);

        Assert.Empty(output.Messages);
    }

    [Fact]
    public void GivenNullErrorCollection_WhenUsingTheFluentHelper_ThenTheSameInstanceComesBackUnchanged()
    {
        var output = ProcessOutput.New;

        var returned = output.WithErrors(null);

        Assert.Same(output, returned);
        Assert.True(returned.Success);
    }

    [Fact]
    public void GivenNullMessageCollection_WhenUsingTheFluentHelper_ThenTheSameInstanceComesBackUnchanged()
    {
        var output = ProcessOutput.New;

        var returned = output.WithMessages(null);

        Assert.Same(output, returned);
        Assert.Empty(returned.Messages);
    }

    [Fact]
    public void GivenTwoAccessesToTheFactory_WhenCreating_ThenDistinctInstancesComeBack()
    {
        var first = ProcessOutput.New;
        var second = ProcessOutput.New;

        Assert.NotSame(first, second);
    }

    [Fact]
    public void GivenAnOutput_WhenCreated_ThenTheTimestampIsUtcAndRecent()
    {
        var before = DateTime.UtcNow;

        var output = ProcessOutput.New;

        Assert.Equal(DateTimeKind.Utc, output.Timestamp.Kind);
        Assert.InRange(output.Timestamp, before.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
    }
}

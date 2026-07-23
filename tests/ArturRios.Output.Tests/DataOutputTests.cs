using System.Text.Json;

namespace ArturRios.Output.Tests;

public class DataOutputTests
{
    [Fact]
    public void GivenStringData_WhenCreatingWithData_ThenOutputContainsDataAndSuccess()
    {
        var output = DataOutput<string>.New.WithData("Hello world");

        Assert.NotNull(output);
        Assert.Equal("Hello world", output.Data);
        Assert.True(output.Success);
    }

    [Fact]
    public void GivenNewOutput_WhenAddingData_ThenOutputContainsDataAndSuccess()
    {
        var output = DataOutput<string>.New;
        output.AddData("Hello world");

        Assert.NotNull(output);
        Assert.Equal("Hello world", output.Data);
        Assert.True(output.Success);
    }

    [Fact]
    public void GivenOutputWithData_WhenAddingError_ThenSuccessIsFalse()
    {
        var output = DataOutput<int>.New.WithData(5).WithError("Something went wrong");

        Assert.False(output.Success);
        Assert.Contains("Something went wrong", output.Errors);
        Assert.Equal(5, output.Data);
    }

    [Fact]
    public void GivenErrorsWithEmptyStrings_WhenAddingErrors_ThenOnlyNonEmptyErrorsAreAdded()
    {
        var errors = new[] { "err1", "", "  ", "err2" };

        var output = DataOutput<object>.New.WithErrors(errors);

        Assert.False(output.Success);
        Assert.Equal(2, output.Errors.Count);
        Assert.Contains("err1", output.Errors);
        Assert.Contains("err2", output.Errors);
    }

    [Fact]
    public void GivenMessage_WhenAddingMessage_ThenMessageIsAdded()
    {
        var output = DataOutput<bool>.New.WithMessage("Ok");

        Assert.True(output.Success);
        Assert.Contains("Ok", output.Messages);
    }

    [Fact]
    public void GivenMessagesWithEmptyStrings_WhenAddingMessages_ThenOnlyNonEmptyMessagesAreAdded()
    {
        var messages = new List<string> { "m1", "", "m2" };

        var output = DataOutput<string>.New.WithMessages(messages);

        Assert.Equal(2, output.Messages.Count);
        Assert.Contains("m1", output.Messages);
        Assert.Contains("m2", output.Messages);
    }

    [Fact]
    public void GivenNullableType_WhenAddingNullData_ThenNullDataIsAllowed()
    {
        var output = DataOutput<string?>.New.WithData(null);

        Assert.Null(output.Data);
        Assert.True(output.Success);
    }

    [Fact]
    public void GivenSerializedOutput_WhenDeserializing_ThenAllPropertiesAreRestored()
    {
        var output = DataOutput<string>.New
            .WithData("Hello world")
            .WithMessage("Ok")
            .WithError("Something went wrong");

        var json = JsonSerializer.Serialize(output);
        var deserialized = JsonSerializer.Deserialize<DataOutput<string>>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("Hello world", deserialized.Data);
        Assert.Equal(output.Messages, deserialized.Messages);
        Assert.Equal(output.Errors, deserialized.Errors);
        Assert.Equal(output.Timestamp, deserialized.Timestamp);
        Assert.False(deserialized.Success);
    }

    [Fact]
    public void GivenCamelCaseJson_WhenDeserializingWithWebOptions_ThenAllPropertiesAreRestored()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var output = DataOutput<int>.New.WithData(5).WithMessage("Ok");

        var json = JsonSerializer.Serialize(output, options);
        var deserialized = JsonSerializer.Deserialize<DataOutput<int>>(json, options);

        Assert.NotNull(deserialized);
        Assert.Equal(5, deserialized.Data);
        Assert.Equal(output.Messages, deserialized.Messages);
        Assert.Equal(output.Timestamp, deserialized.Timestamp);
        Assert.True(deserialized.Success);
    }
}

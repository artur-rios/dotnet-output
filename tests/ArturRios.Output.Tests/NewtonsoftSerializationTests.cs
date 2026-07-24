using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SystemTextJson = System.Text.Json.JsonSerializer;

namespace ArturRios.Output.Tests;

/// <summary>
/// Verifies the output types survive Newtonsoft.Json, on its own and when it has to
/// read payloads written by System.Text.Json (and the other way around).
/// </summary>
public class NewtonsoftSerializationTests
{
    private static readonly JsonSerializerSettings CamelCase = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };

    [Fact]
    public void GivenSerializedOutput_WhenDeserializingWithNewtonsoft_ThenAllPropertiesAreRestored()
    {
        var output = DataOutput<string>.New
            .WithData("Hello world")
            .WithMessage("Ok")
            .WithError("Something went wrong");

        var json = JsonConvert.SerializeObject(output);
        var deserialized = JsonConvert.DeserializeObject<DataOutput<string>>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("Hello world", deserialized.Data);
        Assert.Equal(output.Messages, deserialized.Messages);
        Assert.Equal(output.Errors, deserialized.Errors);
        Assert.Equal(output.Timestamp, deserialized.Timestamp);
        Assert.False(deserialized.Success);
    }

    [Fact]
    public void GivenSystemTextJsonPayload_WhenDeserializingWithNewtonsoft_ThenAllPropertiesAreRestored()
    {
        var output = DataOutput<string>.New.WithData("Hello world").WithMessage("Ok");

        var json = SystemTextJson.Serialize(output);
        var deserialized = JsonConvert.DeserializeObject<DataOutput<string>>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("Hello world", deserialized.Data);
        Assert.Equal(output.Messages, deserialized.Messages);
        Assert.Equal(output.Timestamp, deserialized.Timestamp);
    }

    [Fact]
    public void GivenNewtonsoftPayload_WhenDeserializingWithSystemTextJson_ThenAllPropertiesAreRestored()
    {
        var output = DataOutput<string>.New.WithData("Hello world").WithMessage("Ok");

        var json = JsonConvert.SerializeObject(output);
        var deserialized = SystemTextJson.Deserialize<DataOutput<string>>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("Hello world", deserialized.Data);
        Assert.Equal(output.Messages, deserialized.Messages);
        Assert.Equal(output.Timestamp, deserialized.Timestamp);
    }

    [Fact]
    public void GivenCamelCaseSettings_WhenRoundTrippingWithNewtonsoft_ThenAllPropertiesAreRestored()
    {
        var output = DataOutput<int>.New.WithData(5).WithMessage("Ok");

        var json = JsonConvert.SerializeObject(output, CamelCase);
        var deserialized = JsonConvert.DeserializeObject<DataOutput<int>>(json, CamelCase);

        Assert.NotNull(deserialized);
        Assert.Equal(5, deserialized.Data);
        Assert.Equal(output.Messages, deserialized.Messages);
        Assert.Equal(output.Timestamp, deserialized.Timestamp);
    }

    [Fact]
    public void GivenJsonWithNullCollections_WhenDeserializingWithNewtonsoft_ThenCollectionsFallBackToEmptyLists()
    {
        const string json = """{"Data":"Hello world","Messages":null,"Errors":null}""";

        var deserialized = JsonConvert.DeserializeObject<DataOutput<string>>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("Hello world", deserialized.Data);
        Assert.Empty(deserialized.Messages);
        Assert.Empty(deserialized.Errors);
        Assert.True(deserialized.Success);
    }

    [Fact]
    public void GivenProcessOutput_WhenRoundTrippingWithNewtonsoft_ThenMessagesAndErrorsAreRestored()
    {
        var output = ProcessOutput.New.WithMessage("Ok").WithError("Something went wrong");

        var json = JsonConvert.SerializeObject(output);
        var deserialized = JsonConvert.DeserializeObject<ProcessOutput>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(output.Messages, deserialized.Messages);
        Assert.Equal(output.Errors, deserialized.Errors);
        Assert.Equal(output.Timestamp, deserialized.Timestamp);
        Assert.False(deserialized.Success);
    }

    [Fact]
    public void GivenPaginatedOutput_WhenRoundTrippingWithNewtonsoft_ThenPaginationMetadataIsRestored()
    {
        var output = PaginatedOutput<int>.New.WithData([21, 22, 23]).WithPagination(3, 10, 23);

        var json = JsonConvert.SerializeObject(output);
        var deserialized = JsonConvert.DeserializeObject<PaginatedOutput<int>>(json);

        Assert.NotNull(deserialized);
        Assert.Equal([21, 22, 23], deserialized.Data);
        Assert.Equal(3, deserialized.PageNumber);
        Assert.Equal(10, deserialized.PageSize);
        Assert.Equal(23, deserialized.TotalItems);
        Assert.Equal(3, deserialized.TotalPages);
    }
}

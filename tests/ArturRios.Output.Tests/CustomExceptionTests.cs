using ArturRios.Output.Tests.Mock;

namespace ArturRios.Output.Tests;

[Trait("Category", "Unit")]
public class CustomExceptionTests
{
    [Fact]
    public void GivenSingleMessage_WhenExceptionIsThrown_ThenReturnMessage()
    {
        var messages = new[] { "Error 1" };

        var exception = Assert.Throws<TestException>(() => TestMethod(messages));

        Assert.Equal(messages, exception.Messages);
        Assert.Equal("Error 1", exception.Message);
    }

    [Fact]
    public void GivenMultipleMessages_WhenExceptionIsThrown_ThenReturnConcatenatedMessages()
    {
        var messages = new[] { "Error 1", "Error 2" };

        var exception = Assert.Throws<TestException>(() => TestMethod(messages));

        Assert.Equal(messages, exception.Messages);
        Assert.Equal("Error 1, Error 2", exception.Message);
    }

    private static void TestMethod(string[] messages) => throw new TestException(messages);

    [Fact]
    public void GivenNoMessages_WhenExceptionIsCreated_ThenTheMessageIsEmpty()
    {
        var exception = new TestException([]);

        Assert.Empty(exception.Messages);
        Assert.Equal(string.Empty, exception.Message);
    }

    [Fact]
    public void GivenMessages_WhenExceptionIsCreated_ThenItIsAnException()
    {
        Exception exception = new TestException(["Error 1"]);

        Assert.IsAssignableFrom<CustomException>(exception);
        Assert.Null(exception.InnerException);
    }
}

using ArturRios.Output.Tests.Mock;

namespace ArturRios.Output.Tests;

public class Tests
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
}

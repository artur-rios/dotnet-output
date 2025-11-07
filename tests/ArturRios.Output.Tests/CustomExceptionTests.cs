using ArturRios.Output.Tests.Mock;

namespace ArturRios.Output.Tests;

public class Tests
{
    [Fact]
    public void Should_ReturnMessage()
    {
        var messages = new[] { "Error 1" };

        var exception = Assert.Throws<TestException>(() => TestMethod(messages));

        Assert.Equal(messages, exception.Messages);
        Assert.Equal("Error 1", exception.Message);
    }

    [Fact]
    public void Should_ReturnMessages()
    {
        var messages = new[] { "Error 1", "Error 2" };

        var exception = Assert.Throws<TestException>(() => TestMethod(messages));

        Assert.Equal(messages, exception.Messages);
        Assert.Equal("Error 1, Error 2", exception.Message);
    }

    private static void TestMethod(string[] messages) => throw new TestException(messages);
}

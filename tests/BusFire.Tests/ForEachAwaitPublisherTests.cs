using BusFire;
using BusFire.EventPublishers;
using Xunit;

namespace BusFire.Tests;

public class ForEachAwaitPublisherTests
{
    [Fact]
    public async Task Publishes_to_each_handler_executor_in_order()
    {
        var log = new List<string>();
        var executors = new[]
        {
            new EventHandlerExecutor(new object(), (_, _) => { log.Add("1"); return Task.CompletedTask; }),
            new EventHandlerExecutor(new object(), (_, _) => { log.Add("2"); return Task.CompletedTask; }),
        };

        await new ForEachAwaitPublisher().Publish(executors, new Pinged("e"), default);

        Assert.Equal(new[] { "1", "2" }, log);
    }

    [Fact]
    public async Task Surfaces_a_handler_exception()
    {
        var executors = new[]
        {
            new EventHandlerExecutor(new object(), (_, _) => throw new InvalidOperationException("nope")),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ForEachAwaitPublisher().Publish(executors, new Pinged("e"), default));
    }
}

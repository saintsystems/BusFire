using BusFire;
using BusFire.Infrastructure;
using Newtonsoft.Json;
using Xunit;

namespace BusFire.Tests;

public class MessageTypeRegistryTests
{
    // Plain (non-message) types so the assembly scan in AddBusFireTests doesn't pick up the collision.
    [MessageName("dup")] private sealed class Dupe1 { }
    [MessageName("dup")] private sealed class Dupe2 { }

    [Fact]
    public void GetName_defaults_to_full_type_name()
    {
        var registry = new MessageTypeRegistry(new[] { typeof(Ping) });
        Assert.Equal(typeof(Ping).FullName, registry.GetName(typeof(Ping)));
    }

    [Fact]
    public void GetName_honors_the_MessageName_attribute()
    {
        var registry = new MessageTypeRegistry(new[] { typeof(NamedCommand) });
        Assert.Equal("custom-command-name", registry.GetName(typeof(NamedCommand)));
    }

    [Fact]
    public void Resolve_round_trips_a_logical_name_to_its_type()
    {
        var registry = new MessageTypeRegistry(new[] { typeof(NamedCommand) });
        Assert.Equal(typeof(NamedCommand), registry.Resolve("custom-command-name"));
    }

    [Fact]
    public void Resolve_unknown_name_throws()
    {
        var registry = new MessageTypeRegistry(new[] { typeof(Ping) });
        Assert.Throws<JsonSerializationException>(() => registry.Resolve("does-not-exist"));
    }

    [Fact]
    public void Duplicate_logical_names_throw_at_construction()
    {
        Assert.Throws<InvalidOperationException>(
            () => new MessageTypeRegistry(new[] { typeof(Dupe1), typeof(Dupe2) }));
    }

    [Fact]
    public void DiscoverMessageTypes_finds_concrete_commands_and_events()
    {
        var types = MessageTypeRegistry.DiscoverMessageTypes(new[] { typeof(Ping).Assembly }).ToList();

        Assert.Contains(typeof(Ping), types);
        Assert.Contains(typeof(Pinged), types);
        Assert.DoesNotContain(typeof(Dupe1), types); // not a message type
    }
}

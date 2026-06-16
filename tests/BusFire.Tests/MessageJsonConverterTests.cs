using BusFire;
using BusFire.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace BusFire.Tests;

public class MessageJsonConverterTests
{
    private static JsonSerializerSettings Settings(params Type[] messageTypes)
    {
        var registry = new MessageTypeRegistry(messageTypes);
        var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };
        settings.Converters.Add(new MessageJsonConverter(registry));
        return settings;
    }

    [Fact]
    public void Serializes_with_a_logical_type_name_and_no_clr_type_metadata()
    {
        var settings = Settings(typeof(NamedCommand));
        ICommand command = new NamedCommand(7, "hello");

        var json = JsonConvert.SerializeObject(command, settings);
        var obj = JObject.Parse(json);

        Assert.Equal("custom-command-name", (string?)obj["__busfire_type"]);
        Assert.Null(obj["$type"]);
        Assert.DoesNotContain(typeof(NamedCommand).Assembly.GetName().Name!, json);
        Assert.Equal(7, (int?)obj["Id"]);
    }

    [Fact]
    public void Round_trips_a_command_dispatched_through_the_interface_type()
    {
        var settings = Settings(typeof(NamedCommand));
        ICommand original = new NamedCommand(7, "hello");

        var json = JsonConvert.SerializeObject(original, settings);
        var back = JsonConvert.DeserializeObject<ICommand>(json, settings);

        var typed = Assert.IsType<NamedCommand>(back);
        Assert.Equal(7, typed.Id);
        Assert.Equal("hello", typed.Note);
    }

    [Fact]
    public void Round_trips_an_event()
    {
        var settings = Settings(typeof(Pinged));
        IEvent original = new Pinged("hi");

        var json = JsonConvert.SerializeObject(original, settings);
        var back = JsonConvert.DeserializeObject<IEvent>(json, settings);

        Assert.Equal(new Pinged("hi"), back);
    }

    [Fact]
    public void Unknown_logical_name_throws_on_deserialize()
    {
        var settings = Settings(typeof(NamedCommand));
        var json = "{\"__busfire_type\":\"nope\",\"Id\":1,\"Note\":\"x\"}";

        Assert.Throws<JsonSerializationException>(
            () => JsonConvert.DeserializeObject<ICommand>(json, settings));
    }

    [Fact]
    public void Missing_logical_name_throws_on_deserialize()
    {
        var settings = Settings(typeof(NamedCommand));
        var json = "{\"Id\":1,\"Note\":\"x\"}";

        Assert.Throws<JsonSerializationException>(
            () => JsonConvert.DeserializeObject<ICommand>(json, settings));
    }
}

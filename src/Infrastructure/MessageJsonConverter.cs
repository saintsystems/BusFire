using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BusFire.Infrastructure
{
    /// <summary>
    /// Serializes <see cref="ICommand"/>/<see cref="IEvent"/> arguments using a stable logical type name
    /// (from <see cref="IMessageTypeRegistry"/>) instead of Newtonsoft's <c>$type</c> metadata. The body is
    /// serialized with <c>TypeNameHandling.None</c>, so persisted jobs carry no assembly-qualified type
    /// names — eliminating the deserialization-gadget vector and surviving type/assembly renames.
    /// </summary>
    /// <remarks>
    /// Needed because Hangfire stores these arguments under their declared (interface) parameter type, so on
    /// the consumer side it deserializes against <see cref="ICommand"/>/<see cref="IEvent"/> and has nothing
    /// concrete to instantiate without help. This converter supplies the concrete type from the registry.
    /// </remarks>
    public sealed class MessageJsonConverter : JsonConverter
    {
        /// <summary>Property carrying the logical type name. Deliberately not a Newtonsoft metadata key (no leading <c>$</c>).</summary>
        private const string TypeProperty = "__busfire_type";

        private readonly IMessageTypeRegistry _registry;

        // Plain serializer (no message converter) used for the message body, to avoid re-entrancy: a
        // concrete command/event is itself assignable to ICommand/IEvent, so reusing the outer serializer
        // here would recurse into this converter forever.
        private readonly JsonSerializer _bodySerializer =
            JsonSerializer.Create(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None });

        public MessageJsonConverter(IMessageTypeRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(ICommand).IsAssignableFrom(objectType) || typeof(IEvent).IsAssignableFrom(objectType);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            var body = JObject.FromObject(value, _bodySerializer);
            body.AddFirst(new JProperty(TypeProperty, _registry.GetName(value.GetType())));
            body.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            var obj = JObject.Load(reader);

            var nameToken = obj[TypeProperty];
            if (nameToken == null || nameToken.Type != JTokenType.String)
            {
                throw new JsonSerializationException(
                    $"BusFire message payload is missing its '{TypeProperty}' logical type name.");
            }

            var concreteType = _registry.Resolve(nameToken.Value<string>());
            obj.Remove(TypeProperty);

            using (var bodyReader = obj.CreateReader())
            {
                return _bodySerializer.Deserialize(bodyReader, concreteType);
            }
        }
    }
}

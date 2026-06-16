using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace BusFire.Infrastructure
{
    /// <summary>
    /// Maps messages between their CLR <see cref="Type"/> and the stable logical name BusFire persists in
    /// Hangfire jobs, so the wire format does not embed assembly-qualified type names (the
    /// <c>TypeNameHandling.All</c> gadget vector) and survives type/assembly renames.
    /// </summary>
    public interface IMessageTypeRegistry
    {
        /// <summary>The logical name for a message type (its <see cref="MessageNameAttribute"/> if present, else <see cref="Type.FullName"/>).</summary>
        string GetName(Type messageType);

        /// <summary>Resolves a logical name back to its CLR type, or throws if it is unknown.</summary>
        Type Resolve(string name);
    }

    /// <inheritdoc />
    public sealed class MessageTypeRegistry : IMessageTypeRegistry
    {
        private readonly Dictionary<Type, string> _byType = new Dictionary<Type, string>();
        private readonly Dictionary<string, Type> _byName = new Dictionary<string, Type>(StringComparer.Ordinal);

        /// <param name="messageTypes">The concrete command/event types to register (typically discovered by scanning the handler assemblies).</param>
        public MessageTypeRegistry(IEnumerable<Type> messageTypes)
        {
            if (messageTypes == null) throw new ArgumentNullException(nameof(messageTypes));

            foreach (var type in messageTypes.Where(t => t != null).Distinct())
            {
                var name = ResolveName(type);

                if (_byName.TryGetValue(name, out var existing) && existing != type)
                {
                    throw new InvalidOperationException(
                        $"BusFire message-name collision: '{name}' maps to both '{existing.AssemblyQualifiedName}' " +
                        $"and '{type.AssemblyQualifiedName}'. Disambiguate one of them with [MessageName(\"...\")].");
                }

                _byType[type] = name;
                _byName[name] = type;
            }
        }

        /// <summary>Collects the concrete command/event types defined in the given assemblies.</summary>
        public static IEnumerable<Type> DiscoverMessageTypes(IEnumerable<Assembly> assemblies)
        {
            return assemblies
                .Where(a => a != null)
                .Distinct()
                .SelectMany(a => a.DefinedTypes)
                .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters)
                .Where(t => typeof(ICommand).IsAssignableFrom(t) || typeof(IEvent).IsAssignableFrom(t))
                .Select(t => t.AsType());
        }

        private static string ResolveName(Type type)
        {
            var attribute = type.GetCustomAttribute<MessageNameAttribute>(inherit: false);
            return attribute?.Name ?? type.FullName;
        }

        public string GetName(Type messageType)
        {
            if (messageType == null) throw new ArgumentNullException(nameof(messageType));
            // Fall back to the same naming scheme for a type that wasn't pre-registered (e.g. a producer
            // whose assembly wasn't scanned here); the consumer still needs it registered to Resolve it.
            return _byType.TryGetValue(messageType, out var name) ? name : ResolveName(messageType);
        }

        public Type Resolve(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (_byName.TryGetValue(name, out var type)) return type;

            throw new JsonSerializationException(
                $"BusFire could not resolve message name '{name}' to a known type. Register the assembly that " +
                "defines it via AddBusFire's cfg.RegisterServicesFromAssembly(...) so it is in the message registry.");
        }
    }
}

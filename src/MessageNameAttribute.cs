using System;

namespace BusFire
{
    /// <summary>
    /// Pins the stable, logical type name BusFire writes into persisted jobs for a command or event,
    /// decoupling the wire format from the CLR type's namespace/assembly. Rename or move the type freely
    /// without breaking in-flight jobs, as long as the logical name stays the same.
    /// </summary>
    /// <remarks>
    /// When absent, BusFire uses the type's <see cref="Type.FullName"/> as the logical name.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class MessageNameAttribute : Attribute
    {
        public MessageNameAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Message name must be a non-empty string.", nameof(name));

            Name = name;
        }

        /// <summary>The stable logical name written to the wire.</summary>
        public string Name { get; }
    }
}

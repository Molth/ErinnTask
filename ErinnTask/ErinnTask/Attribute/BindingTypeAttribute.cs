using System;

// ReSharper disable ALL

namespace Erinn
{
    /// <summary>
    ///     Associates a decorated field with the runtime type represented by its value.
    ///     The attribute stores metadata only; it does not perform conversions or validate field contents.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class BindingTypeAttribute : Attribute
    {
        /// <summary>
        ///     The runtime type associated with the decorated field.
        /// </summary>
        public readonly Type Type;

        /// <summary>
        ///     Initializes a new instance of this class and stores the specified associated type.
        /// </summary>
        /// <param name="type">The runtime type to associate with the decorated field.</param>
        public BindingTypeAttribute(Type type) => Type = type;
    }
}
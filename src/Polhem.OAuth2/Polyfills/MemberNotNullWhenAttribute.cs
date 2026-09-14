namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Specifies that a method or property ensures that the listed members are not null when it returns the given value.
    /// </summary>
    /// <remarks>
    /// netstandard2.0 does not include this attribute, so the netstandard2.0 build compiles this internal copy; the project
    /// file excludes it from the other targets. The compiler recognizes the attribute by its name.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    internal sealed class MemberNotNullWhenAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MemberNotNullWhenAttribute"/> class with one member.
        /// </summary>
        /// <param name="returnValue">The return value for which the member is not null.</param>
        /// <param name="member">The name of the member.</param>
        public MemberNotNullWhenAttribute(bool returnValue, string member)
        {
            ReturnValue = returnValue;
            Members = new[] { member };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberNotNullWhenAttribute"/> class with several members.
        /// </summary>
        /// <param name="returnValue">The return value for which the members are not null.</param>
        /// <param name="members">The names of the members.</param>
        public MemberNotNullWhenAttribute(bool returnValue, params string[] members)
        {
            ReturnValue = returnValue;
            Members = members;
        }

        /// <summary>
        /// Gets the return value for which the members are not null.
        /// </summary>
        public bool ReturnValue { get; }

        /// <summary>
        /// Gets the names of the members.
        /// </summary>
        public string[] Members { get; }
    }
}

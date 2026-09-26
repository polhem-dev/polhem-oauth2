namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Specifies that an input argument is not null when the method returns.
    /// </summary>
    /// <remarks>
    /// netstandard2.0 does not include this attribute, so the netstandard2.0 build compiles this internal copy; the project
    /// file excludes it from the other targets. The compiler recognizes the attribute by its name.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, Inherited = false)]
    internal sealed class NotNullAttribute : Attribute
    {
    }
}

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Indicates that a parameter captures the expression passed for another parameter as a string.
    /// </summary>
    /// <remarks>
    /// netstandard2.0 does not include this attribute, so the netstandard2.0 build compiles this internal copy; the project
    /// file excludes it from the other targets. The compiler recognizes the attribute by its name.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class CallerArgumentExpressionAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CallerArgumentExpressionAttribute"/> class.
        /// </summary>
        /// <param name="parameterName">The name of the parameter whose expression is captured.</param>
        public CallerArgumentExpressionAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }

        /// <summary>
        /// Gets the name of the parameter whose expression is captured.
        /// </summary>
        public string ParameterName { get; }
    }
}

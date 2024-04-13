#pragma warning disable CA1032  // Implement standard exception constructors.
#pragma warning disable CA1819  // Properties should not return arrays.
#pragma warning disable CS1591  // Missing XML comment for publicly visible type or member.
#pragma warning disable MA0075  // Do not use implicit culture-sensitive ToString.
#pragma warning disable RCS1194 // Implement exception constructors.

namespace Cysharp.Diagnostics;

public class ProcessErrorException(int exitCode, string[] errorOutput) : Exception("Process returns error, ExitCode:" + exitCode + Environment.NewLine + string.Join(Environment.NewLine, errorOutput))
{
    public int ExitCode { get; } = exitCode;
    public string[] ErrorOutput { get; } = errorOutput;
}

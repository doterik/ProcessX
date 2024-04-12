namespace Cysharp.Diagnostics;

public class ProcessErrorException(int exitCode, string[] errorOutput) : Exception("Process returns error, ExitCode:" + exitCode + Environment.NewLine + string.Join(Environment.NewLine, errorOutput))
{
    public int ExitCode { get; } = exitCode;
    public string[] ErrorOutput { get; } = errorOutput;
}

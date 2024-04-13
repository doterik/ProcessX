#pragma warning disable CA1310 // Specify StringComparison for correctness.
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member.
#pragma warning disable MA0004 // Use Task.ConfigureAwait.
#pragma warning disable MA0009 // Add regex evaluation timeout.
#pragma warning disable MA0074 // Avoid implicit culture-sensitive methods.
#pragma warning disable MA0110 // Use the Regex source generator.
#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Zx;

public static class StringProcessExtensions
{
    public static TaskAwaiter<string> GetAwaiter(this string command) => ProcessCommand(command).GetAwaiter();

    public static TaskAwaiter GetAwaiter(this string[] commands)
    {
        async Task ProcessCommands() => _ = await Task.WhenAll(commands.Select(ProcessCommand));

        return ProcessCommands().GetAwaiter();
    }

    private static Task<string> ProcessCommand(string command) => TryChangeDirectory(command) ? Task.FromResult(string.Empty) : Env.Process(command);

    private static bool TryChangeDirectory(string command)
    {
        if (command.StartsWith("cd ") || command.StartsWith("chdir "))
        {
            var path = Regex.Replace(command, "^cd|^chdir", string.Empty).Trim();
            Environment.CurrentDirectory = Path.Combine(Environment.CurrentDirectory, path);
            return true;
        }

        return false;
    }
}

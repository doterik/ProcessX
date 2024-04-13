#pragma warning disable CA1054  // URI-like parameters should not be strings.
#pragma warning disable CA2000  // Dispose objects before losing scope.
#pragma warning disable CA2234  // Pass system uri objects instead of strings.
#pragma warning disable CS1591  // Missing XML comment for publicly visible type or member.
#pragma warning disable IDE1006 // Naming Styles.
#pragma warning disable MA0002  // IEqualityComparer<string> or IComparer<string> is missing.
#pragma warning disable MA0004  // Use Task.ConfigureAwait.
#pragma warning disable MA0040  // Forward the CancellationToken parameter to methods that take one.
#pragma warning disable S3358   // Ternary operators should not be nested.
#pragma warning disable SA1309  // Field names should not begin with underscore.

using System.Text;
using Cysharp.Diagnostics;

namespace Zx;

public static class Env
{
    public static bool Verbose { get; set; } = true;

    private static string? _shell;
    public static string Shell
    {
        get
        {
            _shell ??= OperatingSystem.IsWindows()
                    ? "cmd /c"
                    : Which.TryGetPath("bash", out var bashPath)
                        ? bashPath + " -c"
                        : throw new InvalidOperationException("shell is not found in PATH, set Env.shell manually.");

            return _shell;
        }
        set => _shell = value;
    }

    private static readonly Lazy<CancellationTokenSource> _terminateTokenSource = new(() =>
    {
        var source = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) => source.Cancel();
        return source;
    });

    public static CancellationToken TerminateToken => _terminateTokenSource.Value.Token;

    public static string? WorkingDirectory { get; set; }

    private static readonly Lazy<IDictionary<string, string>> _envVars = new(() => new Dictionary<string, string>());

    public static IDictionary<string, string> EnvVars => _envVars.Value;

    public static Task<HttpResponseMessage> Fetch(string requestUri) => new HttpClient().GetAsync(requestUri);

    public static Task<string> FetchText(string requestUri) => new HttpClient().GetStringAsync(requestUri);

    public static Task<byte[]> FetchBytes(string requestUri) => new HttpClient().GetByteArrayAsync(requestUri);

    public static Task Sleep(int seconds, CancellationToken cancellationToken = default) => Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);

    public static Task Sleep(TimeSpan timeSpan, CancellationToken cancellationToken = default) => Task.Delay(timeSpan, cancellationToken);

    public static async Task<string> WithTimeout(FormattableString command, int seconds)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        return (await ProcessStartAsync(EscapeFormattableString.Escape(command), cts.Token)).StdOut;
    }

    public static async Task<string> WithTimeout(FormattableString command, TimeSpan timeSpan)
    {
        using var cts = new CancellationTokenSource(timeSpan);
        return (await ProcessStartAsync(EscapeFormattableString.Escape(command), cts.Token)).StdOut;
    }

    public static async Task<(string StdOut, string StdError)> WithTimeout2(FormattableString command, int seconds)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        return await ProcessStartAsync(EscapeFormattableString.Escape(command), cts.Token);
    }

    public static async Task<(string StdOut, string StdError)> WithTimeout2(FormattableString command, TimeSpan timeSpan)
    {
        using var cts = new CancellationTokenSource(timeSpan);
        return await ProcessStartAsync(EscapeFormattableString.Escape(command), cts.Token);
    }

    public static async Task<string> WithCancellation(FormattableString command, CancellationToken cancellationToken) => (await ProcessStartAsync(EscapeFormattableString.Escape(command), cancellationToken)).StdOut;

    public static async Task<(string StdOut, string StdError)> WithCancellation2(FormattableString command, CancellationToken cancellationToken) => await ProcessStartAsync(EscapeFormattableString.Escape(command), cancellationToken);

    public static Task<string> Run(FormattableString command, CancellationToken cancellationToken = default) => Process(EscapeFormattableString.Escape(command), cancellationToken);

    public static Task<(string StdOut, string StdError)> Run2(FormattableString command, CancellationToken cancellationToken = default) => Process2(EscapeFormattableString.Escape(command), cancellationToken);

    public static Task<string[]> Runl(FormattableString command, CancellationToken cancellationToken = default) => Processl(EscapeFormattableString.Escape(command), cancellationToken);

    public static Task<(string[] StdOut, string[] StdError)> Runl2(FormattableString command, CancellationToken cancellationToken = default) => Processl2(EscapeFormattableString.Escape(command), cancellationToken);

    public static string Escape(FormattableString command) => EscapeFormattableString.Escape(command);

    public static async Task<string> Process(string command, CancellationToken cancellationToken = default) => (await ProcessStartAsync(command, cancellationToken)).StdOut;

    public static async Task<(string StdOut, string StdError)> Process2(string command, CancellationToken cancellationToken = default) => await ProcessStartAsync(command, cancellationToken);

    public static async Task<string[]> Processl(string command, CancellationToken cancellationToken = default) => (await ProcessStartListAsync(command, cancellationToken)).StdOut;

    public static async Task<(string[] StdOut, string[] StdError)> Processl2(string command, CancellationToken cancellationToken = default) => await ProcessStartListAsync(command, cancellationToken);

    public static async Task<T> Ignore<T>(Task<T> task)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (ProcessErrorException)
        {
            return default!; // !
        }
    }

    public static async Task<string> Question(string question)
    {
        Console.WriteLine(question);
        var str = await Console.In.ReadLineAsync();
        return str ?? string.Empty;
    }

    public static void Log(object? value, ConsoleColor? color = default)
    {
        if (color is not null)
        {
            using (Color(color.Value))
            {
                Console.WriteLine(value);
            }
        }
        else
        {
            Console.WriteLine(value);
        }
    }

    public static IDisposable Color(ConsoleColor color)
    {
        var current = Console.ForegroundColor;
        Console.ForegroundColor = color;
        return new ColorScope(current);
    }

    private static async Task<(string StdOut, string StdError)> ProcessStartAsync(string command, CancellationToken cancellationToken, bool forceSilcent = false)
    {
        var cmd = $@"{Shell} ""{command}""";
        var sbOut = new StringBuilder();
        var sbError = new StringBuilder();

        var (_, stdout, stderror) = Cysharp.Diagnostics.ProcessX.GetDualAsyncEnumerable(cmd, WorkingDirectory, EnvVars);

        var runStdout = Task.Run(async () =>
        {
            var isFirst = true;
            await foreach (var item in stdout.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (isFirst) isFirst = false;
                else _ = sbOut.AppendLine();

                _ = sbOut.Append(item);

                if (Verbose && !forceSilcent) Console.WriteLine(item);
            }
        });

        var runStdError = Task.Run(async () =>
        {
            var isFirst = true;
            await foreach (var item in stderror.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (isFirst) isFirst = false;
                else _ = sbOut.AppendLine();

                _ = sbError.Append(item);

                if (Verbose && !forceSilcent) Console.WriteLine(item);
            }
        });

        await Task.WhenAll(runStdout, runStdError).ConfigureAwait(false);

        return (sbOut.ToString(), sbError.ToString());
    }

    private static async Task<(string[] StdOut, string[] StdError)> ProcessStartListAsync(string command, CancellationToken cancellationToken, bool forceSilcent = false)
    {
        var cmd = $@"{Shell} ""{command}""";
        var sbOut = new List<string>();
        var sbError = new List<string>();

        var (_, stdout, stderror) = Cysharp.Diagnostics.ProcessX.GetDualAsyncEnumerable(cmd, WorkingDirectory, EnvVars);

        var runStdout = Task.Run(async () =>
        {
            await foreach (var item in stdout.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                sbOut.Add(item);

                if (Verbose && !forceSilcent) Console.WriteLine(item);
            }
        });

        var runStdError = Task.Run(async () =>
        {
            await foreach (var item in stderror.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                sbError.Add(item);

                if (Verbose && !forceSilcent) Console.WriteLine(item);
            }
        });

        await Task.WhenAll(runStdout, runStdError).ConfigureAwait(false);

        return (sbOut.ToArray(), sbError.ToArray());
    }

    private sealed class ColorScope(ConsoleColor color) : IDisposable
    {
        private readonly ConsoleColor _color = color;

        public void Dispose() => Console.ForegroundColor = _color;
    }
}

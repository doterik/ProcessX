#pragma warning disable S3168  // "async" methods should not return "void".
#pragma warning disable S2930  // "IDisposables" should be disposed.
#pragma warning disable MA0051  // Method is too long.
#pragma warning disable CA1307  // Specify StringComparison for clarity.
#pragma warning disable MA0001  // StringComparison is missing.
#pragma warning disable CA2000  // Dispose objects before losing scope.
#pragma warning disable CS1591  // Missing XML comment for publicly visible type or member.
#pragma warning disable MA0004  // Use Task.ConfigureAwait.
#pragma warning disable S3358   // Ternary operators should not be nested.

using System.Diagnostics;
using System.Text;
using System.Threading.Channels;

namespace Cysharp.Diagnostics;

public static class ProcessX
{
    public static IReadOnlyList<int> AcceptableExitCodes { get; set; } = [0];

    private static bool IsInvalidExitCode(Process process) => !AcceptableExitCodes.Any(x => x == process.ExitCode);

    private static (string FileName, string? Arguments) ParseCommand(string command)
    {
        var cmdBegin = command.IndexOf(' ');
        return cmdBegin == -1 ? (command, null) : (command[..cmdBegin], command[(cmdBegin + 1)..]);
    }

    private static Process SetupRedirectableProcess(ref ProcessStartInfo processStartInfo, bool redirectStandardInput)
    {
        // Override setings.
        processStartInfo.UseShellExecute = false;
        processStartInfo.CreateNoWindow = true;
        processStartInfo.ErrorDialog = false;
        processStartInfo.RedirectStandardError = true;
        processStartInfo.RedirectStandardOutput = true;
        processStartInfo.RedirectStandardInput = redirectStandardInput;

        return new Process()
        {
            StartInfo = processStartInfo,
            EnableRaisingEvents = true,
        };
    }

    public static ProcessAsyncEnumerable StartAsync(string command, string? workingDirectory = null, IDictionary<string, string>? environmentVariable = null, Encoding? encoding = null)
    {
        var (fileName, arguments) = ParseCommand(command);
        return StartAsync(fileName, arguments, workingDirectory, environmentVariable, encoding);
    }

    public static ProcessAsyncEnumerable StartAsync(string fileName, string? arguments, string? workingDirectory = null, IDictionary<string, string>? environmentVariable = null, Encoding? encoding = null)
    {
        var pi = new ProcessStartInfo()
        {
            FileName = fileName,
            Arguments = arguments,
        };

        if (workingDirectory is not null) pi.WorkingDirectory = workingDirectory;

        if (environmentVariable is not null)
        {
            foreach (var item in environmentVariable)
            {
                pi.EnvironmentVariables[item.Key] = item.Value;
            }
        }

        if (encoding is not null)
        {
            pi.StandardOutputEncoding = encoding;
            pi.StandardErrorEncoding = encoding;
        }

        return StartAsync(pi);
    }

    public static ProcessAsyncEnumerable StartAsync(ProcessStartInfo processStartInfo)
    {
        var process = SetupRedirectableProcess(ref processStartInfo, redirectStandardInput: false);

        var outputChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = true
        });

        var errorList = new List<string>();

        var waitOutputDataCompleted = new TaskCompletionSource<object?>();

        void OnOutputDataReceived(object sender, DataReceivedEventArgs e) => _ = e.Data is not null ? outputChannel?.Writer.TryWrite(e.Data) : waitOutputDataCompleted?.TrySetResult(null);

        process.OutputDataReceived += OnOutputDataReceived;

        var waitErrorDataCompleted = new TaskCompletionSource<object?>();
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data is not null)
            {
                lock (errorList)
                {
                    errorList.Add(e.Data);
                }
            }
            else
            {
                _ = waitErrorDataCompleted.TrySetResult(null);
            }
        };

        process.Exited += async (sender, e) =>
        {
            _ = await waitErrorDataCompleted.Task.ConfigureAwait(false);

            if (errorList.Count is 0)
            {
                _ = await waitOutputDataCompleted.Task.ConfigureAwait(false);
            }
            else
            {
                process.OutputDataReceived -= OnOutputDataReceived;
            }

            _ = IsInvalidExitCode(process)
                ? outputChannel.Writer.TryComplete(new ProcessErrorException(process.ExitCode, [.. errorList]))
                : errorList.Count is 0
                    ? outputChannel.Writer.TryComplete()
                    : outputChannel.Writer.TryComplete(new ProcessErrorException(process.ExitCode, [.. errorList]));
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Can't start process. FileName:{processStartInfo.FileName}, Arguments:{processStartInfo.Arguments}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return new ProcessAsyncEnumerable(process, outputChannel.Reader);
    }

    public static (Process Process, ProcessAsyncEnumerable StdOut, ProcessAsyncEnumerable StdError) GetDualAsyncEnumerable(string command, string? workingDirectory = null, IDictionary<string, string>? environmentVariable = null, Encoding? encoding = null)
    {
        var (fileName, arguments) = ParseCommand(command);
        return GetDualAsyncEnumerable(fileName, arguments, workingDirectory, environmentVariable, encoding);
    }

    public static (Process Process, ProcessAsyncEnumerable StdOut, ProcessAsyncEnumerable StdError) GetDualAsyncEnumerable(string fileName, string? arguments, string? workingDirectory = null, IDictionary<string, string>? environmentVariable = null, Encoding? encoding = null)
    {
        var pi = new ProcessStartInfo()
        {
            FileName = fileName,
            Arguments = arguments,
        };

        if (workingDirectory is not null) pi.WorkingDirectory = workingDirectory;

        if (environmentVariable is not null)
        {
            foreach (var item in environmentVariable)
            {
                pi.EnvironmentVariables.Add(item.Key, item.Value);
            }
        }

        if (encoding is not null)
        {
            pi.StandardOutputEncoding = encoding;
            pi.StandardErrorEncoding = encoding;
        }

        return GetDualAsyncEnumerable(pi);
    }

    public static (Process Process, ProcessAsyncEnumerable StdOut, ProcessAsyncEnumerable StdError) GetDualAsyncEnumerable(ProcessStartInfo processStartInfo)
    {
        var process = SetupRedirectableProcess(ref processStartInfo, redirectStandardInput: true);

        var outputChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = true
        });

        var errorChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = true
        });

        var waitOutputDataCompleted = new TaskCompletionSource<object?>();
        process.OutputDataReceived += (sender, e) => _ = e.Data is not null ? outputChannel.Writer.TryWrite(e.Data) : waitOutputDataCompleted.TrySetResult(null);

        var waitErrorDataCompleted = new TaskCompletionSource<object?>();
        process.ErrorDataReceived += (sender, e) => _ = e.Data is not null ? errorChannel.Writer.TryWrite(e.Data) : waitErrorDataCompleted.TrySetResult(null);

        process.Exited += async (sender, e) =>
        {
            _ = await waitErrorDataCompleted.Task.ConfigureAwait(false);
            _ = await waitOutputDataCompleted.Task.ConfigureAwait(false);

            if (IsInvalidExitCode(process))
            {
                _ = errorChannel.Writer.TryComplete();
                _ = outputChannel.Writer.TryComplete(new ProcessErrorException(process.ExitCode, []));
            }
            else
            {
                _ = errorChannel.Writer.TryComplete();
                _ = outputChannel.Writer.TryComplete();
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Can't start process. FileName:{processStartInfo.FileName}, Arguments:{processStartInfo.Arguments}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Error itertor does not handle process itself.
        return (process, new ProcessAsyncEnumerable(process, outputChannel.Reader), new ProcessAsyncEnumerable(process: null, errorChannel.Reader));
    }

    // Binary

    public static Task<byte[]> StartReadBinaryAsync(string command, string? workingDirectory = null, IDictionary<string, string>? environmentVariable = null, Encoding? encoding = null)
    {
        var (fileName, arguments) = ParseCommand(command);
        return StartReadBinaryAsync(fileName, arguments, workingDirectory, environmentVariable, encoding);
    }

    public static Task<byte[]> StartReadBinaryAsync(string fileName, string? arguments, string? workingDirectory = null, IDictionary<string, string>? environmentVariable = null, Encoding? encoding = null)
    {
        var pi = new ProcessStartInfo()
        {
            FileName = fileName,
            Arguments = arguments,
        };

        if (workingDirectory is not null) pi.WorkingDirectory = workingDirectory;

        if (environmentVariable is not null)
        {
            foreach (var item in environmentVariable)
            {
                pi.EnvironmentVariables.Add(item.Key, item.Value);
            }
        }

        if (encoding is not null)
        {
            pi.StandardOutputEncoding = encoding;
            pi.StandardErrorEncoding = encoding;
        }

        return StartReadBinaryAsync(pi);
    }

    public static Task<byte[]> StartReadBinaryAsync(ProcessStartInfo processStartInfo)
    {
        var process = SetupRedirectableProcess(ref processStartInfo, redirectStandardInput: false);

        var errorList = new List<string>();

        var cts = new CancellationTokenSource();
        var resultTask = new TaskCompletionSource<byte[]>();
        var readTask = new TaskCompletionSource<byte[]?>();

        var waitErrorDataCompleted = new TaskCompletionSource<object?>();
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data is not null)
            {
                lock (errorList)
                {
                    errorList.Add(e.Data);
                }
            }
            else
            {
                _ = waitErrorDataCompleted.TrySetResult(null);
            }
        };

        process.Exited += async (sender, e) =>
        {
            _ = await waitErrorDataCompleted.Task.ConfigureAwait(false);

            if (errorList.Count is 0 && !IsInvalidExitCode(process))
            {
                var resultBin = await readTask.Task.ConfigureAwait(false);
                if (resultBin is not null)
                {
                    _ = resultTask.TrySetResult(resultBin);
                    return;
                }
            }

            cts.Cancel();

            _ = resultTask.TrySetException(new ProcessErrorException(process.ExitCode, [.. errorList]));
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Can't start process. FileName:{processStartInfo.FileName}, Arguments:{processStartInfo.Arguments}");
        }

        RunAsyncReadFully(process.StandardOutput.BaseStream, readTask, cts.Token);
        process.BeginErrorReadLine();

        return resultTask.Task;
    }

    private static async void RunAsyncReadFully(Stream stream, TaskCompletionSource<byte[]?> completion, CancellationToken cancellationToken)
    {
        try
        {
            var ms = new MemoryStream();
            await stream.CopyToAsync(ms, 81920, cancellationToken);
            var result = ms.ToArray();
            _ = completion.TrySetResult(result);
        }
        catch
        {
            _ = completion.TrySetResult(null);
        }
    }
}

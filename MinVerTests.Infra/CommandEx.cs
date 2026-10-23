using System.Collections.Concurrent;
using System.Threading;
using SimpleExec;

namespace MinVerTests.Infra;

internal static class CommandEx
{
    private static readonly ConcurrentDictionary<string, int> indices = new();
    private static readonly TimeSpan commandTimeout = TimeSpan.FromMinutes(2);

    public static async Task<(string StandardOutput, string StandardError)> ReadLoggedAsync(string name, string args = "", string workingDirectory = "", IEnumerable<KeyValuePair<string, string>>? envVars = null, Func<int, bool>? handleExitCode = null)
    {
        envVars = (envVars ?? Enumerable.Empty<KeyValuePair<string, string>>()).ToList();

        (string StandardOutput, string StandardError) result;
        using (var cancellationTokenSource = new CancellationTokenSource(commandTimeout))
        {
            try
            {
                result = await Command.ReadAsync(
                    name,
                    args,
                    workingDirectory,
                    configureEnvironment: env =>
                    {
                        foreach (var (key, value) in envVars)
                        {
                            env[key] = value;
                        }
                    },
                    handleExitCode: handleExitCode,
                    cancellationIgnoresProcessTree: false,
                    cancellationToken: cancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (cancellationTokenSource.IsCancellationRequested)
            {
                throw new TimeoutException($"Command '{name} {args}' exceeded the {commandTimeout} timeout in '{workingDirectory}'.", exception);
            }
        }

        int index;

        lock (indices)
        {
            index = indices.GetOrAdd(workingDirectory, 0);
            indices[workingDirectory] = index + 1;
        }

        var markdown =
            $@"
# Command read {index}

## Command

### Name

`{name}`

### Args

`{args}`

### Working directory

`{workingDirectory}`

### Environment variables

```text
{string.Join(Environment.NewLine, envVars.Select(pair => $"{pair.Key}={pair.Value}"))}
```

## Result

### StandardOutput (stdout)

```text
{result.StandardOutput}
```

### StandardError (stderr)

```text
{result.StandardError}
```
";

        var markdownFileName = Path.Combine(workingDirectory, $"command-read-{index:D2}.md");

        await File.WriteAllTextAsync(markdownFileName, markdown).ConfigureAwait(false);

        return result;
    }
}

using System.Diagnostics;

// The ENet host owns finite test-child output. Drain both pipes immediately,
// then publish evidence from awaited cleanup rather than background callbacks.
internal sealed class HeadlessProcessOutput
{
    internal Process Process { get; }
    private readonly Task<string> output;
    private readonly Task<string> errors;

    internal HeadlessProcessOutput(ProcessStartInfo start)
    {
        Process = Process.Start(start) ?? throw new InvalidOperationException("The test child did not start.");
        output = Process.StandardOutput.ReadToEndAsync();
        errors = Process.StandardError.ReadToEndAsync();
    }

    internal async Task<int> Finish(string outputPath, string errorPath)
    {
        try
        {
            if (!Process.HasExited) Process.Kill(entireProcessTree: true);
            await Process.WaitForExitAsync();
            await Task.WhenAll(output, errors);
            await Task.WhenAll(File.WriteAllTextAsync(outputPath, await output),
                File.WriteAllTextAsync(errorPath, await errors));
            return Process.ExitCode;
        }
        finally { Process.Dispose(); }
    }
}

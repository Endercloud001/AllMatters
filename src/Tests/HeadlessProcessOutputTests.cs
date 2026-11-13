using System.Diagnostics;

internal static class HeadlessProcessOutputTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Headless process output retains both complete streams and nonzero exit while evidence is read", CompleteStreams),
        ("Headless process output kills and drains a timed out child before publishing evidence", TimeoutCleanup),
        ("Headless process output reports evidence write failure without losing the other stream", WriteFailure),
    ];

    private static ProcessStartInfo Child(string command)
    {
        var start = new ProcessStartInfo("pwsh")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-Command", command })
            start.ArgumentList.Add(argument);
        return start;
    }

    private static async Task CompleteStreams()
    {
        string directory = Path.Combine(Path.GetTempPath(), "allmatters-child-output-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        string stdout = Path.Combine(directory, "child.log"), stderr = Path.Combine(directory, "child.err");
        var child = new HeadlessProcessOutput(Child(
            "for($i=0;$i -lt 6000;$i++){[Console]::Out.WriteLine('out-'+$i);[Console]::Error.WriteLine('err-'+$i)};" +
            "[Console]::Out.Write('out-tail');[Console]::Error.Write('err-tail');exit 7"));
        bool finished = false;
        try
        {
            File.WriteAllText(stdout, "existing stdout evidence");
            File.WriteAllText(stderr, "existing stderr evidence");
            // This explicitly injected reader rejects concurrent writes. It is
            // not an assertion about who held the original failing ENet file.
            using (var outputReader = new FileStream(stdout, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var errorReader = new FileStream(stderr, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await child.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                Check(File.ReadAllText(stdout) == "existing stdout evidence" &&
                    File.ReadAllText(stderr) == "existing stderr evidence", "Running child output must not rewrite live evidence files.");
            }
            finished = true;
            int exit = await child.Finish(stdout, stderr);
            string expectedOutput = string.Concat(Enumerable.Range(0, 6000).Select(i => "out-" + i + Environment.NewLine)) + "out-tail";
            string expectedError = string.Concat(Enumerable.Range(0, 6000).Select(i => "err-" + i + Environment.NewLine)) + "err-tail";
            Check(exit == 7 && File.ReadAllText(stdout) == expectedOutput && File.ReadAllText(stderr) == expectedError,
                "Both full pipes, including final text without a newline, and the child's nonzero exit must be retained.");
        }
        finally
        {
            if (!finished) await child.Finish(stdout, stderr);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task TimeoutCleanup()
    {
        string directory = Path.Combine(Path.GetTempPath(), "allmatters-child-timeout-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        string stdout = Path.Combine(directory, "child.log"), stderr = Path.Combine(directory, "child.err");
        string ready = Path.Combine(directory, "ready");
        var start = Child("[Console]::Out.Write('before-kill');[Console]::Error.Write('error-before-kill');" +
            "[Console]::Out.Flush();[Console]::Error.Flush();[IO.File]::WriteAllText($env:ALLMATTERS_CHILD_READY,'ready');Start-Sleep -Seconds 30");
        start.Environment["ALLMATTERS_CHILD_READY"] = ready;
        var child = new HeadlessProcessOutput(start);
        bool timedOut = false;
        try
        {
            var deadline = Stopwatch.StartNew();
            while (!File.Exists(ready) && deadline.Elapsed < TimeSpan.FromSeconds(10)) await Task.Delay(10);
            Check(File.Exists(ready), "The real child must flush both streams before the timeout is injected.");
            try { await child.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromMilliseconds(50)); }
            catch (TimeoutException) { timedOut = true; }
        }
        finally
        {
            int exit = await child.Finish(stdout, stderr);
            Check(timedOut && exit != 0 && File.ReadAllText(stdout) == "before-kill" &&
                File.ReadAllText(stderr) == "error-before-kill", "Timeout cleanup must kill, await exit and drain both real streams.");
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task WriteFailure()
    {
        string directory = Path.Combine(Path.GetTempPath(), "allmatters-child-write-failure-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        string stdout = Path.Combine(directory, "child.log"), stderr = Path.Combine(directory, "child.err");
        var child = new HeadlessProcessOutput(Child("[Console]::Out.Write('output');[Console]::Error.Write('error');exit 3"));
        bool finished = false;
        try
        {
            await child.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            File.WriteAllText(stdout, "unchanged evidence");
            bool rejected = false;
            using (var reader = new FileStream(stdout, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                finished = true;
                try { await child.Finish(stdout, stderr); }
                catch (IOException) { rejected = true; }
            }
            Check(rejected && File.ReadAllText(stdout) == "unchanged evidence" && File.ReadAllText(stderr) == "error",
                "Evidence write failure must reach the awaited caller while the other stream still gets published.");
        }
        finally
        {
            if (!finished) await child.Finish(stdout, stderr);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

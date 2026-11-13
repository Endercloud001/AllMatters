using System.Diagnostics;

internal static class JigsawWinnerEvidenceTests
{
    internal static readonly (string Name, Func<Task> Test)[] Cases =
    [
        ("Jigsaw winner evidence publishes complete content after a temporary sharing refusal", TemporaryReader),
        ("Jigsaw winner evidence reports a persistent sharing refusal within a short bound", PersistentReader),
        ("Jigsaw winner evidence rejects an existing destination immediately without overwriting", ExistingDestination),
    ];

    private static async Task TemporaryReader()
    {
        string directory = Directory.CreateTempSubdirectory("allmatters-winner-temporary-").FullName;
        string source = Path.Combine(directory, "3.winner.tmp"), target = Path.Combine(directory, "3.winner");
        const string payload = "1:1,3,2";
        File.WriteAllText(source, payload);
        var reader = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        Task? publication = null;
        try
        {
            publication = JigsawNetworkTests.PublishWinnerEvidence(source, target);
            await Task.Delay(50);
            Check(!publication.IsCompleted && !File.Exists(target) && File.ReadAllText(source) == payload,
                "While the reader refuses rename, the complete temporary content must remain unpublished.");
            reader.Dispose();
            await publication;
            Check(!File.Exists(source) && File.ReadAllText(target) == payload,
                "After the temporary reader leaves, only the complete original content may be atomically published.");
        }
        finally
        {
            reader.Dispose();
            if (publication != null) try { await publication; } catch (IOException) { }
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task PersistentReader()
    {
        string directory = Directory.CreateTempSubdirectory("allmatters-winner-persistent-").FullName;
        string source = Path.Combine(directory, "host.winner.tmp"), target = Path.Combine(directory, "host.winner");
        File.WriteAllText(source, "1:1,3,2");
        try
        {
            using var reader = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
            var clock = Stopwatch.StartNew();
            IOException? rejected = null;
            try { await JigsawNetworkTests.PublishWinnerEvidence(source, target); }
            catch (IOException error) { rejected = error; }
            Check(rejected != null && (rejected.HResult & 0xffff) is 32 or 33 &&
                clock.Elapsed >= TimeSpan.FromMilliseconds(400) && clock.Elapsed < TimeSpan.FromSeconds(2),
                "A persistent sharing refusal must remain an explicit IOException after a short bounded wait.");
            Check(File.ReadAllText(source) == "1:1,3,2" && !File.Exists(target),
                "A failed publication must preserve complete temporary evidence without exposing a final file.");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static async Task ExistingDestination()
    {
        string directory = Directory.CreateTempSubdirectory("allmatters-winner-existing-").FullName;
        string source = Path.Combine(directory, "2.winner.tmp"), target = Path.Combine(directory, "2.winner");
        File.WriteAllText(source, "new evidence");
        File.WriteAllText(target, "original evidence");
        try
        {
            IOException? rejected = null;
            Task? publication = null;
            try
            {
                publication = JigsawNetworkTests.PublishWinnerEvidence(source, target);
                Check(publication.IsCompleted, "An existing destination must not enter sharing-refusal waits.");
                await publication;
            }
            catch (IOException error) { rejected = error; }
            Check(rejected != null && (rejected.HResult & 0xffff) is not (32 or 33) &&
                File.ReadAllText(source) == "new evidence" && File.ReadAllText(target) == "original evidence",
                "Other IO failures must propagate immediately without overwriting either evidence file.");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

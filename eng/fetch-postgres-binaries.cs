// File-based .NET app: downloads, verifies and extracts the pinned PostgreSQL binaries.
// Usage: dotnet eng/fetch-postgres-binaries.cs <lock-file> <output-dir> <platform>...
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: fetch-postgres-binaries <lock-file> <output-dir> <platform>...");
    return 2;
}

var lockFile = args[0];
var outputDir = Path.GetFullPath(args[1]);
var entries = File.ReadAllLines(lockFile)
    .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith('#'))
    .Select(l => l.Split('\t'))
    .ToDictionary(p => p[0], p => (Version: p[1], Sha256: p[2], Url: p[3]));

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

foreach (var platform in args.Skip(2))
{
    if (!entries.TryGetValue(platform, out var entry))
    {
        Console.Error.WriteLine($"Platform '{platform}' is not in {lockFile}.");
        return 1;
    }

    var target = Path.Combine(outputDir, platform);
    var stamp = Path.Combine(target, ".fetched");
    var stampValue = $"{entry.Version} {entry.Sha256}";
    if (File.Exists(stamp) && File.ReadAllText(stamp) == stampValue)
    {
        Console.WriteLine($"{platform}: up to date ({entry.Version}).");
        continue;
    }

    Console.WriteLine($"{platform}: downloading {entry.Url}");
    var work = Path.Combine(outputDir, $".work-{platform}");
    if (Directory.Exists(work)) Directory.Delete(work, true);
    Directory.CreateDirectory(work);

    var jar = Path.Combine(work, "artifact.jar");
    await using (var net = await http.GetStreamAsync(entry.Url))
    await using (var file = File.Create(jar))
    {
        await net.CopyToAsync(file);
    }

    string actual;
    await using (var file = File.OpenRead(jar))
    {
        actual = Convert.ToHexString(await SHA256.HashDataAsync(file)).ToLowerInvariant();
    }

    if (actual != entry.Sha256)
    {
        Directory.Delete(work, true);
        Console.Error.WriteLine($"{platform}: SHA-256 mismatch. Expected {entry.Sha256}, got {actual}.");
        return 1;
    }

    // The .jar is a zip holding one .txz archive; .NET has no xz support, so tar extracts it.
    var txz = Path.Combine(work, "postgres.txz");
    using (var zip = ZipFile.OpenRead(jar))
    {
        var inner = zip.Entries.Single(e => e.Name.EndsWith(".txz", StringComparison.Ordinal));
        inner.ExtractToFile(txz, true);
    }

    var extracted = Path.Combine(work, "extracted");
    Directory.CreateDirectory(extracted);
    var tar = new ProcessStartInfo("tar") { RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var a in new[] { "-xJf", txz, "-C", extracted }) tar.ArgumentList.Add(a);
    using (var p = Process.Start(tar) ?? throw new InvalidOperationException("tar did not start."))
    {
        var err = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        if (p.ExitCode != 0)
        {
            Console.Error.WriteLine($"{platform}: tar failed ({p.ExitCode}): {err}");
            return 1;
        }
    }

    if (Directory.Exists(target)) Directory.Delete(target, true);
    Directory.Move(extracted, target);
    File.WriteAllText(Path.Combine(target, ".fetched"), stampValue);
    Directory.Delete(work, true);
    Console.WriteLine($"{platform}: extracted to {target}");
}

return 0;

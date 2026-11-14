using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Core.Services;

public sealed class VideoInspector(IConfiguration config)
{
    private readonly SemaphoreSlim slots = new(4);
    public async Task<double?> DurationAsync(string path, CancellationToken cancellationToken)
    {
        await slots.WaitAsync(cancellationToken);
        try
        {
            var start = new ProcessStartInfo(config["Media:FFprobePath"] ?? "ffprobe") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var arg in new[] { "-v", "error", "-protocol_whitelist", "file", "-show_entries", "format=duration:stream=codec_type,width,height", "-of", "json", path }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Video validation unavailable.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errors = process.StandardError.ReadToEndAsync(timeout.Token);
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException) { if (!process.HasExited) process.Kill(entireProcessTree: true); return null; }
            await errors;
            if (process.ExitCode != 0) return null;
            using var doc = JsonDocument.Parse(await output);
            if (!doc.RootElement.TryGetProperty("streams", out var streams) || !streams.EnumerateArray().Any(s => s.GetProperty("codec_type").GetString() == "video" && s.TryGetProperty("width", out var width) && width.GetInt32() > 0)) return null;
            if (!doc.RootElement.GetProperty("format").TryGetProperty("duration", out var duration)) return null;
            return double.TryParse(duration.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds is > 0 and <= 180 ? seconds : null;
        }
        catch (JsonException) { return null; }
        finally { slots.Release(); }
    }
}

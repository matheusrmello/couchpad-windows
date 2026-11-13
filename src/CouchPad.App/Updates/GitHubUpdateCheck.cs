using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CouchPad.App.Updates;

internal sealed class GitHubUpdateCheck(HttpClient client, string cachePath, TimeProvider clock)
{
    private const string Repository = "matheusrmello/couchpad-windows";
    private readonly SemaphoreSlim _gate = new(1);
    private Cache? _cache;

    internal static Version InstalledVersion { get; } =
        typeof(GitHubUpdateCheck).Assembly.GetName().Version ?? new Version(0, 0, 0);

    internal async Task<UpdateNotice?> CheckAsync(Version installed, bool includePreviews, CancellationToken token)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            _cache ??= ReadCache();
            var now = clock.GetUtcNow();
            if (_cache is null || now - _cache.CheckedAt >= TimeSpan.FromDays(1))
            {
                // Record attempts as well as successes so offline startups don't keep retrying.
                _cache = new Cache(now, []);
                SaveCache();
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get,
                        $"https://api.github.com/repos/{Repository}/releases?per_page=100");
                    request.Headers.UserAgent.ParseAdd("CouchPad");
                    request.Headers.Accept.ParseAdd("application/vnd.github+json");
                    using var response = await client.SendAsync(request, token).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    var releases = await response.Content.ReadFromJsonAsync<Release[]>(cancellationToken: token)
                        .ConfigureAwait(false);
                    _cache = new Cache(now, releases ?? []);
                    SaveCache();
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException or OperationCanceledException)
                {
                    token.ThrowIfCancellationRequested();
                }
            }
            return Select(_cache.Releases, installed, includePreviews);
        }
        finally { _gate.Release(); }
    }

    internal static UpdateNotice? Select(IEnumerable<Release> releases, Version installed, bool includePreviews)
    {
        var current = new Version(installed.Major, installed.Minor, Math.Max(0, installed.Build));
        return releases
            .Where(r => !r.Draft && (includePreviews || !r.Prerelease))
            .Select(r => (Release: r, Version: ParseTag(r.Tag)))
            .Where(r => r.Version is not null && r.Version > current)
            .Where(r => r.Release.Assets?.Any(a => a.State == "uploaded" &&
                a.Name == $"CouchPad-Setup-{r.Version}-win-x64.exe") == true)
            .OrderByDescending(r => r.Version)
            .ThenBy(r => r.Release.Prerelease)
            .Select(r => new UpdateNotice(r.Version!, r.Release.Prerelease,
                new Uri($"https://github.com/{Repository}/releases/tag/v{r.Version}")))
            .FirstOrDefault();
    }

    private static Version? ParseTag(string? tag) =>
        tag is not null && tag.StartsWith('v') && Version.TryParse(tag[1..], out var parsed) &&
        parsed.Build >= 0 && parsed.Revision == -1 && tag == $"v{parsed}" ? parsed : null;

    private Cache? ReadCache()
    {
        try
        {
            var cache = JsonSerializer.Deserialize<Cache>(File.ReadAllText(cachePath));
            return cache?.Releases is not null && cache.CheckedAt <= clock.GetUtcNow() ? cache : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }

    private void SaveCache()
    {
        var temp = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            File.WriteAllText(temp, JsonSerializer.Serialize(_cache));
            File.Move(temp, cachePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        finally
        {
            try { File.Delete(temp); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private sealed record Cache(DateTimeOffset CheckedAt, Release[] Releases);
    internal sealed record Release([property: JsonPropertyName("tag_name")] string? Tag,
        bool Draft, bool Prerelease, Asset[]? Assets);
    internal sealed record Asset(string Name, string State);
}

public sealed record UpdateNotice(Version Version, bool IsPreview, Uri ReleasePage);

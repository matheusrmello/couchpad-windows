using System.Net;
using CouchPad.App.Updates;

namespace CouchPad.App.Tests;

public sealed class GitHubUpdateCheckTests
{
    [Fact]
    public void Selects_newer_eligible_installers_for_each_channel()
    {
        var releases = new[]
        {
            Release("1.0.10", preview: true), Release("1.0.9"), Release("1.0.20", draft: true),
            Release("1.0.30", assetState: "new"), Release("1.0.40-rc1"), Release("1.0.6"),
        };
        Assert.Equal(new Version(1, 0, 9), GitHubUpdateCheck.Select(releases, new Version(1, 0, 6, 0), false)?.Version);
        Assert.Equal(new Version(1, 0, 10), GitHubUpdateCheck.Select(releases, new Version(1, 0, 6, 0), true)?.Version);
        Assert.Null(GitHubUpdateCheck.Select(releases, new Version(1, 0, 10, 0), true));
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Caches_the_attempt_across_starts_and_reuses_it_for_channel_changes(HttpStatusCode status)
    {
        var cache = Path.Combine(Path.GetTempPath(), "couchpad-update-" + Guid.NewGuid().ToString("N") + ".json");
        var clock = new TestClock();
        using var handler = new ReleasesHandler(status);
        using var client = new HttpClient(handler);
        try
        {
            var version = new Version(1, 0, 6);
            Assert.Null(await new GitHubUpdateCheck(client, cache, clock).CheckAsync(version, false, default));
            var preview = await new GitHubUpdateCheck(client, cache, clock).CheckAsync(version, true, default);
            Assert.Equal(status == HttpStatusCode.OK ? new Version(1, 0, 7) : null, preview?.Version);
            Assert.Equal(1, handler.Requests);
            clock.Now = clock.Now.AddDays(1);
            await new GitHubUpdateCheck(client, cache, clock).CheckAsync(version, true, default);
            Assert.Equal(2, handler.Requests);
        }
        finally { File.Delete(cache); }
    }

    private static GitHubUpdateCheck.Release Release(string version, bool preview = false, bool draft = false,
        string assetState = "uploaded") => new("v" + version, draft, preview,
            [new($"CouchPad-Setup-{version}-win-x64.exe", assetState)]);

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class ReleasesHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Assert.Equal("api.github.com", request.RequestUri!.Host);
            Assert.Null(request.Headers.Authorization);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("""
                    [{"tag_name":"v1.0.7","draft":false,"prerelease":true,"assets":[{"name":"CouchPad-Setup-1.0.7-win-x64.exe","state":"uploaded"}]}]
                    """),
            });
        }
    }
}

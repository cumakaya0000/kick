using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using KickAutoRecorder.Core.Enums;
using KickAutoRecorder.Infrastructure.Kick;
using Xunit;

namespace KickAutoRecorder.IntegrationTests;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}

public class KickApiClientTests
{
    [Fact]
    public async Task CheckChannelStatusAsync_ShouldParseLiveStream()
    {
        var jsonResponse = """
        {
          "id": 123,
          "slug": "rraenee",
          "playback_url": "https://stream.kick.com/master.m3u8",
          "livestream": {
            "id": 999,
            "slug": "rraenee-stream",
            "session_title": "PUBG MOBA Pro Gaming",
            "viewer_count": 14500,
            "category": { "id": 5, "name": "PUBG: BATTLEGROUNDS" },
            "source": "https://stream.kick.com/master.m3u8"
          }
        }
        """;

        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
        });

        var client = new HttpClient(handler);
        var kickClient = new KickApiClient(client, NullLogger<KickApiClient>.Instance);

        var result = await kickClient.CheckChannelStatusAsync("rraenee");

        Assert.Equal("rraenee", result.Username);
        Assert.Equal(StreamStatus.Live, result.Status);
        Assert.Equal("PUBG MOBA Pro Gaming", result.StreamTitle);
        Assert.Equal("PUBG: BATTLEGROUNDS", result.Category);
        Assert.Equal(14500, result.ViewerCount);
        Assert.Equal("https://stream.kick.com/master.m3u8", result.PlaybackUrl);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task CheckChannelStatusAsync_ShouldParseOfflineStream()
    {
        var jsonResponse = """
        {
          "id": 123,
          "slug": "rraenee",
          "playback_url": null,
          "livestream": null
        }
        """;

        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
        });

        var client = new HttpClient(handler);
        var kickClient = new KickApiClient(client, NullLogger<KickApiClient>.Instance);

        var result = await kickClient.CheckChannelStatusAsync("rraenee");

        Assert.Equal("rraenee", result.Username);
        Assert.Equal(StreamStatus.Offline, result.Status);
        Assert.Empty(result.StreamTitle);
        Assert.Equal(0, result.ViewerCount);
    }

    [Fact]
    public async Task CheckChannelStatusAsync_ShouldHandleHttpErrorGracefully()
    {
        var handler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            ReasonPhrase = "Cloudflare Block"
        });

        var client = new HttpClient(handler);
        var kickClient = new KickApiClient(client, NullLogger<KickApiClient>.Instance);

        var result = await kickClient.CheckChannelStatusAsync("rraenee");

        Assert.Equal("rraenee", result.Username);
        Assert.Equal(StreamStatus.Error, result.Status);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("403", result.ErrorMessage);
    }
}

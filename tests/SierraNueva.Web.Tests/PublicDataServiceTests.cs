using System.Net;
using SierraNueva.Web.Services;

namespace SierraNueva.Web.Tests;

public sealed class PublicDataServiceTests
{
    [Fact]
    public async Task LoadAsync_UsesOneCacheTokenForEveryPublicDataset()
    {
        RecordingHandler handler = new();
        using HttpClient httpClient = new(handler)
        {
            BaseAddress = new("https://example.test/")
        };
        PublicDataService service = new(httpClient);

        await service.LoadAsync(CancellationToken.None);

        Assert.Equal(4, handler.Requests.Count);
        Assert.All(handler.Requests, uri => Assert.StartsWith("?v=", uri.Query));
        Assert.Single(handler.Requests.Select(uri => uri.Query).Distinct(StringComparer.Ordinal));
        Assert.Equal(
            [
                "/data/promotions.json",
                "/data/changes.json",
                "/data/run.json",
                "/data/promotions.geojson"
            ],
            handler.Requests.Select(uri => uri.AbsolutePath));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Uri uri = request.RequestUri ??
                      throw new InvalidOperationException("La solicitud no tiene URI.");
            Requests.Add(uri);
            string content = uri.AbsolutePath.EndsWith(
                ".geojson",
                StringComparison.OrdinalIgnoreCase)
                ? """{"type":"FeatureCollection","features":[]}"""
                : "{}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
        }
    }
}

using System.Net;
using OnlyWinget.Application.Winget;
using OnlyWinget.Infrastructure.Winget;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class WingetRestSourceClientTests
{
    [Fact]
    public async Task GetPackageManifestAsync_ReturnsManifestOnSuccess()
    {
        var jsonResponse = """
        {
          "Data": {
            "PackageIdentifier": "Git.Git",
            "PackageName": "Git for Windows",
            "Publisher": "Git",
            "Author": "Git Community",
            "License": "GPL-2.0",
            "ShortDescription": "Distributed version control system",
            "Versions": ["2.45.0", "2.44.0"]
          }
        }
        """;

        var handler = new MockHttpMessageHandler(jsonResponse, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var client = new WingetRestSourceClient(httpClient);

        var manifest = await client.GetPackageManifestAsync("https://rest.winget.source/v1", "Git.Git");

        Assert.NotNull(manifest);
        Assert.Equal("Git.Git", manifest.PackageIdentifier);
        Assert.Equal("Git for Windows", manifest.PackageName);
        Assert.Equal("Git", manifest.Publisher);
        Assert.Equal(2, manifest.PackageVersions.Count);
    }

    [Fact]
    public async Task GetPackageManifestAsync_ReturnsNullOnNotFound()
    {
        var handler = new MockHttpMessageHandler("", HttpStatusCode.NotFound);
        var httpClient = new HttpClient(handler);
        var client = new WingetRestSourceClient(httpClient);

        var manifest = await client.GetPackageManifestAsync("https://rest.winget.source/v1", "NonExistent.App");

        Assert.Null(manifest);
    }

    [Fact]
    public async Task SearchPackagesAsync_ReturnsPackagesOnSuccess()
    {
        var jsonResponse = """
        {
          "Data": [
            {
              "PackageIdentifier": "Microsoft.VisualStudioCode",
              "PackageName": "Visual Studio Code",
              "Publisher": "Microsoft",
              "Versions": ["1.90.0"]
            }
          ]
        }
        """;

        var handler = new MockHttpMessageHandler(jsonResponse, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var client = new WingetRestSourceClient(httpClient);

        var results = await client.SearchPackagesAsync("https://rest.winget.source/v1", "vscode");

        Assert.Single(results);
        Assert.Equal("Microsoft.VisualStudioCode", results[0].PackageIdentifier);
        Assert.Equal("Visual Studio Code", results[0].PackageName);
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string content;
        private readonly HttpStatusCode statusCode;

        public MockHttpMessageHandler(string content, HttpStatusCode statusCode)
        {
            this.content = content;
            this.statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}

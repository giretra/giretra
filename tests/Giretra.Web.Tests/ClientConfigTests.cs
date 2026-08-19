using Giretra.Web.Models;
using Giretra.Web.Models.Responses;
using Microsoft.Extensions.Configuration;

namespace Giretra.Web.Tests;

public class ClientConfigTests
{
    [Fact]
    public void MobileClientOptions_BindsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MobileClient:MinSupportedVersion"] = "1.2.0",
                ["MobileClient:LatestVersion"] = "1.4.1",
                ["MobileClient:StoreUrls:Android"] = "https://play.google.com/store/apps/details?id=com.giretra.app",
                ["MobileClient:StoreUrls:Ios"] = "https://apps.apple.com/app/id0000000000"
            })
            .Build();

        var options = config.GetSection(MobileClientOptions.SectionName).Get<MobileClientOptions>()!;

        Assert.Equal("1.2.0", options.MinSupportedVersion);
        Assert.Equal("1.4.1", options.LatestVersion);
        Assert.Equal("https://play.google.com/store/apps/details?id=com.giretra.app", options.StoreUrls.Android);
        Assert.Equal("https://apps.apple.com/app/id0000000000", options.StoreUrls.Ios);
    }

    [Fact]
    public void MobileClientOptions_UsesDefaults_WhenSectionMissing()
    {
        var config = new ConfigurationBuilder().Build();

        var options = config.GetSection(MobileClientOptions.SectionName).Get<MobileClientOptions>()
            ?? new MobileClientOptions();

        Assert.Equal("0.1.0", options.MinSupportedVersion);
        Assert.Equal("0.1.0", options.LatestVersion);
        Assert.Null(options.StoreUrls.Android);
        Assert.Null(options.StoreUrls.Ios);
    }

    [Fact]
    public void FromOptions_MapsAllFields()
    {
        var options = new MobileClientOptions
        {
            MinSupportedVersion = "1.0.0",
            LatestVersion = "2.0.0",
            StoreUrls = new MobileStoreUrls { Android = "https://example/android", Ios = null }
        };

        var response = ClientConfigResponse.FromOptions(options);

        Assert.Equal("1.0.0", response.MinSupportedMobileVersion);
        Assert.Equal("2.0.0", response.LatestMobileVersion);
        Assert.Equal("https://example/android", response.StoreUrls.Android);
        Assert.Null(response.StoreUrls.Ios);
    }
}

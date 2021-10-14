using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace IntegrationTests
{
    [TestClass]
    public class AzurePlatformImageTests
    {
        private ConfigurationHelper ConfigurationHelper { get; }

        public AzurePlatformImageTests()
        {
            ConfigurationHelper = new ConfigurationHelper();
        }

        [TestMethod]
        public async Task AzureVmImageAPI_should_return_platform_images_to_import()
        {
            List<AzureResponse> pubResponses = new();
            List<AzureResponse> offerResponses = new();
            List<AzureResponse> skuResponses = new();
            List<AzureResponse> versionResponses = new();
            List<AzureResponse> vmImageResponses = new();

            string bearerToken;

            using (HttpClient httpClient = new() { BaseAddress = new Uri("https://login.microsoftonline.com/") })
            {

                var authUrl = ConfigurationHelper.GetTenantId() + "/oauth2/token";

                var dict = new Dictionary<string, string> {
                        {"grant_type", "client_credentials"},
                        {"client_id", ConfigurationHelper.GetClientId()},
                        {"client_secret", ConfigurationHelper.GetClientSecret()},
                        {"resource", ConfigurationHelper.GetResource()}
                    };
                HttpContent content = new FormUrlEncodedContent(dict);

                var result = await httpClient.PostAsync(authUrl, content);

                Assert.IsTrue(result.IsSuccessStatusCode);
                string json = JsonConvert.DeserializeObject(await result.Content.ReadAsStringAsync())?.ToString();

                Assert.IsNotNull(json);
                var authResponse = JsonConvert.DeserializeObject<AuthResponse>(json ?? string.Empty);

                bearerToken = authResponse?.access_token;
                Assert.IsNotNull(bearerToken);
            }


            using (HttpClient httpClient = new() { BaseAddress = new Uri("https://management.azure.com/") })
            {

                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                string getPublishersUrl = "subscriptions/" + ConfigurationHelper.GetSubscriptionId() +
                                          "/providers/Microsoft.Compute/locations/" +
                                          ConfigurationHelper.GetLocation() +
                                          "/publishers?api-version=2021-07-01";


                var result = await httpClient.GetAsync(getPublishersUrl);

                if (result.IsSuccessStatusCode)
                {
                    string json = JsonConvert.DeserializeObject(await result.Content.ReadAsStringAsync())
                        ?.ToString();
                    pubResponses = JsonConvert.DeserializeObject<List<AzureResponse>>(json ?? string.Empty);

                    Assert.IsTrue(pubResponses is { Count: > 1600 });
                }
            }

            var filteredPubResponses = pubResponses.Where(x => x.name.Contains("microsoft")).ToList();

            foreach (var publisher in filteredPubResponses)
            {

                using (HttpClient httpClient = new() { BaseAddress = new Uri("https://management.azure.com/") })
                {

                    httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                    string getOffersUrl = "subscriptions/" + ConfigurationHelper.GetSubscriptionId() +
                                          "/providers/Microsoft.Compute/locations/" +
                                          ConfigurationHelper.GetLocation() +
                                          "/publishers/" + publisher.name +
                                          "/artifacttypes/vmimage/offers?api-version=2021-07-01";

                    var result = await httpClient.GetAsync(getOffersUrl);

                    if (result.IsSuccessStatusCode)
                    {
                        string json = JsonConvert.DeserializeObject(await result.Content.ReadAsStringAsync())
                            ?.ToString();
                        offerResponses.AddRange(
                            JsonConvert.DeserializeObject<List<AzureResponse>>(json ?? string.Empty) ??
                            new List<AzureResponse>());

                        Assert.IsTrue(offerResponses is { Count: > 1 });
                    }

                    foreach (var offerResponse in offerResponses)
                    {

                        string getSkusUrl = "subscriptions/" + ConfigurationHelper.GetSubscriptionId() +
                                            "/providers/Microsoft.Compute/locations/" +
                                            ConfigurationHelper.GetLocation() +
                                            "/publishers/" + publisher.name + "/artifacttypes/vmimage/" +
                                            "offers/" + offerResponse.name + "/skus?api-version=2021-07-01";

                        var skuResult = await httpClient.GetAsync(getSkusUrl);

                        if (skuResult.IsSuccessStatusCode)
                        {
                            string skuJson = JsonConvert
                                .DeserializeObject(await skuResult.Content.ReadAsStringAsync())?.ToString();
                            skuResponses.AddRange(
                                JsonConvert.DeserializeObject<List<AzureResponse>>(skuJson ?? string.Empty) ??
                                new List<AzureResponse>());

                            Assert.IsTrue(skuResponses is { Count: > 1 });
                        }

                        foreach (var skuResponse in skuResponses)
                        {

                            string getVersionsUrl = "subscriptions/" + ConfigurationHelper.GetSubscriptionId() +
                                                    "/providers/Microsoft.Compute/locations/" +
                                                    ConfigurationHelper.GetLocation() +
                                                    "/publishers/" + publisher.name + "/artifacttypes/vmimage/" +
                                                    "offers/" + offerResponse.name +
                                                    "/skus/" + skuResponse.name +
                                                    "/versions?api-version=2021-07-01";

                            var versionResult = await httpClient.GetAsync(getVersionsUrl);

                            if (versionResult.IsSuccessStatusCode)
                            {
                                string versionJson = JsonConvert
                                    .DeserializeObject(await versionResult.Content.ReadAsStringAsync())?.ToString();
                                versionResponses.AddRange(
                                    JsonConvert.DeserializeObject<List<AzureResponse>>(versionJson ??
                                        string.Empty) ?? new List<AzureResponse>());
                            }


                            foreach (var versionResponse in versionResponses)
                            {

                                string getVmImageUrl = "subscriptions/" + ConfigurationHelper.GetSubscriptionId() +
                                                       "/providers/Microsoft.Compute/locations/" +
                                                       ConfigurationHelper.GetLocation() +
                                                       "/publishers/" + publisher.name + "/artifacttypes/vmimage/" +
                                                       "offers/" + offerResponse.name +
                                                       "/skus/" + skuResponse.name +
                                                       "/versions/" + versionResponse.name +
                                                       "?api-version=2021-07-01";

                                var vmImageResult = await httpClient.GetAsync(getVmImageUrl);

                                if (vmImageResult.IsSuccessStatusCode)
                                {
                                    string vmImageJson = JsonConvert
                                        .DeserializeObject(await vmImageResult.Content.ReadAsStringAsync())
                                        ?.ToString();
                                    var vmImageResponse =
                                        JsonConvert.DeserializeObject<AzureResponse>(vmImageJson ?? string.Empty);
                                    vmImageResponses.Add(vmImageResponse);

                                    if (vmImageResponses.Count < 20)
                                    {
                                        continue;
                                    }

                                    Assert.IsNotNull(vmImageResponses);
                                    Assert.IsTrue(vmImageResponses.Count >= 20);
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

public class AzureResponse
{
    public string name { get; set; }
}

public class AuthResponse
{
    public string access_token { get; set; }
}

public class ConfigurationHelper
{
    private IConfiguration Configuration { get; set; }

    public ConfigurationHelper()
    {
        var builder = new ConfigurationBuilder()
            .AddUserSecrets<ConfigurationHelper>();
        Configuration = builder.Build();
    }

    private string GetSetting(string userSecretVariableName)
    {
        try
        {
            return Configuration[userSecretVariableName];
        }
        catch (Exception e)
        {
            throw new Exception($"You need a configuration/user secret of {userSecretVariableName} in the integration test project", e);
        }
    }

    public string GetTenantId()
    {
        return GetSetting("tenantId");
    }

    public string GetClientSecret()
    {
        return GetSetting("clientSecret");
    }

    public string GetClientId()
    {
        return GetSetting("clientId");
    }

    public string GetSubscriptionId()
    {
        return GetSetting("subscriptionId");
    }

    public string GetResource()
    {
        return GetSetting("resource");
    }

    public string GetLocation()
    {
        return GetSetting("location");
    }
}
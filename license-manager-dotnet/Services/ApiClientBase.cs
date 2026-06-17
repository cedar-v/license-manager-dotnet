using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LicenseManager.DotNet.Configuration;
using LicenseManager.DotNet.Json;

namespace LicenseManager.DotNet.Services;

public abstract class ApiClientBase
{
    protected ApiClientBase(HttpClient httpClient, LicenseClientConfig config)
    {
        HttpClient = httpClient;
        HttpClient.BaseAddress ??= config.BuildBaseUri();
        HttpClient.Timeout = config.HttpTimeoutOrDefault();

        if (!HttpClient.DefaultRequestHeaders.Contains("Accept"))
        {
            HttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        }

        foreach (var header in config.HttpHeaders)
        {
            HttpClient.DefaultRequestHeaders.Remove(header.Key);
            HttpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    protected HttpClient HttpClient { get; }

    protected async Task<T> PostApiAsync<T>(string path, object body, string scope, CancellationToken cancellationToken)
        where T : class
    {
        HttpResponseMessage response;
        try
        {
            response = await HttpClient.PostAsJsonAsync(path, body, LicenseJson.Options, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"{scope}: network error: {ex.Message}", ex);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        ApiResponse<T>? apiResponse;
        try
        {
            apiResponse = await System.Text.Json.JsonSerializer
                .DeserializeAsync<ApiResponse<T>>(stream, LicenseJson.Options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"{scope}: decode response failed, status {(int)response.StatusCode}", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            if (!string.IsNullOrWhiteSpace(apiResponse?.Message))
            {
                throw new InvalidOperationException($"{scope}: {apiResponse.Message} ({apiResponse.Code})");
            }

            throw new InvalidOperationException($"{scope}: status {(int)response.StatusCode}");
        }

        if (!string.IsNullOrWhiteSpace(apiResponse?.Code) && apiResponse.Code != "000000")
        {
            throw new InvalidOperationException($"{scope}: {apiResponse.Message} ({apiResponse.Code})");
        }

        return apiResponse?.Data ?? throw new InvalidOperationException($"{scope}: response missing data");
    }

    private sealed class ApiResponse<T>
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = "";

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }
}

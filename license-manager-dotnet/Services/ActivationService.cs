using LicenseManager.DotNet.Configuration;
using LicenseManager.DotNet.Models;

namespace LicenseManager.DotNet.Services;

public sealed class ActivationService : ApiClientBase
{
    public ActivationService(HttpClient httpClient, LicenseClientConfig config)
        : base(httpClient, config)
    {
    }

    public Task<ActivateResponse> ActivateAsync(ActivateRequest request, CancellationToken cancellationToken = default)
    {
        return PostApiAsync<ActivateResponse>("api/v1/activate", request, "activation", cancellationToken);
    }
}

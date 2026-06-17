using LicenseManager.DotNet.Configuration;
using LicenseManager.DotNet.Models;

namespace LicenseManager.DotNet.Services;

public sealed class HeartbeatService(HttpClient httpClient, LicenseClientConfig config) : ApiClientBase(httpClient, config)
{
    public Task<HeartbeatResponse> SendAsync(HeartbeatRequest request, CancellationToken cancellationToken = default)
    {
        return PostApiAsync<HeartbeatResponse>("api/v1/heartbeat", request, "heartbeat", cancellationToken);
    }
}

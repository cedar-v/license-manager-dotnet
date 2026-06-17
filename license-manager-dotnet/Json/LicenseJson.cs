using System.Text.Json;

namespace LicenseManager.DotNet.Json;

internal static class LicenseJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
}

using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using LicenseManager.DotNet.Abstractions;

namespace LicenseManager.DotNet.Hardware;

public sealed class DefaultHardwareProvider : IHardwareProvider
{
    private readonly IReadOnlyList<string> fields;

    public DefaultHardwareProvider(IEnumerable<string>? fields = null)
    {
        var normalized = fields?
            .Select(field => field.Trim().ToLowerInvariant())
            .Where(field => field.Length > 0)
            .Distinct()
            .ToArray();

        this.fields = normalized is { Length: > 0 } ? normalized : ["mac", "hostname"];
    }

    public Task<HardwareFingerprint> GetFingerprintAsync(CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (field)
            {
                case "hostname":
                    data["hostname"] = Dns.GetHostName();
                    break;
                case "mac":
                    var mac = GetMacAddress();
                    if (!string.IsNullOrWhiteSpace(mac))
                    {
                        data["mac"] = mac;
                    }

                    break;
                case "cpu":
                    var cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
                    if (!string.IsNullOrWhiteSpace(cpu))
                    {
                        data["cpu"] = cpu;
                    }

                    break;
                case "memory":
                    var memory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                    if (memory > 0)
                    {
                        data["memory"] = FormatBytes(memory);
                    }

                    break;
            }
        }

        var builder = new StringBuilder();
        foreach (var item in data.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            builder.Append(item.Key);
            builder.Append('=');
            builder.Append(item.Value.ToLowerInvariant());
            builder.Append(';');
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
        return Task.FromResult(new HardwareFingerprint(hash, data));
    }

    private static string GetMacAddress()
    {
        return NetworkInterface
            .GetAllNetworkInterfaces()
            .Where(item => item.NetworkInterfaceType != NetworkInterfaceType.Loopback
                && item.OperationalStatus == OperationalStatus.Up)
            .Select(item => item.GetPhysicalAddress().ToString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => string.Join(':', Enumerable.Range(0, value.Length / 2).Select(i => value.Substring(i * 2, 2))).ToLowerInvariant())
            .FirstOrDefault() ?? "";
    }

    private static string FormatBytes(long bytes)
    {
        const long kb = 1024;
        const long mb = 1024 * kb;
        const long gb = 1024 * mb;

        return bytes switch
        {
            >= gb => $"{bytes / gb}GB",
            >= mb => $"{bytes / mb}MB",
            >= kb => $"{bytes / kb}KB",
            _ => $"{bytes}B"
        };
    }
}

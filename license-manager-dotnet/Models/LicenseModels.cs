using System.Text.Json;
using System.Text.Json.Serialization;

namespace LicenseManager.DotNet.Models;

public sealed class LicensePayload
{
    [JsonPropertyName("license_key")]
    public string LicenseKey { get; set; } = "";

    [JsonPropertyName("authorization_code")]
    public string AuthorizationCode { get; set; } = "";

    [JsonPropertyName("hardware_fingerprint")]
    public string HardwareFingerprint { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("start_date")]
    public DateTimeOffset? StartDate { get; set; }

    [JsonPropertyName("end_date")]
    public DateTimeOffset? EndDate { get; set; }

    [JsonIgnore]
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonPropertyName("product")]
    public string? Product { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("authorization_code_id")]
    public string? AuthorizationCodeId { get; set; }

    [JsonPropertyName("deployment_type")]
    public string? DeploymentType { get; set; }

    [JsonPropertyName("max_activations")]
    public int? MaxActivations { get; set; }

    [JsonPropertyName("custom_parameters")]
    public Dictionary<string, JsonElement> CustomParameters { get; set; } = [];

    [JsonPropertyName("feature_config")]
    public Dictionary<string, JsonElement> FeatureConfig { get; set; } = [];

    [JsonPropertyName("usage_limits")]
    public Dictionary<string, JsonElement> UsageLimits { get; set; } = [];

    [JsonIgnore]
    public Dictionary<string, JsonElement> Extras { get; set; } = [];

    [JsonPropertyName("activated_at")]
    public DateTimeOffset? ActivatedAt { get; set; }

    [JsonPropertyName("generated_at")]
    public DateTimeOffset? GeneratedAt { get; set; }

    [JsonPropertyName("config_updated_at")]
    public DateTimeOffset? ConfigUpdatedAt { get; set; }
}

public sealed class LicenseEnvelope
{
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "";

    [JsonPropertyName("data")]
    public string Data { get; set; } = "";

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = "";
}

public sealed class ActivateRequest
{
    [JsonPropertyName("authorization_code")]
    public string AuthorizationCode { get; set; } = "";

    [JsonPropertyName("product")]
    public string Product { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("hardware_fingerprint")]
    public string HardwareFingerprint { get; set; } = "";

    [JsonPropertyName("software_version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SoftwareVersion { get; set; }

    [JsonPropertyName("device_info")]
    public Dictionary<string, object?> DeviceInfo { get; set; } = [];

    [JsonPropertyName("metadata")]
    public Dictionary<string, object?> Metadata { get; set; } = [];
}

public sealed class ActivateResponse
{
    [JsonPropertyName("license_key")]
    public string LicenseKey { get; set; } = "";

    [JsonPropertyName("license_file")]
    public string LicenseFile { get; set; } = "";

    [JsonPropertyName("heartbeat_interval")]
    public int HeartbeatInterval { get; set; }

    [JsonPropertyName("payload")]
    public LicensePayload? Payload { get; set; }

    [JsonPropertyName("public_key")]
    public string? PublicKey { get; set; }
}

public sealed class HeartbeatRequest
{
    [JsonPropertyName("license_key")]
    public string LicenseKey { get; set; } = "";

    [JsonPropertyName("hardware_fingerprint")]
    public string HardwareFingerprint { get; set; } = "";

    [JsonPropertyName("software_version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SoftwareVersion { get; set; }

    [JsonPropertyName("usage_data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Dictionary<string, object?> UsageData { get; set; } = [];

    [JsonPropertyName("config_updated_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? ConfigUpdatedAt { get; set; }
}

public sealed class HeartbeatResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("license_file")]
    public string? LicenseFile { get; set; }

    [JsonPropertyName("heartbeat_interval")]
    public int? HeartbeatInterval { get; set; }

    [JsonPropertyName("config_updated")]
    public bool? ConfigUpdated { get; set; }

    [JsonPropertyName("payload")]
    public LicensePayload? Payload { get; set; }
}

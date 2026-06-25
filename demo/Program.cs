using LicenseManager.DotNet;
using LicenseManager.DotNet.Configuration;
using LicenseManager.DotNet.Models;

static string Env(string name, string fallback)
{
    return Environment.GetEnvironmentVariable(name) ?? fallback;
}

// 1. SDK 基础配置
// 对齐 C++ examples/basic.cpp：首次在线激活不需要提前准备公钥。
// 激活接口会直接返回 public_key 和 license_file，SDK 会把它们缓存到本地。
var config = new LicenseClientConfig
{
    // TODO: 对接时改成你的授权服务地址、产品标识和软件版本。
    Server = Env("LICENSE_SERVER", "http://lm-e.cedar-v.com"),
    Product = Env("LICENSE_PRODUCT", "my-product"),
    Version = Env("LICENSE_VERSION", "1.0.0"),

    // 本地许可证保存位置。首次激活成功后，SDK 会保存 license.lic。
    LicenseFilePath = Env("LICENSE_FILE_PATH", Path.Combine("license_code", "license.lic")),

    // 可选：如果你想从文件读取激活码，就设置 LICENSE_AUTH_CODE_PATH。
    // 默认不要求提前放授权码文件；本地没有有效许可证时，会在控制台提示输入激活码。
    AuthorizationCode = Env("LICENSE_AUTH_CODE", ""),
    AuthorizationCodePath = Env("LICENSE_AUTH_CODE_PATH", ""),

    // 首次在线激活不要配置 PublicKeyPath。
    // 只有离线校验或固定公钥场景，才需要设置 PublicKeyPath/PublicKeyPem。

    // 硬件指纹字段用于把许可证绑定到当前设备。
    // 字段越多绑定越严格，但硬件变化后越可能需要重新激活。
    HardwareFields = new List<string> { "mac", "hostname", "cpu" },

    // demo 默认 10 秒心跳，方便观察；生产环境按后台策略配置。
    HeartbeatIntervalSeconds = int.TryParse(Environment.GetEnvironmentVariable("LICENSE_HEARTBEAT_SECONDS"), out var heartbeatSeconds)
        ? heartbeatSeconds
        : 10,

    // true 表示只校验本地已有 license，不请求服务端激活。
    Offline = bool.TryParse(Environment.GetEnvironmentVariable("LICENSE_OFFLINE"), out var offline) && offline
};

// 2. 注册 SDK 回调
// 业务系统可以在这些回调里更新界面、记录日志、停用核心功能或提示重新激活。
var callbacks = new LicenseCallbacks
{
    OnLicenseUpdated = license =>
    {
        Log($"许可证已更新，过期时间：{license.ExpiresAt:u}");
        return Task.CompletedTask;
    },
    OnHeartbeatError = error =>
    {
        Log($"心跳异常：{error.Message}");
        return Task.CompletedTask;
    },
    OnActivationRequired = reason =>
    {
        Log($"需要重新激活：{reason}");
        return Task.CompletedTask;
    },
    OnHeartbeatPing = () =>
    {
        Log("心跳成功");
        return Task.CompletedTask;
    }
};

try
{
    using var client = await CreateClientAsync(config, callbacks);

    // 3. 授权校验通过后，读取当前许可证内容。
    var current = client.CurrentLicense();
    if (current is null)
    {
        Log("没有读取到许可证信息。");
        return 1;
    }

    client.Validate();
    PrintLicense(current);

    // 4. 从这里开始启动你的真实业务逻辑。
    // 可以根据 FeatureConfig / UsageLimits / CustomParameters 控制模块权限、额度和功能开关。
    Log("主线程正在模拟业务运行，按 Ctrl+C 退出。");
    await WaitForExitAsync();
    Log("正在关闭 SDK 客户端。");
    return 0;
}
catch (Exception ex)
{
    Log($"授权校验/激活失败：{ex.Message}");
    return 1;
}

static async Task<LicenseClient> CreateClientAsync(LicenseClientConfig config, LicenseCallbacks callbacks)
{
    try
    {
        // CreateAsync 会先尝试加载并校验本地 license。
        // 如果本地没有有效 license，且已有激活码，则会发起在线激活。
        return await LicenseClient.CreateAsync(config, new LicenseClientOptions
        {
            Callbacks = callbacks
        });
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("activation requires authorization code", StringComparison.OrdinalIgnoreCase))
    {
        // 本地没有有效许可证，并且还没有激活码：参考 C++ demo，在控制台提示用户输入。
        Console.Write("未找到有效许可证，请输入激活码：");
        var code = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("未输入激活码，无法在线激活。");
        }

        config.AuthorizationCode = code;
        return await LicenseClient.CreateAsync(config, new LicenseClientOptions
        {
            Callbacks = callbacks
        });
    }
}

static async Task WaitForExitAsync()
{
    var exit = new TaskCompletionSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        exit.TrySetResult();
    };

    var runSeconds = int.TryParse(Environment.GetEnvironmentVariable("LICENSE_DEMO_RUN_SECONDS"), out var seconds)
        ? seconds
        : 35;

    await Task.WhenAny(exit.Task, Task.Delay(TimeSpan.FromSeconds(runSeconds)));
}

static void PrintLicense(LicensePayload license)
{
    Console.WriteLine();
    Console.WriteLine("========== 当前许可证 ==========");
    Console.WriteLine($"产品:         {license.Product}");
    Console.WriteLine($"版本:         {license.Version}");
    Console.WriteLine($"许可证Key:    {license.LicenseKey}");
    Console.WriteLine($"状态:         {license.Status}");
    Console.WriteLine($"部署类型:     {license.DeploymentType}");
    Console.WriteLine($"最大激活数:   {license.MaxActivations?.ToString() ?? "-"}");
    Console.WriteLine($"开始时间:     {license.StartDate:u}");
    Console.WriteLine($"结束时间:     {license.EndDate:u}");
    Console.WriteLine($"过期时间:     {license.ExpiresAt:u}");
    Console.WriteLine("==============================");
    Console.WriteLine();
}

static void Log(string message)
{
    Console.WriteLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {message}");
}

# License Manager .NET 示例

本示例对应 Python 版本中 `examples/basic/main.py` 的流程：

1. 尝试校验本地缓存的 `license_code/license.lic`。
2. 如果本地没有有效的许可证，则提示输入激活码。
3. 调用在线激活接口，接口同时返回 `public_key` 和 `license_file`。
4. 将公钥和许可证缓存到本地，然后打印当前许可证信息。

在仓库根目录下运行：

```powershell
dotnet run --project .\demo\demo.csproj
```

可选的环境变量覆盖：

```powershell
$env:LICENSE_SERVER="https://license.example.com"
$env:LICENSE_PRODUCT="edge-gateway"
$env:LICENSE_VERSION="2.3.1"
$env:LICENSE_AUTH_CODE="your-activation-code"
# 也可以使用文件：
# $env:LICENSE_AUTH_CODE_PATH="license_code\authorization_code.txt"
$env:LICENSE_FILE_PATH="license_code\license.lic"
$env:LICENSE_HEARTBEAT_SECONDS="10"
$env:LICENSE_DEMO_RUN_SECONDS="35"
```

## 支持的 .NET 版本

- .NET 6.0 及以上版本（`net6.0`）

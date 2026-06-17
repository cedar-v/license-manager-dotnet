# License Manager .NET Demo

This demo mirrors the Python `examples/basic/main.py` flow:

1. Try to validate the cached local `license_code/license.lic`.
2. If no valid local license exists, ask for an activation code.
3. Activate online. The activation API returns both `public_key` and `license_file`.
4. Cache the public key and license locally, then print the current license.

Run from the repository root:

```powershell
dotnet run --project .\demo\demo.csproj
```

Optional environment overrides:

```powershell
$env:LICENSE_SERVER="https://license.example.com"
$env:LICENSE_PRODUCT="edge-gateway"
$env:LICENSE_VERSION="2.3.1"
$env:LICENSE_AUTH_CODE="your-activation-code"
# Or use a file:
# $env:LICENSE_AUTH_CODE_PATH="license_code\authorization_code.txt"
$env:LICENSE_FILE_PATH="license_code\license.lic"
$env:LICENSE_HEARTBEAT_SECONDS="10"
$env:LICENSE_DEMO_RUN_SECONDS="35"
```

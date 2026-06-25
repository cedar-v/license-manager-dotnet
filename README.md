# license-manager-dotnet

License Manager 的 .NET SDK，提供在线激活、本地许可证校验以及心跳同步能力。

## 支持的 .NET 版本

- .NET 6.0 及以上版本（`net6.0`）

## 项目结构

- `license-manager-dotnet`：核心 SDK 库。
- `demo`：控制台示例，用于演示 SDK 的完整接入流程。

## 构建

```powershell
dotnet build .\license-manager-dotnet.sln
```

## 打包

```powershell
dotnet pack .\license-manager-dotnet\license-manager-dotnet.csproj -c Release
```

# 工具箱

## v1.0.4

- 新增小型更新引导程序，旧版只需先下载一个很小的 EXE，避免 69MB 完整安装包被旧版 15 秒超时中断。
- 引导程序显示完整安装包的百分比、文件大小、速度、已用时间和预计剩余时间。
- 完整安装包下载完成后自动安装，并重新打开工具箱。
- Release 中 `WorkbenchSetup.exe` 为更新引导程序，`WorkbenchFullSetup.exe` 为完整离线安装包。

## v1.0.3

- 修复网络稍慢时检查更新在 15 秒后失败的问题，增加超时放宽、自动重试及备用检查地址。
- 新增更新进度窗口，显示下载百分比、文件大小、速度、已用时间和预计剩余时间。
- 下载完整安装包后自动关闭旧版、静默覆盖安装，并自动重新打开工具箱。

## v1.0.2

- 倒计时与鼠标高亮在左侧导航栏中互换位置。
- 悬浮倒计时默认使用完全透明背景。
- 归零后继续以红色负数计时，例如 `-00:01`。
- 支持自行配置倒计时背景颜色和不透明度。

面向 Windows 10/11 的轻量桌面效率工具，将鼠标高亮、演示标注、悬浮倒计时和桌面文档收纳整合在同一个应用中。

## 功能

### 鼠标高亮

- `F1` 全局开启/关闭高亮，系统指针保持不变
- 红环、方框、十字准星，自定义颜色、大小和相对位置
- 聚光与霓虹主题，可选动态呼吸效果
- 事件驱动跟随，点击穿透，支持多显示器与 Per-Monitor V2 DPI
- `F2` 开启/关闭全屏自由标注
- 可选显示键盘按键与组合键

### 倒计时

- 设置分钟与秒数
- 独立无边框悬浮窗口，始终置顶
- 可拖动、缩放、开始/暂停、重置
- 倒计时结束变红提示

### 桌面收纳

- 执行前预览待整理文件
- 将 Word、Excel、PowerPoint、PDF、TXT、CSV、WPS 等办公文档移动到桌面的当天日期文件夹
- 自动避免重名覆盖
- 跳过程序、快捷方式、文件夹、隐藏与系统文件
- 支持撤回上一次收纳

### 安装与更新

- `WorkbenchSetup.exe` 是约 76KB 的在线更新引导程序，适合自动更新和在线安装
- `WorkbenchFullSetup.exe` 是包含完整运行环境的离线安装包，无需管理员权限
- 自动创建桌面及开始菜单快捷方式
- 可从 Windows“已安装的应用”中卸载
- 启动时通过 GitHub Releases 检查新版本
- 用户确认后显示下载进度、文件大小、速度和预计剩余时间
- 下载完整安装包后自动覆盖升级并重新打开应用

## 构建

要求：Windows 10/11、.NET 6 SDK。

```powershell
dotnet publish -c Release
```

发布文件位于：

```text
bin\Release\net6.0-windows\win-x64\publish\Workbench.exe
```

完整安装器源码位于 `Installer/Installer.cs`，更新引导程序源码位于 `Installer/Bootstrapper.cs`。两者均使用 Windows 自带的 .NET Framework C# 编译器构建；完整安装器会将发布后的 `Workbench.exe` 嵌入为资源。

## 自动更新发布流程

1. 修改 `Workbench.csproj` 中的 `Version`。
2. 重新构建应用与安装器。
3. 在 GitHub 创建对应版本标签，例如 `v1.1.0`。
4. 将小型更新引导程序以固定文件名 `WorkbenchSetup.exe` 上传至 Release。
5. 将完整离线安装包以固定文件名 `WorkbenchFullSetup.exe` 上传至同一个 Release。

程序读取 `li85120194-netizen/Workbench` 的最新 Release，并比较语义版本号。

## 设计参考

实现思路参考了 Microsoft PowerToys Mouse Highlighter、DesktopFences/DesktopBox 的桌面层设计以及 Updatum 的 GitHub Releases 更新流程；本项目代码为独立实现。

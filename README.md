# 工作台

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

- `WorkbenchSetup.exe` 安装到当前用户目录，无需管理员权限
- 自动创建桌面及开始菜单快捷方式
- 可从 Windows“已安装的应用”中卸载
- 启动时通过 GitHub Releases 检查新版本
- 用户确认后下载新版安装包并自动覆盖升级

## 构建

要求：Windows 10/11、.NET 6 SDK。

```powershell
dotnet publish -c Release
```

发布文件位于：

```text
bin\Release\net6.0-windows\win-x64\publish\Workbench.exe
```

安装器源码位于 `Installer/Installer.cs`。安装器使用 Windows 自带的 .NET Framework C# 编译器构建，并将发布后的 `Workbench.exe` 嵌入为资源。

## 自动更新发布流程

1. 修改 `Workbench.csproj` 中的 `Version`。
2. 重新构建应用与安装器。
3. 在 GitHub 创建对应版本标签，例如 `v1.1.0`。
4. 将安装包以固定文件名 `WorkbenchSetup.exe` 上传至 Release。

程序读取 `li85120194-netizen/Workbench` 的最新 Release，并比较语义版本号。

## 设计参考

实现思路参考了 Microsoft PowerToys Mouse Highlighter、DesktopFences/DesktopBox 的桌面层设计以及 Updatum 的 GitHub Releases 更新流程；本项目代码为独立实现。

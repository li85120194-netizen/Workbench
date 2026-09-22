# 工具箱

工具箱是一款面向 Windows 10/11 的轻量桌面效率应用。它把演示辅助、计时、便签、文件整理、图片处理和 PDF 工具整合在同一个原生 WPF 应用中，安装后可通过桌面快捷方式或系统托盘使用。

## v1.1.0

### 新增功能

- 剪贴板历史：监听文本剪贴板，保留最近 100 条记录，可预览、重新复制、单条删除或全部清空。
- 系统托盘：最小化时隐藏到托盘；托盘菜单可打开工具箱、切换鼠标高亮、切换屏幕标注或退出。
- 番茄钟：支持自定义专注、短休息、长休息和轮次数；自动循环并通过托盘提醒。
- 桌面便签 / 待办：本地保存标题、正文和完成状态；可将任意便签显示为可拖动、可缩放、始终置顶的独立窗口。
- 文件批量重命名：支持查找替换、正则表达式、前后缀和自动编号；执行前预览并检测重名冲突。
- 图片压缩与格式转换：批量输出 JPEG、PNG、BMP、TIFF、GIF，可调 JPEG 质量和最长边尺寸，不覆盖原图。
- PDF 工具：本地合并、逐页拆分、按页码范围提取、整本旋转，不上传文件。

### 原有功能

- 鼠标高亮：`F1` 全局开启 / 关闭，支持圆环、方框、十字准星、颜色、大小、相对位置和动态主题。
- 屏幕标注：`F2` 全局开启 / 关闭。
- 键盘按键显示。
- 悬浮倒计时：透明背景、可拖动缩放；归零后继续显示红色负计时。
- 桌面收纳：预览、按日期整理办公文档、撤回上一次整理。
- 单实例运行、Per-Monitor V2 DPI、多显示器和 GitHub Releases 自动更新。

## 数据与隐私

剪贴板历史、便签和设置只写入：

```text
%LOCALAPPDATA%\Toolbox\state.json
```

图片和 PDF 处理完全在本机完成。应用不会上传这些内容。剪贴板历史只记录文本，单条超过 200,000 字符时跳过，最多保留 100 条。

## 构建

要求：Windows 10/11、.NET 6 SDK。

```powershell
dotnet restore
dotnet build -c Release
dotnet publish -c Release
```

单文件程序位于：

```text
bin\Release\net6.0-windows\win-x64\publish\Workbench.exe
```

构建完整离线安装包和小型在线更新引导程序：

```powershell
powershell -ExecutionPolicy Bypass -File .\build-installer.ps1
```

脚本会生成：

- `WorkbenchFullSetup.exe`：包含自带 .NET 运行时的完整安装包。
- `WorkbenchSetup.exe`：小型在线引导程序，分段下载完整安装包并校验 SHA-256。

## 自动更新发布流程

1. 修改 `Workbench.csproj`、`Installer/Installer.cs`、`Installer/Bootstrapper.cs` 中的版本号。
2. 运行 `build-installer.ps1`。
3. 创建同名 Git 标签，例如 `v1.1.0`。
4. 将 `WorkbenchSetup.exe` 和 `WorkbenchFullSetup.exe` 上传到该 GitHub Release。
5. 发布 Release。应用启动时会读取最新 Release 并比较语义版本号。

## 设计参考与第三方组件

- 文件重命名和图片批处理的交互参考 Microsoft PowerToys 的 PowerRename 与 Image Resizer；实现代码为本项目独立编写。
- 番茄钟的阶段循环参考 Pomotroid；实现代码为本项目独立编写。
- 桌面便签的独立窗口交互参考 PaperTodo；实现代码为本项目独立编写。
- PDF 功能使用 `PDFsharp 6.2.4`，MIT License。完整声明见 `THIRD-PARTY-NOTICES.md`。

未复制或合并 GPL / AGPL 项目的源代码。

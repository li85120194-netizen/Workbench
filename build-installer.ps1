$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $projectRoot
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

dotnet publish "$projectRoot\Workbench.csproj" -c Release
$payload = "$projectRoot\bin\Release\net6.0-windows\win-x64\publish\Workbench.exe"

& $csc /nologo /target:winexe /platform:x64 /optimize+ `
  /win32icon:"$projectRoot\Assets\Workbench.ico" `
  /resource:"$payload,WorkbenchPayload" `
  /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:Microsoft.CSharp.dll `
  /out:"$repoRoot\WorkbenchSetup.exe" "$projectRoot\Installer\Installer.cs"

Write-Host "安装包已生成：$repoRoot\WorkbenchSetup.exe"

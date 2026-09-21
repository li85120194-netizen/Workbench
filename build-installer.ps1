$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $projectRoot
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

dotnet publish "$projectRoot\Workbench.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
$payload = "$projectRoot\bin\Release\net6.0-windows\win-x64\publish\Workbench.exe"

& $csc /nologo /target:winexe /platform:x64 /optimize+ `
  /win32icon:"$projectRoot\Assets\Workbench.ico" `
  /resource:"$payload,WorkbenchPayload" `
  /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:Microsoft.CSharp.dll `
  /out:"$repoRoot\WorkbenchFullSetup.exe" "$projectRoot\Installer\Installer.cs"
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }

& $csc /nologo /target:winexe /platform:x64 /optimize+ `
  /win32icon:"$projectRoot\Assets\Workbench.ico" `
  /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
  /out:"$repoRoot\WorkbenchSetup.exe" "$projectRoot\Installer\Bootstrapper.cs"
if ($LASTEXITCODE -ne 0) { throw 'Bootstrapper compilation failed.' }

Write-Host "Full installer created: $repoRoot\WorkbenchFullSetup.exe"
Write-Host "Update bootstrapper created: $repoRoot\WorkbenchSetup.exe"

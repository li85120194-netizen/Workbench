$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $projectRoot
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$projectXml = [xml](Get-Content -LiteralPath "$projectRoot\Workbench.csproj")
$version = [string]$projectXml.Project.PropertyGroup.Version

dotnet publish "$projectRoot\Workbench.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
$payload = "$projectRoot\bin\Release\net6.0-windows\win-x64\publish\Workbench.exe"

& $csc /nologo /target:winexe /platform:x64 /optimize+ `
  /win32icon:"$projectRoot\Assets\Workbench.ico" `
  /resource:"$payload,WorkbenchPayload" `
  /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:Microsoft.CSharp.dll `
  /out:"$repoRoot\WorkbenchFullSetup.exe" "$projectRoot\Installer\Installer.cs"
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }

$fullInstaller = "$repoRoot\WorkbenchFullSetup.exe"
$fullSize = (Get-Item -LiteralPath $fullInstaller).Length
$fullHash = (Get-FileHash -LiteralPath $fullInstaller -Algorithm SHA256).Hash
$segmentCount = 4
$segmentSize = [long][Math]::Floor($fullSize / $segmentCount)
$inputStream = [IO.File]::OpenRead($fullInstaller)
try {
  for ($segment = 0; $segment -lt $segmentCount; $segment++) {
    $partPath = "$fullInstaller.part$segment"
    $bytesToWrite = if ($segment -eq $segmentCount - 1) { $fullSize - ($segmentSize * $segment) } else { $segmentSize }
    $outputStream = [IO.File]::Create($partPath)
    try {
      $buffer = New-Object byte[] (1024 * 1024)
      while ($bytesToWrite -gt 0) {
        $read = $inputStream.Read($buffer, 0, [int][Math]::Min($buffer.Length, $bytesToWrite))
        if ($read -le 0) { throw "Unexpected end of full installer while creating segment $segment." }
        $outputStream.Write($buffer, 0, $read)
        $bytesToWrite -= $read
      }
    }
    finally { $outputStream.Dispose() }
  }
}
finally { $inputStream.Dispose() }
$bootstrapInfo = [IO.Path]::Combine([IO.Path]::GetTempPath(), 'WorkbenchBootstrap-' + [Guid]::NewGuid().ToString('N') + '.txt')
[IO.File]::WriteAllText($bootstrapInfo, "$fullSize|$fullHash|$version")
try {
  & $csc /nologo /target:winexe /platform:x64 /optimize+ `
    /win32icon:"$projectRoot\Assets\Workbench.ico" `
    /resource:"$bootstrapInfo,WorkbenchPayloadInfo" `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
    /out:"$repoRoot\WorkbenchSetup.exe" "$projectRoot\Installer\Bootstrapper.cs"
  if ($LASTEXITCODE -ne 0) { throw 'Bootstrapper compilation failed.' }
}
finally {
  Remove-Item -LiteralPath $bootstrapInfo -Force -ErrorAction SilentlyContinue
}

Write-Host "Full installer created: $repoRoot\WorkbenchFullSetup.exe"
Write-Host "Full installer segments created: $repoRoot\WorkbenchFullSetup.exe.part0..3"
Write-Host "Update bootstrapper created: $repoRoot\WorkbenchSetup.exe"

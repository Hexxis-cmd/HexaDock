param([string]$CertificateThumbprint = "")

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\HexaDock.csproj"
$release = Join-Path $PSScriptRoot "..\..\Release\HexaDock-win-x64"
$installer = Join-Path $PSScriptRoot "HexaDock.iss"
$signTool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"
$compiler = if ($env:ISCC) { $env:ISCC } else {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $command.Source } else { Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe" }
}

if (-not (Test-Path -LiteralPath $compiler)) { throw "Inno Setup compiler not found. Set the ISCC environment variable to ISCC.exe." }

dotnet publish $project -c Release -r win-x64 --self-contained true --source https://api.nuget.org/v3/index.json -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($CertificateThumbprint) {
    & $signTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 (Join-Path $release "HexaDock.exe")
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

& $compiler $installer
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($CertificateThumbprint) {
    & $signTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 (Join-Path $PSScriptRoot "..\..\Release\HexaDock-1.0.0-Setup.exe")
}

param(
    [Parameter(Mandatory)] [string] $Dotnet,
    [Parameter(Mandatory)] [string] $Iscc,
    [string] $Version,
    [switch] $SkipDownloadsCopy
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$Dotnet = (Resolve-Path -LiteralPath $Dotnet).Path
$Iscc = (Resolve-Path -LiteralPath $Iscc).Path
if (!$Version) {
    $Version = ([xml](Get-Content -Raw (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Expected a numeric product version.' }

$work = Join-Path $root "artifacts\installer-$Version"
$build = Join-Path $work ([Guid]::NewGuid().ToString('N'))
$payload = Join-Path $build 'payload'
$rescue = Join-Path $build 'rescue'
$output = Join-Path $build 'output'
foreach ($project in @('App', 'Rescue')) {
    $destination = if ($project -eq 'App') { $payload } else { $rescue }
    & $Dotnet publish (Join-Path $root "src\CouchPad.$project\CouchPad.$project.csproj") -c Release -r win-x64 --self-contained true -o $destination --artifacts-path (Join-Path $build 'build') "-p:Version=$Version" -p:CouchPadInstallerPublish=true
    if ($LASTEXITCODE) { throw "Publish failed for $project." }
}

foreach ($suffix in @('exe','dll','deps.json','runtimeconfig.json')) {
    Copy-Item -LiteralPath (Join-Path $rescue "CouchPad.Rescue.$suffix") -Destination $payload
}
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $payload
foreach ($file in @('CouchPad.exe','coreclr.dll','hostfxr.dll','Localization\Strings.en.json','Localization\Strings.pt-BR.json')) {
    if (!(Test-Path -LiteralPath (Join-Path $payload $file) -PathType Leaf)) { throw "Missing payload file: $file" }
}
if (Get-ChildItem -LiteralPath $payload -Recurse | Where-Object Name -match 'CouchPad\.Notifications|^notifications$') {
    throw 'Experimental notifications were found in the payload.'
}
$preflight = Join-Path $build 'preflight'
& $Dotnet publish (Join-Path $root 'src\CouchPad.Rescue\CouchPad.Rescue.csproj') -c Release -r win-x64 --self-contained true -o $preflight --artifacts-path (Join-Path $build 'build') "-p:Version=$Version" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
if ($LASTEXITCODE) { throw 'Publish failed for installer preflight.' }
Rename-Item -LiteralPath (Join-Path $preflight 'CouchPad.Rescue.exe') -NewName 'CouchPad.InstallCheck.exe'
& $Iscc "/DPayload=$payload" "/DOutput=$output" "/DVersion=$Version" "/DPreflight=$(Join-Path $preflight 'CouchPad.InstallCheck.exe')" (Join-Path $PSScriptRoot 'CouchPad.iss')
if ($LASTEXITCODE) { throw 'Inno Setup compilation failed.' }
$setup = Join-Path $output "CouchPad-Setup-$Version-win-x64.exe"
Get-FileHash -LiteralPath $setup -Algorithm SHA256
if (!$SkipDownloadsCopy) {
    & (Join-Path $PSScriptRoot 'Copy-InstallerToDownloads.ps1') -Installer $setup
}

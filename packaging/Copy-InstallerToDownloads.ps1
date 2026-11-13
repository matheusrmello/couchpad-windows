param(
    [Parameter(Mandatory)] [string] $Installer
)

$ErrorActionPreference = 'Stop'
$source = Get-Item -LiteralPath $Installer
$folder = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders' -Name '{374DE290-123F-4565-9164-39C4925E467B}' -ErrorAction SilentlyContinue
$configured = $folder.'{374DE290-123F-4565-9164-39C4925E467B}'
$downloads = if ($configured) {
    [Environment]::ExpandEnvironmentVariables($configured)
} else {
    Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads'
}
[void][IO.Directory]::CreateDirectory($downloads)
$hash = (Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash
for ($index = 0; ; $index++) {
    $name = if ($index -eq 0) { $source.Name } else { "$($source.BaseName) ($index)$($source.Extension)" }
    $destination = Join-Path $downloads $name
    if (!(Test-Path -LiteralPath $destination)) {
        [IO.File]::Copy($source.FullName, $destination, $false)
    }
    $copied = Get-FileHash -LiteralPath $destination -Algorithm SHA256
    if ($copied.Hash -eq $hash) {
        $copied
        break
    }
}

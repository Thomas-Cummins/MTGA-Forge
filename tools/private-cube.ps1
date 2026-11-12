# Both players need identical cube files and the same FriendCode.
param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$FriendCode,
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$CubeFile = 'PrewarIconic',
    [ValidatePattern('^[^"\r\n]+$')][string]$CubeTitle = 'Prewar Iconic',
    [string]$Folder = '',
    [string]$Name = ''
)
$ErrorActionPreference = 'Stop'
if ($FriendCode.Length -gt 32) { throw 'The private code must be at most 32 characters.' }
$root = Split-Path $PSScriptRoot -Parent
foreach ($part in @("server\forge-gui\res\draft\$CubeFile.draft", "server\forge-gui\res\cube\$CubeFile.dck")) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $part))) { throw "Missing custom cube file: $part. Extract the complete custom package first." }
}
$savedOptions = $env:JAVA_TOOL_OPTIONS
$savedTitle = $env:MTGA_FORGE_CUSTOM_CUBE_TITLE
$savedDescription = $env:MTGA_FORGE_CUSTOM_CUBE_DESCRIPTION
try {
    $env:JAVA_TOOL_OPTIONS = ($savedOptions + " `"-Dmtga.premier.cube=$CubeFile`" `"-Dmtga.premier.cubeTitle=$CubeTitle`"").Trim()
    $env:MTGA_FORGE_CUSTOM_CUBE_TITLE = $CubeTitle
    $env:MTGA_FORGE_CUSTOM_CUBE_DESCRIPTION = 'Custom singleton cube. Join with your friend using the same private code; bots fill the remaining seats. '
    Write-Host "Starting $CubeTitle. Both players must join the same Premier Draft event before the table forms."
    $launch = @{ FriendCode = $FriendCode; NoPause = $true }
    if ($Folder) { $launch.Folder = $Folder }
    if ($Name) { $launch.Name = $Name }
    & "$PSScriptRoot\launcher.ps1" @launch
} finally {
    $env:JAVA_TOOL_OPTIONS = $savedOptions
    $env:MTGA_FORGE_CUSTOM_CUBE_TITLE = $savedTitle
    $env:MTGA_FORGE_CUSTOM_CUBE_DESCRIPTION = $savedDescription
}

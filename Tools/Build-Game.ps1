[CmdletBinding()]
param(
    [string]$UnityPath,
    [switch]$InspectOnly
)

# Windows PowerShell 5.1 compatible. Unity starts only when the user runs this script.
$ErrorActionPreference = 'Stop'
$taskProject = Split-Path -Parent $PSScriptRoot
$taskBuild = Join-Path $taskProject 'Builds\Windows'
$taskExe = Join-Path $taskBuild 'WarOfTheWorlds.exe'
$taskLog = Join-Path $taskProject 'Builds\windows-build.log'
$taskReceipt = Join-Path $taskBuild 'build-success.json'
$taskVersionText = [IO.File]::ReadAllText((Join-Path $taskProject 'ProjectSettings\ProjectVersion.txt'))
if ($taskVersionText -notmatch '(?m)^m_EditorVersion:\s*([^\r\n]+)') { throw 'Unity version setting is missing.' }
$taskVersion = $Matches[1].Trim()
if (-not $UnityPath) {
    $UnityPath = Join-Path ${env:ProgramFiles} ('Unity\Hub\Editor\' + $taskVersion + '\Editor\Unity.exe')
}
if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw ('Unity ' + $taskVersion + ' was not found. Install this version in Unity Hub, or supply -UnityPath with your Unity.exe path.')
}
$UnityPath = [IO.Path]::GetFullPath($UnityPath)
$taskModule = Join-Path (Split-Path -Parent $UnityPath) 'Data\PlaybackEngines\windowsstandalonesupport'
if (-not (Test-Path -LiteralPath $taskModule)) {
    throw 'Windows Build Support is missing. Add the Windows module for this Unity version in Unity Hub.'
}
$taskRequest = [Guid]::NewGuid().ToString('N')
$taskArguments = '-batchmode -projectPath "' + $taskProject + '" -buildTarget Win64 -executeMethod Integration.Editor.WindowsPlayerBuild.BuildExistingScenes -campaignBuildRequest ' + $taskRequest + ' -logFile "' + $taskLog + '"'
if ($InspectOnly) {
    Write-Output ('Project: ' + $taskProject)
    Write-Output ('Unity: ' + $UnityPath)
    Write-Output ('Arguments: ' + $taskArguments)
    Write-Output ('Output: ' + $taskExe)
    Write-Output 'Inspection only: Unity and the game were not started.'
    return
}

New-Item -ItemType Directory -Path $taskBuild -Force | Out-Null
Write-Host 'Building the Windows game from the existing team scenes.'
Write-Host 'The first build can take several minutes. Keep this window open.'
Write-Host 'Close this copy of the project in Unity before continuing.'
Write-Host ('Build log: ' + $taskLog)

# Start-Process flattens ArgumentList: taskArguments includes explicit native quotes.
$taskProcess = Start-Process -FilePath $UnityPath -ArgumentList $taskArguments -WorkingDirectory $taskProject -WindowStyle Hidden -Wait -PassThru
if ($taskProcess.ExitCode -ne 0) {
    throw ('Unity build failed (exit ' + $taskProcess.ExitCode + '). Read ' + $taskLog + '. Check licensing, project lock and Console errors.')
}
if (-not (Test-Path -LiteralPath $taskReceipt) -or -not (Test-Path -LiteralPath $taskExe)) {
    throw ('No successful build receipt was produced. Read ' + $taskLog)
}
$taskSaved = [IO.File]::ReadAllText($taskReceipt) | ConvertFrom-Json
if ($taskSaved.result -ne 'Succeeded' -or $taskSaved.request -ne $taskRequest -or
    [IO.Path]::GetFullPath($taskSaved.output) -ne [IO.Path]::GetFullPath($taskExe)) {
    throw 'The build receipt does not match this build request. A stale game was not launched.'
}
Write-Host 'Build succeeded. Starting WarOfTheWorlds.'
Start-Process -FilePath $taskExe -WorkingDirectory $taskBuild

param(
    [string]$IsolatedProject = 'D:\MiniProject\BuildTemp\LoopSort-3D-20261101',
    [string]$Unity = 'C:\Program Files\Unity 2022.3.62f2\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$sourceProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$targetProject = [IO.Path]::GetFullPath($IsolatedProject)
$buildRoot = [IO.Path]::GetFullPath('D:\MiniProject\BuildTemp')
if (-not $targetProject.StartsWith($buildRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $targetProject -eq $sourceProject) { throw 'The isolated project must be below D:\MiniProject\BuildTemp.' }
if (-not (Test-Path -LiteralPath $Unity)) { throw 'Unity editor is missing.' }
foreach ($process in Get-CimInstance Win32_Process -Filter "Name='Unity.exe'") {
    if ($process.CommandLine -and $process.CommandLine.Contains($targetProject)) { throw 'The isolated Unity project is already open.' }
}
New-Item -ItemType Directory -Path $targetProject -Force | Out-Null
& python (Join-Path $PSScriptRoot 'sync_isolated_project.py') $targetProject
if ($LASTEXITCODE -ne 0) { throw 'Isolated project synchronization failed.' }
$buildTag = Get-Date -Format 'yyyyMMdd-HHmmss'
$outputDirectory = Join-Path $sourceProject 'Builds\AndroidPlayer'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$privateLogs = Join-Path $targetProject ('Logs\android-player-' + $buildTag)
New-Item -ItemType Directory -Path $privateLogs -Force | Out-Null
$apk = Join-Path $outputDirectory ('LoopSort-' + $buildTag + '.apk')
$gradleProject = Join-Path $targetProject ('Builds\AndroidPlayer\gradle-' + $buildTag)
foreach ($phase in @('Prepare', 'Build')) {
    $editorArgs = @('-batchmode', '-nographics', '-quit', '-projectPath', $targetProject, '-buildTarget', 'Android',
        '-executeMethod', ('LoopSort0.Editor.StandalonePlayerBuild.' + $phase), '-loopSortOutput', $gradleProject, '-loopSortExportGradle',
        '-logFile', (Join-Path $privateLogs ($phase + '.log')))
    $unityProcess = Start-Process -FilePath $Unity -ArgumentList ($editorArgs | ForEach-Object { '"' + $_ + '"' }) -WindowStyle Hidden -PassThru
    $unityProcess.WaitForExit()
    if ($unityProcess.ExitCode -ne 0) { throw "$phase failed. Inspect the private log in $privateLogs; do not share raw SDK logs." }
}
& python (Join-Path $PSScriptRoot 'package_android_gradle.py') --project $gradleProject --apk $apk
if ($LASTEXITCODE -ne 0) { throw 'Modern Gradle packaging failed; inspect the isolated build log.' }
Write-Output "Android player APK: $apk"
Write-Output 'Debug-signed preview with automatic ads and normal game UI. Native completion and release checks remain separate.'

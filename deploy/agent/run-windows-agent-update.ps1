# Installed in Program Files by MSI. The task and update directory are writable only by SYSTEM/Administrators.
$ErrorActionPreference = 'Stop'
if (-not [Security.Principal.WindowsIdentity]::GetCurrent().IsSystem) { throw 'The SYSTEM update task is required.' }
$updateRoot = Join-Path $env:ProgramData 'SentinelLAN\Updates'
$runnerRoot = Join-Path $updateRoot 'Runner'
$installDirectory = Join-Path $env:ProgramFiles 'SentinelLAN\Agent'
function Assert-AdminDirectory([string]$path) {
    $item = Get-Item -LiteralPath $path -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Updater paths cannot be reparse points.' }
    $acl = Get-Acl -LiteralPath $path
    $owner = $acl.GetOwner([Security.Principal.SecurityIdentifier]).Value
    if ($owner -notin @('S-1-5-18', 'S-1-5-32-544', 'S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464')) { throw 'Updater path owner is not trusted.' }
    foreach ($rule in $acl.Access) {
        $sid = $rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
        if ($rule.AccessControlType -eq 'Allow' -and -not ($rule.PropagationFlags -band [Security.AccessControl.PropagationFlags]::InheritOnly) -and
            ($rule.FileSystemRights -band ([Security.AccessControl.FileSystemRights]'Write,Delete,DeleteSubdirectoriesAndFiles,ChangePermissions,TakeOwnership')) -and
            $sid -notin @('S-1-5-18', 'S-1-5-32-544', 'S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464')) { throw 'Updater path is writable by an untrusted account.' }
    }
}
foreach ($path in @((Split-Path $updateRoot), $updateRoot, $installDirectory)) { Assert-AdminDirectory $path }
if (-not (Test-Path -LiteralPath (Join-Path $updateRoot 'update-settings.json'))) { throw 'IT has not configured updates.' }
if (-not (Test-Path -LiteralPath $runnerRoot)) { New-Item -ItemType Directory -Path $runnerRoot | Out-Null }
Assert-AdminDirectory $runnerRoot
# The installed EXE must be released before MSI replaces it. Run a self-contained copy outside Program Files.
$runner = Join-Path $runnerRoot 'SentinelLAN.Agent.exe'
if ((Test-Path -LiteralPath $runner) -and ((Get-Item -LiteralPath $runner -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Runner cannot be a reparse point.' }
if (Test-Path -LiteralPath $runner) { Remove-Item -LiteralPath $runner -Force }
Copy-Item -LiteralPath (Join-Path $installDirectory 'SentinelLAN.Agent.exe') -Destination $runner
$process = Start-Process -FilePath $runner -ArgumentList '--update-agent' -WorkingDirectory $runnerRoot -WindowStyle Hidden -PassThru -Wait
exit $process.ExitCode

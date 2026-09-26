param(
    [Parameter(Mandatory = $true)] [string]$UserName,
    [string]$AgentPath = "$env:ProgramFiles\SentinelLAN\Agent\SentinelLAN.Agent.exe"
)

$ErrorActionPreference = 'Stop'
$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run PowerShell as Administrator to register the desktop companion for the authorized console user.'
}
$resolvedAgent = (Resolve-Path -LiteralPath $AgentPath).Path
$programFilesRoot = [IO.Path]::GetFullPath($env:ProgramFiles).TrimEnd('\') + '\'
if (-not $resolvedAgent.StartsWith($programFilesRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Install the companion binary under protected Program Files before registering a logon task.'
}
$account = [Security.Principal.NTAccount]::new($UserName)
$userSid = $account.Translate([Security.Principal.SecurityIdentifier]).Value
$taskName = 'SentinelLAN-Desktop-' + $userSid
# The account's interactive token is used; no password is requested or stored.
$action = New-ScheduledTaskAction -Execute $resolvedAgent -Argument '--desktop-companion'
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $UserName
$taskPrincipal = New-ScheduledTaskPrincipal -UserId $UserName -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -RestartCount 3 -RestartInterval ([TimeSpan]::FromMinutes(1))
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $taskPrincipal -Settings $settings -Force | Out-Null
Start-ScheduledTask -TaskName $taskName
Write-Host 'Desktop companion registered for the authorized user. This starts the helper; it does not send a lock or isolation command.'

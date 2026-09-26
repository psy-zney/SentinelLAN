param([ValidateSet('Isolation', 'RecoveryFailure', 'RegistrationFailure', 'RuleVerificationFailure')] [string]$Scenario = 'Isolation')

# OS boundary contract test. Every mutating OS cmdlet is replaced below; no firewall/task is changed.
$ErrorActionPreference = 'Stop'
$source = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'apps/agent/src/SentinelLAN.Agent.Infrastructure/WindowsCommandExecutor.cs') -Raw
$scriptText = [regex]::Match($source, '(?s)private const string IsolationScript = """\r?\n(.*?)\r?\n\s*""";').Groups[1].Value
if (-not $scriptText) { throw 'Could not load the production isolation script.' }
$script:rules = @{}
$script:registered = $false
$script:unregistered = $false
$script:denyRemoval = $false
$env:SENTINELLAN_ACTION_INPUT = '{"id":"11111111111111111111111111111111","server":"192.0.2.10","duration":30}'
function Get-NetFirewallProfile { param($PolicyStore) 1..3 | ForEach-Object { [pscustomobject]@{ Enabled = $true; AllowLocalFirewallRules = 'True' } } }
function Get-ScheduledTask { [CmdletBinding()] param($TaskName) if ($script:registered -and $TaskName -notlike '*`**') { [pscustomobject]@{ TaskName = $TaskName } } }
function New-ScheduledTaskAction { param($Execute, $Argument) [pscustomobject]@{ Execute = $Execute; Argument = $Argument } }
function New-ScheduledTaskTrigger { param([switch]$Once, $At, [switch]$AtStartup) [pscustomobject]@{ Startup = $AtStartup.IsPresent; At = $At } }
function New-ScheduledTaskSettingsSet {
    param([switch]$StartWhenAvailable, [switch]$AllowStartIfOnBatteries, [switch]$DontStopIfGoingOnBatteries, $RestartCount, $RestartInterval, $ExecutionTimeLimit)
    if (-not $AllowStartIfOnBatteries -or -not $DontStopIfGoingOnBatteries -or $RestartCount -lt 1) { throw 'Recovery must work on battery and retry failures.' }
    [pscustomobject]@{ Valid = $true }
}
function Register-ScheduledTask {
    param($TaskName, $Action, $Trigger, $Settings, $User, $RunLevel)
    if ($Scenario -eq 'RegistrationFailure') { Write-Host 'REGISTRATION_DENIED'; throw 'Mock registration denial' }
    if ($User -ne 'SYSTEM' -or $RunLevel -ne 'Highest' -or $Trigger.Count -ne 2 -or -not $Trigger[1].Startup) { throw 'Recovery must have expiry and startup triggers with SYSTEM privileges.' }
    $script:registered = $true
    $script:recovery = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String(($Action.Argument -split ' ')[-1]))
    Write-Host 'RECOVERY_REGISTERED'
}
function New-NetFirewallRule {
    param($Name, $DisplayName, $Direction, $Action, $RemoteAddress, $Profile, $Enabled, $PolicyStore)
    if (-not $script:registered) { throw 'Firewall mutation happened before durable recovery registration.' }
    if ($Name -like '*4' -and (($RemoteAddress -join ',') -ne '0.0.0.0-192.0.2.9,192.0.2.11-255.255.255.255')) { throw 'API address exclusion is incorrect.' }
    if ($Name -like '*6' -and ($RemoteAddress -join ',') -ne '::/0') { throw 'IPv6 must be covered.' }
    if ($Action -ne 'Block' -or $Profile -ne 'Any' -or $PolicyStore -ne 'PersistentStore') { throw 'Firewall block plan is incomplete.' }
    $script:rules[$Name] = [pscustomobject]@{ Name = $Name; Action = $Action; Enabled = $Enabled; Direction = $Direction }
    Write-Host ('CREATE_RULE:' + $Name)
}
function Get-NetFirewallRule {
    [CmdletBinding()] param($Name, $PolicyStore)
    if ($Scenario -eq 'RuleVerificationFailure' -and $PolicyStore -eq 'ActiveStore') { return }
    foreach ($ruleName in $Name) { if ($script:rules.ContainsKey($ruleName)) { $script:rules[$ruleName] } }
}
function Remove-NetFirewallRule {
    [CmdletBinding()] param($Name, [Parameter(ValueFromPipeline = $true)]$InputObject)
    process {
        if ($script:denyRemoval) { throw 'Mock recovery removal denied' }
        $targets = if ($InputObject) { @($InputObject.Name) } else { @($Name) }
        foreach ($ruleName in $targets) {
            if ($ruleName -notmatch '^SentinelLAN-11111111111111111111111111111111-(in|out)[46]$') { throw 'Recovery targets an unrelated rule.' }
            $script:rules.Remove($ruleName)
        }
        Write-Host 'OWN_RULES_REMOVED'
    }
}
function Unregister-ScheduledTask {
    [CmdletBinding(SupportsShouldProcess = $true)] param($TaskName)
    if ($script:rules.Count) { throw 'Recovery task was removed while firewall rules remain.' }
    $script:unregistered = $true
    Write-Host 'RECOVERY_UNREGISTERED'
}
& ([scriptblock]::Create($scriptText))
if ($script:rules.Count -ne 4 -or -not $script:registered) { throw 'Isolation did not create the full verified plan.' }
if ($Scenario -eq 'RecoveryFailure') {
    $script:denyRemoval = $true
    try { & ([scriptblock]::Create($script:recovery)); throw 'Removal failure was not raised.' }
    catch { if ($script:unregistered -or $script:rules.Count -ne 4) { throw 'Recovery task was lost after failed removal.' } }
    Write-Host 'FAILED_RECOVERY_RETAINED'
} else {
    & ([scriptblock]::Create($script:recovery))
    if ($script:rules.Count -or -not $script:unregistered) { throw 'Recovery did not remove exactly its own rules and task.' }
    Write-Host 'RECOVERY_VERIFIED'
}

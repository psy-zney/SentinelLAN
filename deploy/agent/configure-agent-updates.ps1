param(
    [string]$ManifestUrl,
    [string]$PublicKeyPath,
    [string]$PublisherThumbprint,
    [string]$CompanyServerUrl,
    [ValidateRange(15, 1440)][int]$CheckIntervalMinutes = 60,
    [switch]$Disable
)

$ErrorActionPreference = 'Stop'
$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run PowerShell as Administrator.' }
$taskName = 'SentinelLAN Agent Update'
$parent = Join-Path $env:ProgramData 'SentinelLAN'
$root = Join-Path $parent 'Updates'
if ($Disable) {
    $task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    if ($task) {
        if ($task.State -eq 'Running') { throw 'An update is running. Wait for Windows Installer to complete before disabling/uninstalling.' }
        Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    }
    # Preserve audit and trust configuration for IT review. No service/device credentials are deleted.
    Write-Output 'Automatic Agent updates disabled. Audit and device identity were preserved.'
    return
}
foreach ($value in @($ManifestUrl, $CompanyServerUrl)) {
    $uri = $null
    if (-not [Uri]::TryCreate($value, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -ne 'https' -or
        $uri.IsLoopback -or $uri.UserInfo -or $uri.Query -or $uri.Fragment) { throw 'Use trusted HTTPS URLs without credentials, query or fragment.' }
}
if (([Uri]$CompanyServerUrl).AbsolutePath -ne '/') { throw 'CompanyServerUrl must be an HTTPS origin.' }
if ($PublisherThumbprint -notmatch '^[A-Fa-f0-9]{40}$') { throw 'A pinned Authenticode publisher thumbprint is required.' }
if (-not $PublicKeyPath) { throw 'The release verification public key file is required.' }
$publicKey = Get-Content -LiteralPath $PublicKeyPath -Raw
if ($publicKey.Length -gt 8192 -or $publicKey -notmatch '^\s*-----BEGIN PUBLIC KEY-----[\s\S]+-----END PUBLIC KEY-----\s*$') { throw 'Provide only the PEM release public key, never a private key.' }
$installDirectory = Join-Path $env:ProgramFiles 'SentinelLAN\Agent'
$launcher = Join-Path $installDirectory 'run-windows-agent-update.ps1'
if (-not (Test-Path -LiteralPath $launcher) -or -not (Get-Service SentinelLANAgent -ErrorAction SilentlyContinue) -or
    -not (Test-Path -LiteralPath (Join-Path $parent 'Agent\identity.dat'))) { throw 'Install and enroll the new Agent MSI before enabling updates.' }
$existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($existingTask -and $existingTask.State -eq 'Running') { throw 'Wait for the running update before changing its trust settings.' }
$agentConfiguration = & (Join-Path $installDirectory 'SentinelLAN.Agent.exe') --check-configuration
if ($LASTEXITCODE -ne 0) { throw 'Could not verify the installed Agent company configuration.' }
$agentConfiguration = $agentConfiguration | ConvertFrom-Json
if (([Uri]$agentConfiguration.ServerUrl) -ne ([Uri]$CompanyServerUrl)) { throw 'The release company must match the installed Agent server.' }

# Protect the parent against directory replacement, while retaining LocalService read access.
# The Agent subdirectory keeps its own explicit LocalService permission for telemetry/identity.
# Check the original ACL before changing inheritance on its parent.
if (Test-Path -LiteralPath $root) {
    $previousAcl = Get-Acl -LiteralPath $root
    if ($previousAcl.GetOwner([Security.Principal.SecurityIdentifier]).Value -notin @('S-1-5-18', 'S-1-5-32-544')) { throw 'Existing Updates directory has an untrusted owner. IT must inspect it before enabling updates.' }
    foreach ($rule in $previousAcl.Access) {
        if ($rule.AccessControlType -eq 'Allow' -and -not ($rule.PropagationFlags -band [Security.AccessControl.PropagationFlags]::InheritOnly) -and
            ($rule.FileSystemRights -band ([Security.AccessControl.FileSystemRights]'Write,Delete,DeleteSubdirectoriesAndFiles,ChangePermissions,TakeOwnership')) -and
            $rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value -notin @('S-1-5-18', 'S-1-5-32-544')) { throw 'Existing Updates directory is writable by an untrusted account. IT must inspect it first.' }
    }
}
foreach ($path in @($parent, $root)) {
    if (-not (Test-Path -LiteralPath $path)) { New-Item -ItemType Directory -Path $path | Out-Null }
    if ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Updater directories cannot be reparse points.' }
    $acl = New-Object Security.AccessControl.DirectorySecurity
    $acl.SetOwner([Security.Principal.SecurityIdentifier]'S-1-5-32-544')
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($sid in @('S-1-5-18', 'S-1-5-32-544')) {
        $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule([Security.Principal.SecurityIdentifier]$sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
    }
    if ($path -eq $parent) {
        $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule([Security.Principal.SecurityIdentifier]'S-1-5-19', 'ReadAndExecute', 'None', 'None', 'Allow')))
    }
    Set-Acl -LiteralPath $path -AclObject $acl
}
$temporarySettings = Join-Path $root ('settings-' + [Guid]::NewGuid().ToString('N') + '.tmp')
@{ enabled = $true; manifestUrl = $ManifestUrl; publicKeyPem = $publicKey; publisherThumbprint = $PublisherThumbprint.ToUpperInvariant(); companyServerUrl = ([Uri]$CompanyServerUrl).GetLeftPart([UriPartial]::Authority) } |
    ConvertTo-Json | Set-Content -LiteralPath $temporarySettings -Encoding UTF8
Move-Item -LiteralPath $temporarySettings -Destination (Join-Path $root 'update-settings.json') -Force

# Fixed loader follows the same embedded-script pattern as Setup; it does not change ExecutionPolicy.
$loader = '$path = Join-Path $env:ProgramFiles ''SentinelLAN\Agent\run-windows-agent-update.ps1''; & ([scriptblock]::Create([IO.File]::ReadAllText($path)))'
$encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($loader))
$action = New-ScheduledTaskAction -Execute (Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe') -Argument "-NoLogo -NoProfile -NonInteractive -EncodedCommand $encoded"
$triggers = @(
    (New-ScheduledTaskTrigger -AtStartup),
    (New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval ([TimeSpan]::FromMinutes($CheckIntervalMinutes)))
)
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit ([TimeSpan]::FromHours(2)) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
$taskPrincipal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $triggers -Settings $settings -Principal $taskPrincipal -Description 'IT-authorized signed SentinelLAN Agent MSI updates; no forced restart.' -Force | Out-Null
$scheduler = New-Object -ComObject Schedule.Service
$scheduler.Connect()
$scheduler.GetFolder('\').GetTask($taskName).SetSecurityDescriptor('D:P(A;;FA;;;SY)(A;;FA;;;BA)', 0)
Write-Output "Agent automatic updates enabled, every $CheckIntervalMinutes minutes. Status: $root\update-status.json"

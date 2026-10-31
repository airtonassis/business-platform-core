# G1 catalog inspection harness. NOT EXECUTED.
# No provisioning, DDL, scenario execution or automatic grant changes.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Configuration,
    [Parameter(Mandatory=$true)][string]$AuthorizationReference,
    [switch]$AuthorizeInspection
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $AuthorizeInspection) { throw 'Inspection disabled. Separate human authorization required.' }
$cfg = Get-Content -Raw -LiteralPath $Configuration -Encoding UTF8 | ConvertFrom-Json
if ($cfg.executionAuthorized -ne $true -or
    [string]::IsNullOrWhiteSpace($cfg.authorizationReference) -or
    $cfg.authorizationReference -cne $AuthorizationReference) {
    throw 'Configuration must reference the explicit future authorization.'
}
if ($cfg.host -cne '127.0.0.1') { throw 'Only the separately approved loopback target is allowed.' }
if ($cfg.database -notmatch '^bpc_adr0009_g1_[a-z0-9_]+$' -or $cfg.database -match 'change_me') {
    throw 'Require a concrete dedicated test database name.'
}
if ($cfg.observerUser -notmatch '^bpc_g1_observer_[a-z0-9_]+$' -or
    $cfg.isolationMarker -notmatch '^bpc-adr0009-g1:isolated:[a-zA-Z0-9_-]+$') {
    throw 'Require dedicated observer and pre-existing isolation marker.'
}
foreach ($key in @('port','serverVersionNum','statementTimeoutMs','lockTimeoutMs')) {
    $value = $cfg.$key
    if ($null -eq $value -or "$value" -notmatch '^[1-9][0-9]*$') {
        throw "Missing approved numeric setting: $key"
    }
}
if ([int]$cfg.port -gt 65535) { throw 'Invalid port.' }
foreach ($key in @('psqlExecutable','pgpassFile','outputDirectory')) {
    if ([string]::IsNullOrWhiteSpace($cfg.$key) -or
        -not [IO.Path]::IsPathRooted($cfg.$key) -or
        -not (Test-Path -LiteralPath $cfg.$key)) { throw "Missing approved path: $key" }
}
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$secretPath = (Resolve-Path -LiteralPath $cfg.pgpassFile).ProviderPath
if ($secretPath.StartsWith($repositoryRoot + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Credential file must remain outside the repository.'
}
if (-not (Test-Path -LiteralPath $cfg.outputDirectory -PathType Container)) {
    throw 'Evidence directory must already exist under the approved access policy.'
}
$actualBinaryHash = (Get-FileHash -LiteralPath $cfg.psqlExecutable -Algorithm SHA256).Hash
if ($cfg.expectedPsqlSha256 -notmatch '^[a-fA-F0-9]{64}$' -or
    $actualBinaryHash -ine $cfg.expectedPsqlSha256) { throw 'Unapproved client binary.' }
$sql = Join-Path $PSScriptRoot '..\sql\inspect-catalog.sql'
if (-not (Test-Path -LiteralPath $sql -PathType Leaf)) { throw 'Missing fixed inspection script.' }
# Clear libpq overrides (including PGHOSTADDR/PGSERVICE/PGPASSWORD).
$savedPg = @{}
Get-ChildItem Env: | Where-Object { $_.Name -like 'PG*' } | ForEach-Object {
    $savedPg[$_.Name]=$_.Value
}
try {
    foreach ($key in @($savedPg.Keys)) { [Environment]::SetEnvironmentVariable($key,$null,'Process') }
    $env:PGPASSFILE = $cfg.pgpassFile
    $env:PGAPPNAME = 'bpc-adr0009-g1-inspection'
    $env:PGOPTIONS = "-c default_transaction_read_only=on -c statement_timeout=$($cfg.statementTimeoutMs) -c lock_timeout=$($cfg.lockTimeoutMs)"
    $arguments = @(
        '--no-psqlrc','--no-password','--quiet','--tuples-only','--no-align',
        '--host','127.0.0.1','--port',"$($cfg.port)",
        '--dbname',$cfg.database,'--username',$cfg.observerUser,
        '--set','ON_ERROR_STOP=on',
        '--set',"expected_database=$($cfg.database)",
        '--set',"isolation_marker=$($cfg.isolationMarker)",
        '--set',"observer_user=$($cfg.observerUser)",
        '--set',"server_version_num=$($cfg.serverVersionNum)",
        '--file',$sql
    )
    $output = @(& $cfg.psqlExecutable @arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        # Raw stderr may include target metadata; do not persist it automatically.
        throw 'Inspection aborted. No scenario result exists. Review failure in the approved secure channel.'
    }
    $lines = @($output | ForEach-Object { "$_".Trim() } | Where-Object { $_ })
    foreach ($line in $lines) { $null = $line | ConvertFrom-Json }
    if ($lines.Count -eq 0) { throw 'No catalog evidence collected.' }
    $runId = [Guid]::NewGuid().ToString('N')
    $path = Join-Path $cfg.outputDirectory ("catalog-" + $runId + '.jsonl')
    $stream = [IO.File]::Open($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try {
        $writer = [IO.StreamWriter]::new($stream,[Text.UTF8Encoding]::new($false))
        try { foreach ($line in $lines) { $writer.WriteLine($line) } } finally { $writer.Dispose() }
    } finally { $stream.Dispose() }
    [pscustomobject]@{
        RunId=$runId
        EvidencePath=$path
        Sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        InspectionScriptSha256=(Get-FileHash -LiteralPath $sql -Algorithm SHA256).Hash
        ConfigurationSha256=(Get-FileHash -LiteralPath $Configuration -Algorithm SHA256).Hash
        Status='COLLECTED_NOT_VALIDATED'
        ScenarioResults='NOT_EXECUTED'
    }
} finally {
    Get-ChildItem Env: | Where-Object { $_.Name -like 'PG*' } | ForEach-Object {
        [Environment]::SetEnvironmentVariable($_.Name,$null,'Process')
    }
    foreach ($key in $savedPg.Keys) { [Environment]::SetEnvironmentVariable($key,$savedPg[$key],'Process') }
}

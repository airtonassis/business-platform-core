# Offline catalog triage, NOT an execution or approval of F/Q/V/K.
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$CatalogFile)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$manifestPath=Join-Path $PSScriptRoot '..\manifests\security.expected.json'
$expected=Get-Content -Raw -LiteralPath $manifestPath -Encoding UTF8 | ConvertFrom-Json
$rows=@(Get-Content -LiteralPath $CatalogFile -Encoding UTF8 |
    Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json })
$findings=[Collections.Generic.List[object]]::new()
foreach($owner in $expected.owners) {
    $actual=@($rows | Where-Object { $_.kind -eq 'role' -and $_.name -eq $owner.name })
    if($actual.Count -ne 1) {
        $findings.Add([pscustomobject]@{Item=$owner.name;Finding='MISSING_OR_AMBIGUOUS_ROLE';Severity='BLOCKER'})
        continue
    }
    if($actual[0].canLogin -or $actual[0].superuser -or $actual[0].bypassRls -or
       $actual[0].createRole -or $actual[0].createDb -or $actual[0].replication) {
        $findings.Add([pscustomobject]@{Item=$owner.name;Finding='EXCESS_ROLE_ATTRIBUTE';Severity='P0_REVIEW'})
    }
    $findings.Add([pscustomobject]@{Item=$owner.name;Finding='EXACT_SIGNATURES_COLUMN_GRANTS_AND_GRAPH_REQUIRE_REVIEW';Severity='BLOCKER'})
}
foreach($table in $expected.tables) {
    $actual=@($rows | Where-Object { $_.kind -eq 'table' -and $_.name -eq $table.name })
    if($actual.Count -ne 1) {
        $findings.Add([pscustomobject]@{Item=$table.name;Finding='CANDIDATE_TABLE_NOT_OBSERVED';Severity='BLOCKER'})
        continue
    }
    if(-not $actual[0].rls -or -not $actual[0].forceRls -or
       $actual[0].owner -in @($expected.owners.name + $expected.runtimeLogins)) {
        $findings.Add([pscustomobject]@{Item=$table.name;Finding='RLS_OR_OWNERSHIP_REVIEW';Severity='P0_REVIEW'})
    }
}
foreach($fn in @($rows | Where-Object {$_.kind -eq 'function'})) {
    if(@($fn.acl | Where-Object {$_.grantee -eq 'PUBLIC' -and $_.privilege -eq 'EXECUTE'}).Count) {
        $findings.Add([pscustomobject]@{Item=$fn.identity;Finding='PUBLIC_EXECUTE_REVIEW';Severity='P0_REVIEW'})
    }
}
[pscustomobject]@{
    Status='BLOCKED_PENDING_BINDINGS_AND_HUMAN_REVIEW'
    EvidenceSha256=(Get-FileHash -LiteralPath $CatalogFile -Algorithm SHA256).Hash
    Findings=@($findings.ToArray())
    Notice='Triage only: effective membership, schema/column ACL, all overloads, policies, bodies and graph still require review. No PASS is produced.'
}

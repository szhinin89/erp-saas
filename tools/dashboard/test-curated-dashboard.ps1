$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
. (Join-Path $PSScriptRoot 'compose-curated-dashboard.ps1')
$source = Get-Content (Join-Path $root 'docs/ProgressDashboard/templates/dashboard-curated.html') -Raw -Encoding UTF8
$html = Get-Content (Join-Path $root 'docs/ProgressDashboard/index.html') -Raw -Encoding UTF8
$manifest = Get-Content (Join-Path $root 'docs/ProgressDashboard/templates/dashboard-curated.json') -Raw -Encoding UTF8 | ConvertFrom-Json
function Assert($condition, $message) { if (!$condition) { throw $message } }
$diagramPattern = "(?s)<div id='view-architecture'>.*?(?=<div class='groups-view')"
Assert ([regex]::Match($source,$diagramPattern).Value -ceq [regex]::Match($html,$diagramPattern).Value) 'Curated diagram or navigation changed'
foreach ($id in $manifest.curatedSections) {
    $pattern = "(?s)<section id='" + [regex]::Escape($id) + "'.*?</section>"
    Assert ([regex]::Match($source,$pattern).Value -ceq [regex]::Match($html,$pattern).Value) "Curated section changed: $id"
}
# Exercise the failure paths without writing any output or mutating data.
foreach ($broken in @(
    ($html -replace "id='dependency-graph'", "id='missing-dependency-graph'"),
    ($html -replace 'var SEARCH_INDEX =', 'var MISSING_INDEX =')
)) {
    $failed = $false
    try { $null = Merge-CuratedDashboard -GeneratedHtml $broken -ProjectRoot $root } catch { $failed = $true }
    Assert $failed 'Composition accepted a missing dynamic section/dataset'
}
$rebuilt = Merge-CuratedDashboard -GeneratedHtml $html -ProjectRoot $root
Assert ($rebuilt.Contains('Referencia historica')) 'Historical references lost'
Assert (!$rebuilt.Contains('{{DATA:')) 'Unresolved data slot'
Write-Host 'PASS: curated sections/diagram, historical references, missing section/dataset guards.'

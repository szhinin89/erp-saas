# Curated presentation recovered from c395ec6c. No Git dependency at runtime.
# The generated HTML supplies live evidence; this template owns historical UI.
function Merge-CuratedDashboard([string]$GeneratedHtml, [string]$ProjectRoot) {
    $templateRoot = Join-Path $ProjectRoot 'docs/ProgressDashboard/templates'
    $template = Get-Content (Join-Path $templateRoot 'dashboard-curated.html') -Raw -Encoding UTF8
    $manifest = Get-Content (Join-Path $templateRoot 'dashboard-curated.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($id in $manifest.dynamicSections) {
        $pattern = "(?s)<section id='" + [regex]::Escape($id) + "'.*?</section>"
        $matches = [regex]::Matches($GeneratedHtml, $pattern)
        if ($matches.Count -ne 1) { throw "Expected one generated section: $id" }
        $template = $template.Replace('{{SECTION:' + $id + '}}', $matches[0].Value)
    }
    foreach ($property in $manifest.historicalPanels.PSObject.Properties) {
        $name = $property.Name
        $match = [regex]::Match($GeneratedHtml, '(?m)^var ' + $name + ' = (.*);\r?$')
        if (!$match.Success) { throw "Missing generated dataset: $name" }
        $current = $match.Groups[1].Value | ConvertFrom-Json
        if ($name -eq 'SEARCH_INDEX') {
            $keys = @{}
            $merged = @($current)
            foreach ($item in $current) { $keys[$item.kind + '|' + $item.target + '|' + $item.label] = $true }
            foreach ($item in $property.Value) {
                if (!$keys.ContainsKey($item.kind + '|' + $item.target + '|' + $item.label)) {
                    $item.label = '[Historico] ' + $item.label
                    $merged += $item
                }
            }
            $json = ConvertTo-Json -InputObject @($merged) -Depth 30 -Compress
        } else {
            $merged = [ordered]@{}
            foreach ($entry in $property.Value.PSObject.Properties) {
                $merged[$entry.Name] = "<p class='muted-note'>Referencia historica de c395ec6c; no detectada en el analisis actual.</p>" + $entry.Value
            }
            foreach ($entry in $current.PSObject.Properties) { $merged[$entry.Name] = $entry.Value }
            $json = ConvertTo-Json -InputObject $merged -Depth 30 -Compress
        }
        $template = $template.Replace('{{DATA:' + $name + '}}', $json)
    }
    if ($template -match '\{\{(?:SECTION|DATA):') { throw 'Unresolved curated dashboard slot' }
    foreach ($id in @($manifest.curatedSections) + @($manifest.dynamicSections)) {
        if ([regex]::Matches($template, "<section id='" + [regex]::Escape($id) + "'").Count -ne 1) {
            throw "Missing or duplicated dashboard section: $id"
        }
    }
    # Every curated section must survive byte-for-byte (apart from line endings).
    $sourceTemplate = Get-Content (Join-Path $templateRoot 'dashboard-curated.html') -Raw -Encoding UTF8
    foreach ($id in $manifest.curatedSections) {
        $pattern = "(?s)<section id='" + [regex]::Escape($id) + "'.*?</section>"
        if ([regex]::Match($sourceTemplate, $pattern).Value -cne [regex]::Match($template, $pattern).Value) {
            throw "Curated section changed during composition: $id"
        }
    }
    return $template
}

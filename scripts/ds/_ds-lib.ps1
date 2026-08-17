<#
.SYNOPSIS
  Libreria compartida por ds-audit.ps1 / ds-guard.ps1 / ds-fix-known-patterns.ps1.

.DESCRIPTION
  No se ejecuta directamente. Los 3 scripts de scripts/ds/ hacen
  ". (Join-Path $PSScriptRoot '_ds-lib.ps1')" para reusar:
    - resolucion de -Scope/-ModuleName a rutas reales;
    - definicion de patrones auditables (regex);
    - clasificacion de cada hallazgo (OK_GLOBAL/OK_ICON/OK_LAYOUT/OK_CODE/
      OK_TOKEN/NOT_OK_VISUAL_LOCAL/NEEDS_DECISION);
    - deteccion de hallazgos (Get-DsFindings), comun a audit y guard.

  Es deteccion basada en lineas (regex), no un parser real de CSS/TSX:
  clasifica por heuristica (ver DS_RULES.md). Los NEEDS_DECISION siempre
  requieren revision humana; nunca se auto-corrigen.
#>

Set-StrictMode -Version Latest

function Get-DsRepoRoot {
    (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
}

# Devuelve la lista de carpetas absolutas a escanear segun -Scope/-ModuleName.
function Resolve-DsScopePaths {
    param(
        [ValidateSet("modules", "components", "styles", "all")]
        [string]$Scope = "modules",
        [string]$ModuleName
    )

    $root = Get-DsRepoRoot
    $srcRoot = Join-Path $root "frontend/src"

    $modulesPath = Join-Path $srcRoot "modules"
    if ($ModuleName) {
        $modulesPath = Join-Path $modulesPath $ModuleName
        if (-not (Test-Path $modulesPath)) {
            throw "ModuleName '$ModuleName' no existe en frontend/src/modules/"
        }
    }

    $map = @{
        modules    = @($modulesPath)
        components = @(Join-Path $srcRoot "components")
        styles     = @(Join-Path $srcRoot "styles")
        all        = @($modulesPath, (Join-Path $srcRoot "components"), (Join-Path $srcRoot "styles"))
    }
    return $map[$Scope] | Where-Object { Test-Path $_ }
}

# Lista de archivos .tsx/.css bajo las rutas resueltas.
function Get-DsTargetFiles {
    param([string[]]$Paths)
    $files = foreach ($p in $Paths) {
        Get-ChildItem -Path $p -Recurse -File -Include "*.tsx", "*.css" -ErrorAction SilentlyContinue
    }
    return $files | Sort-Object FullName -Unique
}

# Definicion de patrones: Name, Regex, y a que tipo de archivo aplica.
function Get-DsPatternDefs {
    return @(
        @{ Name = "font-family";           Regex = "font-family\s*:";  Exts = @(".css", ".tsx") }
        @{ Name = "font-style";             Regex = "font-style\s*:";   Exts = @(".css") }
        @{ Name = "italic-prop";            Regex = "\bitalic\b";       Exts = @(".tsx") }
        @{ Name = "zh-data-value--italic";  Regex = "zh-data-value--italic"; Exts = @(".css", ".tsx") }
        @{ Name = "font-size";              Regex = "font-size\s*:";    Exts = @(".css") }
        @{ Name = "font-weight";            Regex = "font-weight\s*:";  Exts = @(".css") }
        @{ Name = "line-height";            Regex = "line-height\s*:";  Exts = @(".css") }
        @{ Name = "letter-spacing";         Regex = "letter-spacing\s*:"; Exts = @(".css") }
        @{ Name = "text-transform";         Regex = "text-transform\s*:"; Exts = @(".css") }
        @{ Name = "inline-style";           Regex = "style=\{\{";       Exts = @(".tsx") }
        @{ Name = "hex-color";              Regex = "#[0-9a-fA-F]{3,8}\b"; Exts = @(".css", ".tsx") }
        @{ Name = "rgb-color";              Regex = "rgb\(";            Exts = @(".css", ".tsx") }
        @{ Name = "rgba-color";             Regex = "rgba\(";           Exts = @(".css", ".tsx") }
    )
}

# true si la linea, ya recortada, es claramente el inicio/cuerpo de un comentario
# de una sola linea. No detecta continuaciones de comentarios multilinea sin '*'
# inicial (limitacion conocida y documentada en DS_RULES.md).
function Test-DsCommentLine {
    param([string]$Line)
    $t = $Line.Trim()
    return ($t.StartsWith("/*") -or $t.StartsWith("*") -or $t.StartsWith("//"))
}

function Get-DsRelativePath {
    param([string]$FullName, [string]$Root)
    return $FullName.Substring($Root.Length + 1).Replace("\", "/")
}

# Clasifica un hallazgo puntual. $PrevSelectorLine es la linea de selector mas
# cercana hacia arriba (o la misma linea, si el bloque es de una sola linea) -
# se usa para casos ICON/GLOBAL que dependen del nombre de la clase.
function Get-DsClassification {
    param(
        [string]$PatternName,
        [string]$Line,
        [string]$PrevSelectorLine,
        [string]$RelativePath
    )

    $inModules = $RelativePath -match "^modules/"
    $isGlobalDsFile = $RelativePath -match "^(components/zh/|styles/)"
    $isDesignTokens = $RelativePath -match "^styles/design-tokens\.css$"

    switch ($PatternName) {
        "font-family" {
            if ($inModules) { return "NOT_OK_VISUAL_LOCAL" }
            if ($isGlobalDsFile) { return "OK_GLOBAL" }
            return "NEEDS_DECISION"
        }
        "font-style" {
            if ($Line -match "italic") {
                if ($inModules) { return "NOT_OK_VISUAL_LOCAL" }
                return "NEEDS_DECISION"
            }
            return "OK_LAYOUT"
        }
        "italic-prop" {
            if ($inModules) { return "NOT_OK_VISUAL_LOCAL" }
            if ($isGlobalDsFile) { return "OK_GLOBAL" }
            return "NEEDS_DECISION"
        }
        "zh-data-value--italic" {
            if ($inModules) { return "NOT_OK_VISUAL_LOCAL" }
            return "OK_GLOBAL"
        }
        "inline-style" {
            if ($inModules) { return "NOT_OK_VISUAL_LOCAL" }
            return "NEEDS_DECISION"
        }
        { $_ -in @("hex-color", "rgb-color", "rgba-color") } {
            if ($isDesignTokens) { return "OK_GLOBAL" }
            if ($inModules) { return "NOT_OK_VISUAL_LOCAL" }
            return "NEEDS_DECISION"
        }
        { $_ -in @("font-size", "font-weight") } {
            if ($Line -match "var\(--text-") { return "OK_TOKEN" }
            if ($Line -match ":\s*inherit\s*;?\s*$") { return "OK_GLOBAL" }
            if ($Line -match "(?i)icon" -or ($PrevSelectorLine -and $PrevSelectorLine -match "(?i)icon|material-symbols")) { return "OK_ICON" }
            if ($Line -match "(?i)\bcode\b|zh-code-value|--code\b") { return "OK_CODE" }
            return "NEEDS_DECISION"
        }
        "line-height" {
            if ($Line -match "var\(--text-") { return "OK_TOKEN" }
            if ($Line -match ":\s*inherit\s*;?\s*$") { return "OK_GLOBAL" }
            if ($Line -match ":\s*\d(\.\d+)?\s*;?\s*$") { return "OK_LAYOUT" }
            return "NEEDS_DECISION"
        }
        "letter-spacing" {
            if ($Line -match "var\(--text-") { return "OK_TOKEN" }
            if ($Line -match ":\s*(inherit|normal)\s*;?\s*$") { return "OK_GLOBAL" }
            if ($PrevSelectorLine -and $PrevSelectorLine -match "zh-section-title|zh-field-label|zh-row-title|ZHFieldLabel") { return "OK_GLOBAL" }
            return "NEEDS_DECISION"
        }
        "text-transform" {
            if ($Line -match ":\s*none\s*;?\s*$") { return "OK_LAYOUT" }
            if ($PrevSelectorLine -and $PrevSelectorLine -match "zh-section-title|zh-field-label|zh-row-title|ZHFieldLabel") { return "OK_GLOBAL" }
            return "NEEDS_DECISION"
        }
        default { return "NEEDS_DECISION" }
    }
}

function Get-DsSuggestedAction {
    param([string]$PatternName, [string]$Classification)

    if ($Classification -notmatch "^(NOT_OK_VISUAL_LOCAL|NEEDS_DECISION)$") {
        return "Sin accion - cumple regla DS."
    }

    $actions = @{
        "font-family"          = "Eliminar font-family local. Texto normal hereda var(--font-family) global; codigo tecnico usa .zh-code-value / ZHDataValue variant=code / Badge code."
        "font-style"           = "Eliminar font-style: italic. Usar variant muted/caption, nunca italic, para jerarquia secundaria."
        "italic-prop"          = "Quitar prop italic de ZHDataValue/similar en datos normales."
        "zh-data-value--italic"= "Quitar la clase zh-data-value--italic del className."
        "inline-style"         = "Reemplazar style={{...}} por clase CSS (global si es reutilizable, local si es layout puro)."
        "hex-color"            = "Reemplazar color hardcodeado por var(--color-*) de design-tokens.css."
        "rgb-color"            = "Reemplazar rgb(...) por var(--color-*) o color-mix() con un token."
        "rgba-color"           = "Reemplazar rgba(...) por var(--color-*) o color-mix() con un token."
        "font-size"            = "Revisar: si es dato normal, mover a token var(--text-*) o a ZHFieldLabel/ZHDataValue/ZHMoneyValue global."
        "font-weight"          = "Revisar: si es dato normal, mover a token var(--text-*-weight) o al componente global correspondiente."
        "line-height"          = "Revisar si es layout (evitar recorte) o si deberia venir de var(--text-*-height)."
        "letter-spacing"       = "Revisar si es titulo/label global (OK) o tracking local sin justificacion."
        "text-transform"       = "Revisar si es titulo/label global (OK) o uppercase local sin justificacion."
    }
    if ($actions.ContainsKey($PatternName)) { return $actions[$PatternName] }
    return "Revisar manualmente."
}

# Recorre los archivos objetivo y devuelve la lista completa de hallazgos
# (un objeto por linea que matchea un patron aplicable a esa extension).
function Get-DsFindings {
    param(
        [ValidateSet("modules", "components", "styles", "all")]
        [string]$Scope = "modules",
        [string]$ModuleName
    )

    $root = Get-DsRepoRoot
    $frontendSrcRoot = Join-Path $root "frontend/src"
    $paths = Resolve-DsScopePaths -Scope $Scope -ModuleName $ModuleName
    $files = Get-DsTargetFiles -Paths $paths
    $patterns = Get-DsPatternDefs

    $findings = [System.Collections.Generic.List[object]]::new()

    foreach ($file in $files) {
        $ext = $file.Extension
        $relPath = Get-DsRelativePath -FullName $file.FullName -Root $frontendSrcRoot
        $lines = @(Get-Content -LiteralPath $file.FullName -ErrorAction SilentlyContinue)
        if (-not $lines) { continue }

        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            if (Test-DsCommentLine -Line $line) { continue }

            foreach ($pat in $patterns) {
                if ($pat.Exts -notcontains $ext) { continue }
                if ($line -notmatch $pat.Regex) { continue }

                # Selector mas cercano: la propia linea si trae '{', si no, la
                # ultima linea hacia arriba (hasta 8) que contenga '{'.
                $selectorLine = $null
                if ($line -match "\{") {
                    $selectorLine = $line
                } else {
                    for ($j = $i - 1; $j -ge 0 -and $j -ge ($i - 8); $j--) {
                        if ($lines[$j] -match "\{") { $selectorLine = $lines[$j]; break }
                    }
                }

                $classification = Get-DsClassification -PatternName $pat.Name -Line $line -PrevSelectorLine $selectorLine -RelativePath $relPath
                $action = Get-DsSuggestedAction -PatternName $pat.Name -Classification $classification

                $findings.Add([PSCustomObject]@{
                    File           = "frontend/src/$relPath"
                    Line           = $i + 1
                    Pattern        = $pat.Name
                    Content        = $line.Trim()
                    Classification = $classification
                    Action         = $action
                })
            }
        }
    }

    return $findings
}

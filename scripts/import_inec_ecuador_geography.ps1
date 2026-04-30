<#
.SYNOPSIS
  Descarga DPA Ecuador (INEC vía ArcGIS Ecuador en Cifras) y genera SQL para geo_*.

.DESCRIPTION
  Misma lógica que import_inec_ecuador_geography.py: INSERT ... ON CONFLICT DO UPDATE.

.EXAMPLE
  .\import_inec_ecuador_geography.ps1 -OutputFile .\inec_ecuador_geography.sql
  psql $env:DATABASE_URL -v ON_ERROR_STOP=1 -f .\inec_ecuador_geography.sql

  Transacción explícita solo si hace falta:
  .\import_inec_ecuador_geography.ps1 -OutputFile .\x.sql -WrapTransaction
#>
param(
  [string] $OutputFile = "inec_ecuador_geography.sql",
  # Para psql suelto; desactivado por defecto (EF Migration ya usa transacción).
  [switch] $WrapTransaction
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$Base = "https://services7.arcgis.com/iFGeGXTAJXnjq0YN/arcgis/rest/services"
$Urls = @{
  Provinces = "$Base/Provincias_del_Ecuador/FeatureServer/0/query"
  Cantons   = "$Base/Cantones_del_Ecuador/FeatureServer/0/query"
  Parishes   = "$Base/Parroquias_del_Ecuador/FeatureServer/0/query"
}

function Sql-Literal([string] $s) {
  if ($null -eq $s) { $s = "" }
  return "'" + ($s.Replace("'", "''")) + "'"
}

function Clip([string] $s, [int] $n) {
  if ($null -eq $s) { $s = "" }
  $t = $s.Trim()
  if ($t.Length -le $n) { return $t }
  return $t.Substring(0, $n)
}

function Get-AllFeatures {
  param(
    [string] $QueryUrl,
    [string] $OutFields,
    [string] $OrderBy,
    [int] $Batch = 2000
  )
  $offset = 0
  while ($true) {
    $qs = @(
      "f=json"
      "where=1%3D1"
      "outFields=$([uri]::EscapeDataString($OutFields))"
      "returnGeometry=false"
      "orderByFields=$([uri]::EscapeDataString($OrderBy))"
      "resultRecordCount=$Batch"
      "resultOffset=$offset"
    ) -join "&"
    $uri = "$QueryUrl`?$qs"
    $resp = Invoke-RestMethod -Uri $uri -Method Get -TimeoutSec 120
    if ($resp.error) { throw "ArcGIS error: $($resp.error | ConvertTo-Json -Compress)" }
    $feats = @($resp.features)
    foreach ($f in $feats) {
      if ($f.attributes) { $f.attributes }
    }
    if ($feats.Count -lt $Batch) { break }
    $offset += $Batch
  }
}

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("-- INEC / DPA Ecuador (ArcGIS)")
if ($WrapTransaction) {
  [void]$sb.AppendLine("BEGIN;")
  [void]$sb.AppendLine("")
}
[void]$sb.AppendLine("INSERT INTO geo_countries (id, name) VALUES ('EC', 'Ecuador')")
[void]$sb.AppendLine("ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name;")
[void]$sb.AppendLine("")

$provinces = @(Get-AllFeatures -QueryUrl $Urls.Provinces -OutFields "DPA_PROVIN,DPA_DESPRO" -OrderBy "DPA_PROVIN")
foreach ($a in ($provinces | Sort-Object DPA_PROVIN)) {
  $provId = Clip $a.DPA_PROVIN 10
  $name = Clip $a.DPA_DESPRO 120
  if (-not $provId -or -not $name) { continue }
  [void]$sb.AppendLine("INSERT INTO geo_provinces (id, country_id, name) VALUES ($(Sql-Literal $provId), 'EC', $(Sql-Literal $name))")
  [void]$sb.AppendLine("ON CONFLICT (id) DO UPDATE SET country_id = EXCLUDED.country_id, name = EXCLUDED.name;")
}
[void]$sb.AppendLine("")

$cantons = @(Get-AllFeatures -QueryUrl $Urls.Cantons -OutFields "DPA_CANTON,DPA_DESCAN,DPA_PROVIN" -OrderBy "DPA_CANTON")
foreach ($a in ($cantons | Sort-Object DPA_CANTON)) {
  $cid = Clip $a.DPA_CANTON 10
  $cantonName = Clip $a.DPA_DESCAN 120
  $provId = Clip $a.DPA_PROVIN 10
  if (-not $cid -or -not $cantonName -or -not $provId) { continue }
  [void]$sb.AppendLine("INSERT INTO geo_cantons (id, province_id, name) VALUES ($(Sql-Literal $cid), $(Sql-Literal $provId), $(Sql-Literal $cantonName))")
  [void]$sb.AppendLine("ON CONFLICT (id) DO UPDATE SET province_id = EXCLUDED.province_id, name = EXCLUDED.name;")
}
[void]$sb.AppendLine("")

$parishes = @(Get-AllFeatures -QueryUrl $Urls.Parishes -OutFields "DPA_PARROQ,DPA_DESPAR,DPA_CANTON" -OrderBy "DPA_PARROQ")
foreach ($a in ($parishes | Sort-Object DPA_PARROQ)) {
  $rid = Clip $a.DPA_PARROQ 10
  $name = Clip $a.DPA_DESPAR 120
  $cid = Clip $a.DPA_CANTON 10
  if (-not $rid -or -not $name -or -not $cid) { continue }
  [void]$sb.AppendLine("INSERT INTO geo_parishes (id, canton_id, name) VALUES ($(Sql-Literal $rid), $(Sql-Literal $cid), $(Sql-Literal $name))")
  [void]$sb.AppendLine("ON CONFLICT (id) DO UPDATE SET canton_id = EXCLUDED.canton_id, name = EXCLUDED.name;")
}

[void]$sb.AppendLine("")
if ($WrapTransaction) {
  [void]$sb.AppendLine("COMMIT;")
}

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$outPath = if ([System.IO.Path]::IsPathRooted($OutputFile)) { $OutputFile } else { Join-Path (Get-Location) $OutputFile }
[System.IO.File]::WriteAllText($outPath, $sb.ToString(), $utf8NoBom)
Write-Host "Escrito: $outPath ($($parishes.Count) parroquias, $($cantons.Count) cantones, $($provinces.Count) provincias)"

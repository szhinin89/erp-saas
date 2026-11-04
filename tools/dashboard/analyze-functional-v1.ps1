$ErrorActionPreference = 'Stop'
node (Join-Path $PSScriptRoot 'analyze-functional-v1.mjs')
if ($LASTEXITCODE -ne 0) { throw 'Functional V1 calculation failed; no dashboard may be published.' }

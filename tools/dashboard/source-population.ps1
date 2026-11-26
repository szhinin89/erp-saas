# =============================================================================
# ZH Technologies
# Dashboard -- poblacion unica de fuentes (DH-006 / DH-007)
#
# Se carga con dot-source desde los analizadores. La poblacion son los archivos
# versionados (git ls-files) y su contenido se lee del arbol de trabajo. Los
# archivos no versionados (dependencias, bin, obj, dist, fuentes sin 'git add')
# no se analizan. Sin Git no hay poblacion: se falla, nunca se escanea el disco.
# =============================================================================

$SourcePopulationMethod = "git-tracked-v1"
$script:SourcePopulationCache = @{}

function Invoke-PopulationGit([string]$ProjectRoot, [string]$Arguments)
{
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = "git"
    $info.Arguments = "-c core.quotepath=off $Arguments"
    $info.WorkingDirectory = $ProjectRoot
    $info.UseShellExecute = $false
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = [System.Text.Encoding]::UTF8

    try { $process = [System.Diagnostics.Process]::Start($info) }
    catch { throw "Source population: no se pudo ejecutar git ($($_.Exception.Message)). No se analiza nada." }

    $errorTask = $process.StandardError.ReadToEndAsync()
    $output = $process.StandardOutput.ReadToEnd()
    $process.WaitForExit()

    if($process.ExitCode -ne 0)
    {
        throw "Source population: 'git $Arguments' fallo (exit $($process.ExitCode)): $($errorTask.Result.Trim()). No se analiza nada."
    }

    return $output
}

function Split-PopulationList([string]$Output)
{
    # [char[]] explicito: Windows PowerShell 5.1 ignora las opciones con un [char] suelto.
    return @($Output.Split([char[]]@([char]0), [System.StringSplitOptions]::RemoveEmptyEntries))
}

function Get-SourcePopulation([string]$ProjectRoot)
{
    if($script:SourcePopulationCache.ContainsKey($ProjectRoot)) { return $script:SourcePopulationCache[$ProjectRoot] }

    $prefix = (Invoke-PopulationGit $ProjectRoot "rev-parse --show-prefix").Trim()
    if($prefix) { throw "Source population: '$ProjectRoot' no es la raiz del repositorio Git. No se analiza nada." }

    $tracked = Split-PopulationList (Invoke-PopulationGit $ProjectRoot "ls-files -z")
    if($tracked.Count -eq 0) { throw "Source population: el repositorio no tiene archivos versionados. No se analiza nada." }

    # Indice carpeta -> archivos versionados (todos los niveles).
    $byFolder = New-Object 'System.Collections.Generic.Dictionary[string,System.Collections.Generic.List[string]]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach($path in $tracked)
    {
        $cut = $path.LastIndexOf("/")
        while($cut -gt 0)
        {
            $folder = $path.Substring(0, $cut)
            if(-not $byFolder.ContainsKey($folder)) { $byFolder[$folder] = New-Object 'System.Collections.Generic.List[string]' }
            $byFolder[$folder].Add($path)
            $cut = $folder.LastIndexOf("/")
        }
    }

    $population = [PSCustomObject]@{
        Commit    = (Invoke-PopulationGit $ProjectRoot "rev-parse HEAD").Trim()
        ByFolder  = $byFolder
        Tracked   = $tracked
        Untracked = @(Split-PopulationList (Invoke-PopulationGit $ProjectRoot "ls-files -z --others --exclude-standard"))
        Modified  = @(Split-PopulationList (Invoke-PopulationGit $ProjectRoot "diff --name-only --no-renames -z HEAD"))
    }

    $script:SourcePopulationCache[$ProjectRoot] = $population
    return $population
}

# Filtra rutas relativas de Git (con '/') por carpetas y patrones de nombre.
function Select-PopulationPaths($Paths, [string[]]$Scope, [string[]]$Include, [switch]$TopOnly)
{
    foreach($folder in $Scope)
    {
        $prefix = $folder.Replace("\", "/").Trim("/") + "/"

        foreach($path in $Paths)
        {
            if(-not $path.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) { continue }
            if($TopOnly -and $path.IndexOf("/", $prefix.Length) -ge 0) { continue }

            $name = $path.Substring($path.LastIndexOf("/") + 1)
            foreach($pattern in $Include)
            {
                if($name -like $pattern) { $path; break }
            }
        }
    }
}

# Mismo orden que 'Get-ChildItem -Recurse': archivos de una carpeta antes que sus
# subcarpetas, sin distinguir mayusculas. Independiente del orden de Git y del disco.
function Get-PopulationSortKey([string]$Path)
{
    $segments = $Path.Split("/")
    $last = $segments.Count - 1
    $parts = for($i = 0; $i -le $last; $i++) { $(if($i -eq $last) { "0" } else { "1" }) + $segments[$i].ToUpperInvariant() }
    return ($parts -join [string][char]1)
}

function Get-OrdinalSorted($Items, [scriptblock]$Key)
{
    $array = @($Items)
    if($array.Count -lt 2) { return $array }
    $keys = [string[]]@($array | ForEach-Object { & $Key $_ })
    [System.Array]$values = [object[]]$array
    [System.Array]::Sort([System.Array]$keys, $values, [System.StringComparer]::Ordinal)
    return $values
}

# Archivos versionados que existen en el arbol de trabajo (FileInfo).
function Get-SourceFiles([string]$ProjectRoot, [string[]]$Scope, [string[]]$Include = @("*"), [switch]$TopOnly)
{
    $population = Get-SourcePopulation $ProjectRoot

    foreach($folder in $Scope)
    {
        $key = $folder.Replace("\", "/").Trim("/")
        if(-not $population.ByFolder.ContainsKey($key)) { continue }
        $selected = @(Select-PopulationPaths $population.ByFolder[$key] @($key) $Include -TopOnly:$TopOnly)
        foreach($path in (Get-OrdinalSorted $selected { param($p) Get-PopulationSortKey $p }))
        {
            $full = Join-Path $ProjectRoot $path.Replace("/", "\")
            if([System.IO.File]::Exists($full)) { New-Object System.IO.FileInfo($full) }
        }
    }
}

# Subcarpetas inmediatas que contienen al menos un archivo versionado existente.
function Get-SourceDirectories([string]$ProjectRoot, [string]$Scope)
{
    $base = (Join-Path $ProjectRoot $Scope.Replace("/", "\")).TrimEnd("\")
    $names = @{}

    foreach($file in @(Get-SourceFiles $ProjectRoot @($Scope)))
    {
        $rest = $file.FullName.Substring($base.Length + 1)
        $cut = $rest.IndexOf("\")
        if($cut -gt 0) { $names[$rest.Substring(0, $cut)] = $true }
    }

    foreach($name in (Get-OrdinalSorted @($names.Keys) { param($n) $n.ToUpperInvariant() }))
    {
        [PSCustomObject]@{ Name = $name; FullName = (Join-Path $base $name) }
    }
}

# Carpetas con un nombre dado, a cualquier nivel bajo el alcance, con archivos versionados.
function Get-SourceFolders([string]$ProjectRoot, [string]$Scope, [string]$Name)
{
    $base = (Join-Path $ProjectRoot $Scope.Replace("/", "\")).TrimEnd("\")
    $found = @{}

    foreach($file in @(Get-SourceFiles $ProjectRoot @($Scope)))
    {
        $folder = $file.DirectoryName
        $chain = @()
        while($folder.Length -gt $base.Length)
        {
            if([System.IO.Path]::GetFileName($folder) -eq $Name) { $chain = @($folder) + $chain }
            $folder = [System.IO.Path]::GetDirectoryName($folder)
        }

        foreach($match in $chain)
        {
            if($found.ContainsKey($match)) { continue }
            $found[$match] = $true
            [PSCustomObject]@{ Name = [System.IO.Path]::GetFileName($match); FullName = $match }
        }
    }
}

function Get-SourceRelativePath([string]$ProjectRoot, [string]$FullName)
{
    return $FullName.Substring($ProjectRoot.TrimEnd("\").Length + 1)
}

# Registra en data/source-population.json la poblacion usada por un analizador.
# Sin marcas de tiempo: mismo arbol, mismo contenido.
function Register-SourcePopulation([string]$ProjectRoot, [string]$Analyzer, [string[]]$Scope, [string[]]$Include, $Files)
{
    $population = Get-SourcePopulation $ProjectRoot
    $output = Join-Path $ProjectRoot "docs\ProgressDashboard\data\source-population.json"

    function ConvertTo-LocalList($Paths) { return @($Paths | Where-Object { $_ } | ForEach-Object { $_.Replace("/", "\") } | Sort-Object) }

    $missing = @(Select-PopulationPaths $population.Tracked $Scope $Include |
        Where-Object { -not [System.IO.File]::Exists((Join-Path $ProjectRoot $_.Replace("/", "\"))) })

    $entries = @{}
    if(Test-Path $output)
    {
        $previous = Get-Content $output -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach($property in $previous.Analyzers.PSObject.Properties) { $entries[$property.Name] = $property.Value }
    }

    $entries[$Analyzer] = [ordered]@{
        Commit            = $population.Commit
        Scope             = @($Scope)
        Include           = @($Include)
        FilesAnalyzed     = @($Files).Count
        MissingTracked    = @(ConvertTo-LocalList $missing)
        ExcludedUntracked = @(ConvertTo-LocalList (Select-PopulationPaths $population.Untracked $Scope $Include))
        ModifiedInScope   = @(ConvertTo-LocalList (Select-PopulationPaths $population.Modified $Scope $Include))
    }

    $sorted = [ordered]@{}
    foreach($name in (@($entries.Keys) | Sort-Object)) { $sorted[$name] = $entries[$name] }

    # El estado del arbol excluye las salidas del propio Dashboard.
    $treeModified = @($population.Modified | Where-Object { -not $_.StartsWith("docs/ProgressDashboard/") })

    [ordered]@{
        Method           = $SourcePopulationMethod
        Commit           = $population.Commit
        TreeDirty        = ($treeModified.Count -gt 0)
        TreeModified     = @(ConvertTo-LocalList $treeModified)
        Analyzers        = $sorted
    } | ConvertTo-Json -Depth 8 | Set-Content $output -Encoding UTF8
}

# Directivas de importacion C# reales. Comentarios y literales se descartan.
# No detecta referencias totalmente calificadas ni <Using> de los .csproj.
function Get-ImportDirectives([string]$Content)
{
    $noise = '(?s)//[^\r\n]*|/\*.*?\*/|\$*""".*?"""|(?:\$@|@\$|@)"(?:""|[^"])*"|"(?:\\.|[^"\\\r\n])*"|''(?:\\.|[^''\\\r\n])+'''
    $blank = [System.Text.RegularExpressions.MatchEvaluator] { param($m) ' ' + ("`n" * ($m.Value.Split([char]10).Count - 1)) }
    $code = [regex]::Replace($Content, $noise, $blank)

    $directive = '(?m)(?:^|(?<=[;{}]))[ \t]*(?:global\s+)?using\s+(?:static\s+)?(?:unsafe\s+)?(?:@?\w+\s*=\s*)?(?<name>[\w.:@]+)\s*(?:<[^;()]*>)?\s*;'

    foreach($match in [regex]::Matches($code, $directive))
    {
        $before = $code.Substring(0, $match.Groups["name"].Index)
        [PSCustomObject]@{ Name = $match.Groups["name"].Value; Line = $before.Split([char]10).Count }
    }
}

# Importaciones cuyo nombre contiene la referencia prohibida (mismo alcance que
# la busqueda por texto anterior, limitado a directivas reales).
function Find-ForbiddenImports($Files, [string]$Reference)
{
    foreach($file in @($Files))
    {
        $content = [System.IO.File]::ReadAllText($file.FullName)
        if($content.IndexOf($Reference, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) { continue }

        foreach($import in @(Get-ImportDirectives $content))
        {
            if($import.Name.IndexOf($Reference, [System.StringComparison]::OrdinalIgnoreCase) -ge 0)
            {
                [PSCustomObject]@{ File = $file; Line = $import.Line; Name = $import.Name }
            }
        }
    }
}

# Cuenta lineas que contienen el texto (misma metrica que antes, en todos los niveles).
function Measure-SourceLines($Files, [string]$Pattern)
{
    $paths = @(@($Files) | ForEach-Object { $_.FullName })
    if($paths.Count -eq 0) { return 0 }
    return @(Select-String -LiteralPath $paths -Pattern $Pattern -SimpleMatch).Count
}

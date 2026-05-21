Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

param(
    [switch]$Force,
    [switch]$DryRun
)

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Read-Value {
    param(
        [string]$Prompt,
        [string]$Default = ""
    )

    if ([string]::IsNullOrWhiteSpace($Default)) {
        $value = Read-Host $Prompt
    } else {
        $value = Read-Host "$Prompt [$Default]"
        if ([string]::IsNullOrWhiteSpace($value)) {
            $value = $Default
        }
    }

    return $value
}

function To-SnakeCase {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $Value }
    $snake = [regex]::Replace($Value, '([a-z0-9])([A-Z])', '$1_$2')
    return $snake.ToLowerInvariant()
}

function To-CamelCase {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $Value }
    return $Value.Substring(0,1).ToLowerInvariant() + $Value.Substring(1)
}

function Get-Plural {
    param([string]$Value)
    if ($Value.EndsWith("y") -and $Value.Length -gt 1) {
        $prev = $Value.Substring($Value.Length - 2, 1).ToLowerInvariant()
        if ("aeiou".IndexOf($prev) -lt 0) {
            return $Value.Substring(0, $Value.Length - 1) + "ies"
        }
    }
    if ($Value.EndsWith("s") -or $Value.EndsWith("x") -or $Value.EndsWith("ch") -or $Value.EndsWith("sh")) {
        return $Value + "es"
    }
    return $Value + "s"
}

function Ensure-Directory {
    param([string]$Path)
    if (Test-Path $Path) { return }
    if ($DryRun) {
        Write-Info "DRY-RUN mkdir $Path"
        return
    }
    New-Item -Path $Path -ItemType Directory -Force | Out-Null
}

function Write-File {
    param(
        [string]$Path,
        [string]$Content
    )

    if ((Test-Path $Path) -and -not $Force) {
        Write-Warn "Existe y se omite (use -Force): $Path"
        return
    }

    if ($DryRun) {
        Write-Info "DRY-RUN write $Path"
        return
    }

    $dir = Split-Path -Parent $Path
    Ensure-Directory $dir
    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
    Write-Info "Generado: $Path"
}

function Ensure-LineInFile {
    param(
        [string]$Path,
        [string]$Line
    )
    if (-not (Test-Path $Path)) {
        throw "No existe archivo: $Path"
    }

    $content = [System.IO.File]::ReadAllText($Path)
    if ($content.Contains($Line)) { return }

    $newContent = "$Line`r`n$content"
    if ($DryRun) {
        Write-Info "DRY-RUN prepend line in $Path: $Line"
        return
    }
    [System.IO.File]::WriteAllText($Path, $newContent, [System.Text.UTF8Encoding]::new($false))
    Write-Info "Actualizado: $Path"
}

function Insert-Before-Marker {
    param(
        [string]$Path,
        [string]$Marker,
        [string]$InsertText,
        [string]$DetectDuplicate
    )

    if (-not (Test-Path $Path)) {
        throw "No existe archivo: $Path"
    }

    $content = [System.IO.File]::ReadAllText($Path)
    if ($content.Contains($DetectDuplicate)) { return }

    $idx = $content.IndexOf($Marker)
    if ($idx -lt 0) {
        throw "No se encontró marker '$Marker' en: $Path"
    }

    $newContent = $content.Insert($idx, $InsertText)
    if ($DryRun) {
        Write-Info "DRY-RUN insert block in $Path"
        return
    }
    [System.IO.File]::WriteAllText($Path, $newContent, [System.Text.UTF8Encoding]::new($false))
    Write-Info "Actualizado: $Path"
}

function Parse-Field {
    param([string]$Line)
    $pattern = '^\s*([A-Za-z][A-Za-z0-9_]*)\s*:\s*(string|int|long|decimal|bool|DateTime|DateOnly|Guid)(?:\s*:\s*([0-9]+(?:,[0-9]+)?))?(?:\s*:\s*(req|required|opt|optional))?\s*$'
    $m = [regex]::Match($Line, $pattern)
    if (-not $m.Success) {
        throw "Formato inválido: '$Line'. Usa Field:Type[:MaxOrPrecision][:req|opt]"
    }

    $name = $m.Groups[1].Value
    $type = $m.Groups[2].Value
    $lenOrPrecision = $m.Groups[3].Value
    $reqToken = $m.Groups[4].Value.ToLowerInvariant()
    $required = $true
    if ($reqToken -eq "opt" -or $reqToken -eq "optional") {
        $required = $false
    }

    [PSCustomObject]@{
        Name = $name
        Type = $type
        LenOrPrecision = $lenOrPrecision
        Required = $required
    }
}

function Get-CSharpType {
    param($Field)
    $baseType = $Field.Type
    if ($baseType -eq "string") { return $Field.Required ? "string" : "string?" }
    if (-not $Field.Required) { return "$baseType?" }
    return $baseType
}

function Get-PropertySetterExpression {
    param(
        $Field,
        [string]$VariableName
    )
    if ($Field.Type -eq "string") {
        if ($Field.Required) {
            return "$VariableName.Trim()"
        }
        return "string.IsNullOrWhiteSpace($VariableName) ? null : $VariableName.Trim()"
    }
    return $VariableName
}

Write-Host ""
Write-Host "=== ERP Module Scaffold (Master Entity) ===" -ForegroundColor Green
Write-Host "Formato campos: Field:Type[:MaxOrPrecision][:req|opt]" -ForegroundColor DarkGray
Write-Host "Ejemplos: Code:string:20:req | Description:string:250:opt | Price:decimal:18,4:req" -ForegroundColor DarkGray
Write-Host ""

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$srcRoot = Join-Path $repoRoot "backend/src"

$moduleName = Read-Value -Prompt "Module name (PascalCase, ej: Billing)" -Default ""
$entityName = Read-Value -Prompt "Entity name (PascalCase singular, ej: PaymentMethod)" -Default ""
$tableNameDefault = To-SnakeCase $entityName
$tableName = Read-Value -Prompt "Table name (snake_case)" -Default $tableNameDefault
$generateControllerAnswer = Read-Value -Prompt "Generate API controller? (Y/N)" -Default "Y"
$generateController = $generateControllerAnswer.Trim().ToUpperInvariant() -eq "Y"

if ([string]::IsNullOrWhiteSpace($moduleName) -or [string]::IsNullOrWhiteSpace($entityName)) {
    throw "Module name y Entity name son obligatorios."
}

$fields = New-Object System.Collections.Generic.List[object]
while ($true) {
    $line = Read-Host "Field (enter vacío para terminar)"
    if ([string]::IsNullOrWhiteSpace($line)) { break }
    $field = Parse-Field -Line $line
    $fields.Add($field)
}

if ($fields.Count -eq 0) {
    throw "Debes ingresar al menos 1 campo."
}

$entityPlural = Get-Plural $entityName
$entityPluralCamel = To-CamelCase $entityPlural
$moduleNamespace = "ERP.Domain.Modules.$moduleName"
$appNamespace = "ERP.Application.Modules.$moduleName"

$domainEntitiesDir = Join-Path $srcRoot "ERP.Domain/Modules/$moduleName/Entities"
$domainInterfacesDir = Join-Path $srcRoot "ERP.Domain/Modules/$moduleName/Interfaces"
$appDtosDir = Join-Path $srcRoot "ERP.Application/Modules/$moduleName/DTOs"
$appUseCasesDir = Join-Path $srcRoot "ERP.Application/Modules/$moduleName/UseCases"
$infraConfigDir = Join-Path $srcRoot "ERP.Infrastructure/Persistence/Configurations/$moduleName"
$infraRepoDir = Join-Path $srcRoot "ERP.Infrastructure/Persistence/Repositories"
$apiControllersDir = Join-Path $srcRoot "ERP.API/Controllers"

Ensure-Directory $domainEntitiesDir
Ensure-Directory $domainInterfacesDir
Ensure-Directory $appDtosDir
Ensure-Directory $appUseCasesDir
Ensure-Directory $infraConfigDir
Ensure-Directory $infraRepoDir
if ($generateController) { Ensure-Directory $apiControllersDir }

$entityProps = @()
$createParams = @()
$updateParams = @()
$createAssignments = @()
$updateAssignments = @()
$dtoCtorParams = @("Guid Id")
$dtoCtorValues = @("entity.Id")
$detailCtorParams = @("Guid Id")
$detailCtorValues = @("entity.Id")
$validatorRulesCreate = @()
$validatorRulesUpdate = @()
$configLines = @()
$searchField = $null

foreach ($f in $fields) {
    $type = Get-CSharpType -Field $f
    $camel = To-CamelCase $f.Name
    $colName = To-SnakeCase $f.Name
    $setterExprCreate = Get-PropertySetterExpression -Field $f -VariableName $camel
    $setterExprUpdate = Get-PropertySetterExpression -Field $f -VariableName $camel

    $entityProps += "    public $type $($f.Name) { get; private set; }"

    $paramType = $type
    $createParams += "        $paramType $camel"
    $updateParams += "        $paramType $camel"
    $createAssignments += "            $($f.Name) = $setterExprCreate,"
    $updateAssignments += "        $($f.Name) = $setterExprUpdate;"

    $dtoCtorParams += "    $type $($f.Name)"
    $dtoCtorValues += "            entity.$($f.Name)"
    $detailCtorParams += "    $type $($f.Name)"
    $detailCtorValues += "            entity.$($f.Name)"

    if ($f.Type -eq "string") {
        if (-not $searchField) { $searchField = $f }
        if ($f.Required) {
            $validatorRulesCreate += @(
                "        RuleFor(x => x.$($f.Name))",
                "            .NotEmpty().WithMessage(""$($f.Name) is required."");"
            )
            $validatorRulesUpdate += @(
                "        RuleFor(x => x.$($f.Name))",
                "            .NotEmpty().WithMessage(""$($f.Name) is required."");"
            )
        }
        if (-not [string]::IsNullOrWhiteSpace($f.LenOrPrecision)) {
            $validatorRulesCreate += @(
                "        RuleFor(x => x.$($f.Name))",
                "            .MaximumLength($($f.LenOrPrecision)).WithMessage(""$($f.Name) max length is $($f.LenOrPrecision)."");"
            )
            $validatorRulesUpdate += @(
                "        RuleFor(x => x.$($f.Name))",
                "            .MaximumLength($($f.LenOrPrecision)).WithMessage(""$($f.Name) max length is $($f.LenOrPrecision)."");"
            )
        }
    }

    $cfg = "        builder.Property(x => x.$($f.Name)).HasColumnName(""$colName"")"
    if ($f.Type -eq "string" -and -not [string]::IsNullOrWhiteSpace($f.LenOrPrecision)) {
        $cfg += ".HasMaxLength($($f.LenOrPrecision))"
    }
    if ($f.Type -eq "decimal" -and -not [string]::IsNullOrWhiteSpace($f.LenOrPrecision)) {
        $parts = $f.LenOrPrecision.Split(",")
        if ($parts.Count -eq 2) {
            $cfg += ".HasPrecision($($parts[0]), $($parts[1]))"
        }
    }
    if ($f.Required) {
        $cfg += ".IsRequired()"
    }
    $cfg += ";"
    $configLines += $cfg
}

$searchCondition = "true"
if ($searchField) {
    $searchCondition = "x.$($searchField.Name).ToLower().Contains(s)"
    if (-not $searchField.Required) {
        $searchCondition = "(x.$($searchField.Name) != null && x.$($searchField.Name)!.ToLower().Contains(s))"
    }
}

$entityContent = @"
using ERP.Domain.Common;

namespace ERP.Domain.Modules.$moduleName.Entities;

public sealed class $entityName : MasterEntity
{
$($entityProps -join "`r`n")

    private $entityName() { }

    public static $entityName Create(
        Guid tenantId,
$($createParams -join ",`r`n")
,
        Guid createdBy)
    {
        var entity = new $entityName
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
$($createAssignments -join "`r`n")
            IsActive = true
        };
        entity.SetCreated(createdBy);
        return entity;
    }

    public void Update(
$($updateParams -join ",`r`n")
,
        Guid updatedBy)
    {
$($updateAssignments -join "`r`n")
        SetUpdated(updatedBy);
    }
}
"@

$repoInterfaceContent = @"
using ERP.Domain.Modules.$moduleName.Entities;

namespace ERP.Domain.Modules.$moduleName.Interfaces;

public interface I$($entityName)Repository
{
    Task AddAsync($entityName entity, CancellationToken ct = default);
    Task<IReadOnlyList<$entityName>> GetAsync(Guid tenantId, bool? activeFilter = true, string? search = null, CancellationToken ct = default);
    Task<$entityName?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
"@

$dtoContent = @"
namespace ERP.Application.Modules.$moduleName.DTOs;

public sealed record $($entityName)Dto(
$($dtoCtorParams -join ",`r`n")
);

public sealed record $($entityName)DetailDto(
$($detailCtorParams -join ",`r`n"),
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid CreatedBy,
    Guid? UpdatedBy,
    bool IsActive
);
"@

$createCommandDir = Join-Path $appUseCasesDir "Create$entityName"
$updateCommandDir = Join-Path $appUseCasesDir "Update$entityName"
$getByIdDir = Join-Path $appUseCasesDir "Get$($entityName)ById"
$getListDir = Join-Path $appUseCasesDir "Get$($entityPlural)"
$disableDir = Join-Path $appUseCasesDir "Disable$entityName"
$enableDir = Join-Path $appUseCasesDir "Enable$entityName"

Ensure-Directory $createCommandDir
Ensure-Directory $updateCommandDir
Ensure-Directory $getByIdDir
Ensure-Directory $getListDir
Ensure-Directory $disableDir
Ensure-Directory $enableDir

$commandParamRows = @()
foreach ($f in $fields) {
    $commandParamRows += "    $(Get-CSharpType -Field $f) $($f.Name)"
}

$createCommandContent = @"
using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.$moduleName.DTOs;

namespace ERP.Application.Modules.$moduleName.UseCases.Create$entityName;

public sealed record Create$entityName`Command(
$($commandParamRows -join ",`r`n"),
    bool IsActive = true) : IRequest<Result<$($entityName)Dto>>;
"@

$createHandlerArgs = @()
foreach ($f in $fields) { $createHandlerArgs += "command.$($f.Name)" }
$createHandlerArgText = $createHandlerArgs -join ",`r`n            "

$dtoCreateMap = $dtoCtorValues -join ",`r`n            "

$createHandlerContent = @"
using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.$moduleName.DTOs;
using ERP.Domain.Modules.$moduleName.Entities;
using ERP.Domain.Modules.$moduleName.Interfaces;

namespace ERP.Application.Modules.$moduleName.UseCases.Create$entityName;

public sealed class Create$entityName`CommandHandler : IRequestHandler<Create$entityName`Command, Result<$($entityName)Dto>>
{
    private readonly I$($entityName)Repository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public Create$entityName`CommandHandler(I$($entityName)Repository repo, ICurrentTenant tenant, ICurrentUser user)
    {
        _repo = repo;
        _tenant = tenant;
        _user = user;
    }

    public async Task<Result<$($entityName)Dto>> Handle(Create$entityName`Command command, CancellationToken ct)
    {
        var entity = $entityName.Create(
            _tenant.TenantId,
            $createHandlerArgText,
            _user.UserId);

        if (!command.IsActive)
            entity.Disable(_user.UserId);

        await _repo.AddAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);

        return Result<$($entityName)Dto>.Success(new $($entityName)Dto(
            $dtoCreateMap
        ));
    }
}
"@

$createValidatorContent = @"
using FluentValidation;

namespace ERP.Application.Modules.$moduleName.UseCases.Create$entityName;

public sealed class Create$entityName`CommandValidator : AbstractValidator<Create$entityName`Command>
{
    public Create$entityName`CommandValidator()
    {
$($validatorRulesCreate -join "`r`n")
    }
}
"@

$updateCommandRows = @("    Guid Id")
foreach ($f in $fields) { $updateCommandRows += "    $(Get-CSharpType -Field $f) $($f.Name)" }

$updateCommandContent = @"
using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.$moduleName.DTOs;

namespace ERP.Application.Modules.$moduleName.UseCases.Update$entityName;

public sealed record Update$entityName`Command(
$($updateCommandRows -join ",`r`n"),
    bool IsActive) : IRequest<Result<$($entityName)Dto>>;
"@

$updateArgs = @()
foreach ($f in $fields) { $updateArgs += "command.$($f.Name)" }
$updateArgText = $updateArgs -join ",`r`n            "

$updateHandlerContent = @"
using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.$moduleName.DTOs;
using ERP.Domain.Modules.$moduleName.Interfaces;

namespace ERP.Application.Modules.$moduleName.UseCases.Update$entityName;

public sealed class Update$entityName`CommandHandler : IRequestHandler<Update$entityName`Command, Result<$($entityName)Dto>>
{
    private readonly I$($entityName)Repository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public Update$entityName`CommandHandler(I$($entityName)Repository repo, ICurrentTenant tenant, ICurrentUser user)
    {
        _repo = repo;
        _tenant = tenant;
        _user = user;
    }

    public async Task<Result<$($entityName)Dto>> Handle(Update$entityName`Command command, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(_tenant.TenantId, command.Id, ct);
        if (entity is null)
            return Result<$($entityName)Dto>.Failure("$entityName not found.");

        entity.Update(
            $updateArgText,
            _user.UserId);

        if (command.IsActive && !entity.IsActive)
            entity.Enable(_user.UserId);
        else if (!command.IsActive && entity.IsActive)
            entity.Disable(_user.UserId);

        await _repo.SaveChangesAsync(ct);

        return Result<$($entityName)Dto>.Success(new $($entityName)Dto(
            entity.Id,
$($fields | ForEach-Object { "            entity.$($_.Name)" } -join ",`r`n")
        ));
    }
}
"@

$updateValidatorContent = @"
using FluentValidation;

namespace ERP.Application.Modules.$moduleName.UseCases.Update$entityName;

public sealed class Update$entityName`CommandValidator : AbstractValidator<Update$entityName`Command>
{
    public Update$entityName`CommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
$($validatorRulesUpdate -join "`r`n")
    }
}
"@

$getByIdQueryContent = @"
using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.$moduleName.DTOs;

namespace ERP.Application.Modules.$moduleName.UseCases.Get$($entityName)ById;

public sealed record Get$($entityName)ByIdQuery(Guid Id) : IRequest<Result<$($entityName)DetailDto?>>;
"@

$getByIdHandlerContent = @"
using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.$moduleName.DTOs;
using ERP.Domain.Modules.$moduleName.Interfaces;

namespace ERP.Application.Modules.$moduleName.UseCases.Get$($entityName)ById;

public sealed class Get$($entityName)ByIdQueryHandler : IRequestHandler<Get$($entityName)ByIdQuery, Result<$($entityName)DetailDto?>>
{
    private readonly I$($entityName)Repository _repo;
    private readonly ICurrentTenant _tenant;

    public Get$($entityName)ByIdQueryHandler(I$($entityName)Repository repo, ICurrentTenant tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<Result<$($entityName)DetailDto?>> Handle(Get$($entityName)ByIdQuery query, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(_tenant.TenantId, query.Id, ct);
        if (entity is null) return Result<$($entityName)DetailDto?>.Success(null);

        return Result<$($entityName)DetailDto?>.Success(new $($entityName)DetailDto(
            entity.Id,
$($fields | ForEach-Object { "            entity.$($_.Name)" } -join ",`r`n"),
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.CreatedBy,
            entity.UpdatedBy,
            entity.IsActive
        ));
    }
}
"@

$getListQueryContent = @"
using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.$moduleName.DTOs;

namespace ERP.Application.Modules.$moduleName.UseCases.Get$($entityPlural);

public sealed record Get$($entityPlural)Query(
    bool? ActiveFilter = true,
    string? Search = null) : IRequest<Result<IReadOnlyList<$($entityName)Dto>>>;
"@

$getListHandlerContent = @"
using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.$moduleName.DTOs;
using ERP.Domain.Modules.$moduleName.Interfaces;

namespace ERP.Application.Modules.$moduleName.UseCases.Get$($entityPlural);

public sealed class Get$($entityPlural)QueryHandler : IRequestHandler<Get$($entityPlural)Query, Result<IReadOnlyList<$($entityName)Dto>>>
{
    private readonly I$($entityName)Repository _repo;
    private readonly ICurrentTenant _tenant;

    public Get$($entityPlural)QueryHandler(I$($entityName)Repository repo, ICurrentTenant tenant)
    {
        _repo = repo;
        _tenant = tenant;
    }

    public async Task<Result<IReadOnlyList<$($entityName)Dto>>> Handle(Get$($entityPlural)Query query, CancellationToken ct)
    {
        var list = await _repo.GetAsync(_tenant.TenantId, query.ActiveFilter, query.Search, ct);
        var data = list.Select(entity => new $($entityName)Dto(
            entity.Id,
$($fields | ForEach-Object { "            entity.$($_.Name)" } -join ",`r`n")
        )).ToList();

        return Result<IReadOnlyList<$($entityName)Dto>>.Success(data);
    }
}
"@

$disableCommandContent = @"
using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.$moduleName.UseCases.Disable$entityName;

public sealed record Disable$entityName`Command(Guid Id) : IRequest<Result<Guid>>;
"@

$disableHandlerContent = @"
using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.$moduleName.Interfaces;

namespace ERP.Application.Modules.$moduleName.UseCases.Disable$entityName;

public sealed class Disable$entityName`CommandHandler : IRequestHandler<Disable$entityName`Command, Result<Guid>>
{
    private readonly I$($entityName)Repository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public Disable$entityName`CommandHandler(I$($entityName)Repository repo, ICurrentTenant tenant, ICurrentUser user)
    {
        _repo = repo;
        _tenant = tenant;
        _user = user;
    }

    public async Task<Result<Guid>> Handle(Disable$entityName`Command command, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(_tenant.TenantId, command.Id, ct);
        if (entity is null)
            return Result<Guid>.Failure("$entityName not found.");

        entity.Disable(_user.UserId);
        await _repo.SaveChangesAsync(ct);
        return Result<Guid>.Success(entity.Id);
    }
}
"@

$enableCommandContent = @"
using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Modules.$moduleName.UseCases.Enable$entityName;

public sealed record Enable$entityName`Command(Guid Id) : IRequest<Result<Guid>>;
"@

$enableHandlerContent = @"
using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.$moduleName.Interfaces;

namespace ERP.Application.Modules.$moduleName.UseCases.Enable$entityName;

public sealed class Enable$entityName`CommandHandler : IRequestHandler<Enable$entityName`Command, Result<Guid>>
{
    private readonly I$($entityName)Repository _repo;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;

    public Enable$entityName`CommandHandler(I$($entityName)Repository repo, ICurrentTenant tenant, ICurrentUser user)
    {
        _repo = repo;
        _tenant = tenant;
        _user = user;
    }

    public async Task<Result<Guid>> Handle(Enable$entityName`Command command, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(_tenant.TenantId, command.Id, ct);
        if (entity is null)
            return Result<Guid>.Failure("$entityName not found.");

        entity.Enable(_user.UserId);
        await _repo.SaveChangesAsync(ct);
        return Result<Guid>.Success(entity.Id);
    }
}
"@

$repositoryContent = @"
using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.$moduleName.Entities;
using ERP.Domain.Modules.$moduleName.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class $($entityName)Repository : I$($entityName)Repository
{
    private readonly ErpDbContext _context;

    public $($entityName)Repository(ErpDbContext context)
    {
        _context = context;
    }

    public Task AddAsync($entityName entity, CancellationToken ct = default)
        => _context.Set<$entityName>().AddAsync(entity, ct).AsTask();

    public async Task<IReadOnlyList<$entityName>> GetAsync(
        Guid tenantId,
        bool? activeFilter = true,
        string? search = null,
        CancellationToken ct = default)
    {
        var q = _context.Set<$entityName>().Where(x => x.TenantId == tenantId);

        if (activeFilter is true)
            q = q.Where(x => x.IsActive);
        else if (activeFilter is false)
            q = q.Where(x => !x.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            q = q.Where(x => $searchCondition);
        }

        return await q.OrderBy(x => x.CreatedAt).ToListAsync(ct);
    }

    public Task<$entityName?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.Set<$entityName>().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
"@

$configurationContent = @"
using ERP.Domain.Modules.$moduleName.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations.$moduleName;

public sealed class $($entityName)Configuration : IEntityTypeConfiguration<$entityName>
{
    public void Configure(EntityTypeBuilder<$entityName> builder)
    {
        builder.ToTable("$tableName");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
$($configLines -join "`r`n")
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => x.TenantId).HasDatabaseName("ix_${tableName}_tenant_id");
    }
}
"@

$controllerContent = @"
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Modules.$moduleName.DTOs;
using ERP.Application.Modules.$moduleName.UseCases.Create$entityName;
using ERP.Application.Modules.$moduleName.UseCases.Disable$entityName;
using ERP.Application.Modules.$moduleName.UseCases.Enable$entityName;
using ERP.Application.Modules.$moduleName.UseCases.Get$($entityName)ById;
using ERP.Application.Modules.$moduleName.UseCases.Get$($entityPlural);
using ERP.Application.Modules.$moduleName.UseCases.Update$entityName;

namespace ERP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public sealed class $($entityPlural)Controller : ControllerBase
{
    private readonly IMediator _mediator;

    public $($entityPlural)Controller(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Policy = "perm:$($entityPluralCamel).view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<$($entityName)Dto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct = default)
    {
        var activeFilter = CatalogQueryParameters.ParseActiveFilter(Request.Query);
        var search = CatalogQueryParameters.ParseSearch(Request.Query);
        var result = await _mediator.Send(new Get$($entityPlural)Query(activeFilter, search), ct);
        return this.ToOkOrBadRequest(result, "OK", () => Array.Empty<$($entityName)Dto>());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:$($entityPluralCamel).view")]
    [ProducesResponseType(typeof(ApiResponse<$($entityName)DetailDto?>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new Get$($entityName)ByIdQuery(id), ct);
        return this.ToOkOrNotFound(result);
    }

    [HttpPost]
    [Authorize(Policy = "perm:$($entityPluralCamel).create")]
    [ProducesResponseType(typeof(ApiResponse<$($entityName)Dto?>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] Create$entityName`Command command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return this.ToCreatedOrBadRequest(result, "Created");
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:$($entityPluralCamel).update")]
    [ProducesResponseType(typeof(ApiResponse<$($entityName)Dto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] Update$entityName`Command command, CancellationToken ct = default)
    {
        if (id != command.Id)
            return this.ApiBadRequest("Route id and body id must match.");

        var result = await _mediator.Send(command, ct);
        return this.ToOkOrBadRequest(result);
    }

    [HttpPatch("{id:guid}/disable")]
    [Authorize(Policy = "perm:$($entityPluralCamel).delete")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new Disable$entityName`Command(id), ct);
        return this.ToOkOrBadRequest(result, "Disabled");
    }

    [HttpPatch("{id:guid}/enable")]
    [Authorize(Policy = "perm:$($entityPluralCamel).update")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new Enable$entityName`Command(id), ct);
        return this.ToOkOrBadRequest(result, "Enabled");
    }
}
"@

$entityPath = Join-Path $domainEntitiesDir "$entityName.cs"
$repoInterfacePath = Join-Path $domainInterfacesDir "I$entityName`Repository.cs"
$dtoPath = Join-Path $appDtosDir "$entityName`Dtos.cs"
$repoPath = Join-Path $infraRepoDir "$entityName`Repository.cs"
$configPath = Join-Path $infraConfigDir "$entityName`Configuration.cs"
$controllerPath = Join-Path $apiControllersDir "$entityPlural`Controller.cs"

Write-File -Path $entityPath -Content $entityContent
Write-File -Path $repoInterfacePath -Content $repoInterfaceContent
Write-File -Path $dtoPath -Content $dtoContent
Write-File -Path (Join-Path $createCommandDir "Create$entityName`Command.cs") -Content $createCommandContent
Write-File -Path (Join-Path $createCommandDir "Create$entityName`CommandHandler.cs") -Content $createHandlerContent
Write-File -Path (Join-Path $createCommandDir "Create$entityName`CommandValidator.cs") -Content $createValidatorContent
Write-File -Path (Join-Path $updateCommandDir "Update$entityName`Command.cs") -Content $updateCommandContent
Write-File -Path (Join-Path $updateCommandDir "Update$entityName`CommandHandler.cs") -Content $updateHandlerContent
Write-File -Path (Join-Path $updateCommandDir "Update$entityName`CommandValidator.cs") -Content $updateValidatorContent
Write-File -Path (Join-Path $getByIdDir "Get$entityName`ByIdQuery.cs") -Content $getByIdQueryContent
Write-File -Path (Join-Path $getByIdDir "Get$entityName`ByIdQueryHandler.cs") -Content $getByIdHandlerContent
Write-File -Path (Join-Path $getListDir "Get$entityPlural`Query.cs") -Content $getListQueryContent
Write-File -Path (Join-Path $getListDir "Get$entityPlural`QueryHandler.cs") -Content $getListHandlerContent
Write-File -Path (Join-Path $disableDir "Disable$entityName`Command.cs") -Content $disableCommandContent
Write-File -Path (Join-Path $disableDir "Disable$entityName`CommandHandler.cs") -Content $disableHandlerContent
Write-File -Path (Join-Path $enableDir "Enable$entityName`Command.cs") -Content $enableCommandContent
Write-File -Path (Join-Path $enableDir "Enable$entityName`CommandHandler.cs") -Content $enableHandlerContent
Write-File -Path $repoPath -Content $repositoryContent
Write-File -Path $configPath -Content $configurationContent
if ($generateController) {
    Write-File -Path $controllerPath -Content $controllerContent
}

$infraDiPath = Join-Path $srcRoot "ERP.Infrastructure/DependencyInjection.cs"
$dbContextPath = Join-Path $srcRoot "ERP.Infrastructure/Persistence/ErpDbContext.cs"

Ensure-LineInFile -Path $infraDiPath -Line "using ERP.Domain.Modules.$moduleName.Interfaces;"
Insert-Before-Marker `
    -Path $infraDiPath `
    -Marker "        return services;" `
    -InsertText "        services.AddScoped<I$($entityName)Repository, $($entityName)Repository>();`r`n" `
    -DetectDuplicate "services.AddScoped<I$($entityName)Repository, $($entityName)Repository>();"

Ensure-LineInFile -Path $dbContextPath -Line "using ERP.Domain.Modules.$moduleName.Entities;"
Insert-Before-Marker `
    -Path $dbContextPath `
    -Marker "    /// <summary>" `
    -InsertText "    public DbSet<$entityName> $entityPlural => Set<$entityName>();`r`n`r`n" `
    -DetectDuplicate "public DbSet<$entityName> $entityPlural => Set<$entityName>();"

Write-Host ""
Write-Host "Scaffold completado." -ForegroundColor Green
Write-Host "Siguientes pasos recomendados:" -ForegroundColor Green
Write-Host "1) Revisar políticas/permisos del controller generado."
Write-Host "2) Ejecutar: dotnet build backend/src/ERP.API/ERP.API.csproj"
Write-Host "3) Crear migración EF si aplica (tabla nueva/campos):"
Write-Host "   dotnet ef migrations add Add${entityName}Module --project backend/src/ERP.Infrastructure --startup-project backend/src/ERP.API"
Write-Host ""

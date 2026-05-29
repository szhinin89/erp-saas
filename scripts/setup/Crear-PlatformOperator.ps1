<#
.SYNOPSIS
    First-Run Platform Provisioning Script — ERP SaaS.

.DESCRIPTION
    Aprovisiona la plataforma SaaS desde cero en un único flujo interactivo:

      ETAPA 1  Conexión y estado del sistema
      ETAPA 2  Propietario de la plataforma (Subscriber + Company interna)
      ETAPA 3  Platform Operator User (user_type=Platform)
      ETAPA 4  Verificación de login y JWT
      ETAPA 5  Resumen final

    IDEMPOTENCIA:
      Si la plataforma ya está provisionada, el script informa y sale de forma segura.
      Si solo falta el Platform Operator, continúa desde la etapa 3.

    VARIABLES DE ENTORNO (opcionales para CI/CD):
      ERP_API_URL               URL base de la API (ej. http://localhost:5003)
      ERP_PLATFORM_SETUP_TOKEN  Token efímero de first-run (consola del API)
      ERP_PLATFORM_EMAIL        Email del operador platform
      ERP_PLATFORM_PASSWORD     Contraseña del operador platform
      ERP_OWNER_PLATFORM_NAME   Nombre de la plataforma (ej. "Acme SaaS")
      ERP_OWNER_TAXID           RUC / TaxId de la empresa operadora
      ERP_OWNER_LEGAL_NAME      Razón social
      ERP_OWNER_ADDRESS         Dirección principal
      ERP_OWNER_EMAIL           Email de billing/contacto

.EXAMPLE
    .\scripts\setup\Crear-PlatformOperator.ps1

.EXAMPLE
    $env:ERP_API_URL = "https://api.midominio.com"
    $env:ERP_PLATFORM_SETUP_TOKEN = "base64token=="
    .\scripts\setup\Crear-PlatformOperator.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$script:ApiBase    = $null
$script:SetupToken = $null
$script:StageNum   = 0

# ─── UTF-8 ────────────────────────────────────────────────────────────────────
try {
    if ($PSVersionTable.PSVersion.Major -ge 6) {
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        $OutputEncoding = [Console]::OutputEncoding
    } else {
        chcp 65001 | Out-Null
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
    }
} catch { }

# ─── Helpers consola ─────────────────────────────────────────────────────────
function Write-Stage($title) {
    $script:StageNum++
    $line = "─" * 68
    Write-Host ""
    Write-Host $line                              -ForegroundColor DarkGray
    Write-Host "  ETAPA $($script:StageNum) · $title" -ForegroundColor White
    Write-Host $line                              -ForegroundColor DarkGray
}
function Write-Info($m)    { Write-Host "  ℹ  $m" -ForegroundColor Cyan    }
function Write-Ok($m)      { Write-Host "  ✓  $m" -ForegroundColor Green   }
function Write-Warn($m)    { Write-Host "  ⚠  $m" -ForegroundColor Yellow  }
function Write-Err($m)     { Write-Host "  ✗  $m" -ForegroundColor Red     }
function Write-Detail($m)  { Write-Host "     $m" -ForegroundColor DarkCyan }

function Prompt-Line($label, $default = "") {
    $display = if ($default) { "$label [$default]" } else { $label }
    $val = (Read-Host "  → $display").Trim()
    if ([string]::IsNullOrWhiteSpace($val) -and $default) { return $default }
    return $val
}

function Prompt-Secret($label) {
    $sec = Read-Host "  → $label" -AsSecureString
    if ($sec.Length -eq 0) { return "" }
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
    try   { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) | Out-Null }
}

# ─── HTTP helpers ─────────────────────────────────────────────────────────────
function Invoke-Api($method, $path, $body = $null) {
    $uri     = "$($script:ApiBase.TrimEnd('/'))$path"
    $headers = @{ "Content-Type" = "application/json; charset=utf-8" }
    $params  = @{ Uri = $uri; Method = $method; Headers = $headers; TimeoutSec = 30 }
    if ($body) { $params["Body"] = ($body | ConvertTo-Json -Depth 10 -Compress) }
    try {
        return Invoke-RestMethod @params
    } catch {
        $raw = $null
        if ($_.ErrorDetails -and $_.ErrorDetails.Message) {
            $raw = $_.ErrorDetails.Message
        } elseif ($_.Exception.Response) {
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                $reader = New-Object System.IO.StreamReader($stream)
                $raw = $reader.ReadToEnd(); $reader.Dispose()
            } catch { }
        }
        $result = [PSCustomObject]@{ Succeeded=$false; RawBody=$raw; ExMsg=$_.Exception.Message }
        if ($raw) {
            try { $result | Add-Member -NotePropertyName Parsed -NotePropertyValue ($raw | ConvertFrom-Json) } catch { }
        }
        return $result
    }
}

function isOk($r) {
    if ($null -eq $r) { return $false }
    if ($r.PSObject.Properties["Succeeded"] -and $r.Succeeded -eq $false) { return $false }
    $ok = $r.success; if ($null -eq $ok) { $ok = $r.Success }
    return [bool]$ok
}
function getObj($r)   { $o=$r.responseObject; if($null -eq $o){$o=$r.ResponseObject}; return $o }
function getMsg($r)   {
    $m=$r.message; if([string]::IsNullOrWhiteSpace($m)){$m=$r.Message}
    if([string]::IsNullOrWhiteSpace($m) -and $r.Parsed){$m=$r.Parsed.message}
    return $m
}
function getToken($r) {
    $obj=getObj $r; if(-not $obj){return $null}
    $t=$obj.token; if([string]::IsNullOrWhiteSpace($t)){$t=$obj.Token}; return $t
}

# ─────────────────────────────────────────────────────────────────────────────
# BANNER
# ─────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════════╗" -ForegroundColor DarkCyan
Write-Host "║    FIRST-RUN PLATFORM PROVISIONING SCRIPT — ERP SaaS           ║" -ForegroundColor DarkCyan
Write-Host "║    Aprovisiona la plataforma SaaS desde cero en un solo flujo   ║" -ForegroundColor DarkCyan
Write-Host "╚══════════════════════════════════════════════════════════════════╝" -ForegroundColor DarkCyan

# ═════════════════════════════════════════════════════════════════════════════
# ETAPA 1 · CONEXIÓN Y ESTADO
# ═════════════════════════════════════════════════════════════════════════════
Write-Stage "Conexión y estado de la plataforma"

$defaultUrl      = if ($env:ERP_API_URL) { $env:ERP_API_URL } else { "http://localhost:5003" }
Write-Info "URL de la API (vacío = $defaultUrl):"
$inputUrl        = Read-Host "  → URL"
$script:ApiBase  = if ([string]::IsNullOrWhiteSpace($inputUrl)) { $defaultUrl } else { $inputUrl.TrimEnd('/') }

Write-Info "Probando conexión a $($script:ApiBase) ..."
$reachable = $false
foreach ($path in @("/health/live", "/swagger/index.html")) {
    try {
        $r = Invoke-WebRequest -Uri "$($script:ApiBase)$path" -Method Get -TimeoutSec 8 -UseBasicParsing
        if ($r.StatusCode -eq 200) { $reachable = $true; break }
    } catch { }
}
if (-not $reachable) {
    Write-Err "No se pudo conectar a $($script:ApiBase)"
    Write-Warn "¿Está corriendo el API? → dotnet run --project backend/src/ERP.API"
    exit 1
}
Write-Ok "API accesible en $($script:ApiBase)"

Write-Info "Consultando estado de la plataforma..."
$sr = Invoke-Api "GET" "/api/setup/platform/status"
$st = getObj $sr
if ($null -eq $st) {
    Write-Warn "No se pudo consultar el estado. Asumiendo first-run pendiente."
    $st = [PSCustomObject]@{ isFullyProvisioned=$false; hasInternalPlatformOwner=$false; hasPlatformOperator=$false }
}

Write-Host ""
Write-Detail "  Propietario de plataforma : $(if ($st.hasInternalPlatformOwner){'✓ Configurado'}else{'✗ Pendiente'})"
Write-Detail "  Operador platform         : $(if ($st.hasPlatformOperator){'✓ Creado'}else{'✗ Pendiente'})"
Write-Detail "  Estado general            : $(if ($st.isFullyProvisioned){'✓ Completamente provisionado'}else{'⚠ First-run pendiente'})"

if ($st.isFullyProvisioned) {
    Write-Host ""
    Write-Ok "La plataforma ya está completamente provisionada. No hay nada que hacer."
    Write-Info "Para restablecer (Development): POST $($script:ApiBase)/api/dev/reset-first-run"
    exit 0
}

# ═════════════════════════════════════════════════════════════════════════════
# TOKEN FIRST-RUN
# ═════════════════════════════════════════════════════════════════════════════
Write-Stage "Token de first-run"

Write-Info "El token se muestra en la consola del proceso ERP.API al arrancar."
Write-Info "En Development: POST $($script:ApiBase)/api/dev/reset-first-run devuelve uno nuevo."
Write-Host ""

$envToken = $env:ERP_PLATFORM_SETUP_TOKEN
if ([string]::IsNullOrWhiteSpace($envToken)) {
    Read-Host "  Presiona ENTER cuando tengas el token listo" | Out-Null
    $script:SetupToken = Prompt-Secret "Pega el token de first-run"
} else {
    Write-Ok "ERP_PLATFORM_SETUP_TOKEN detectado."
    $manual = Prompt-Line "Pega manualmente o ENTER para usar la variable de entorno"
    $script:SetupToken = if ([string]::IsNullOrWhiteSpace($manual)) { $envToken } else { $manual }
}

if ([string]::IsNullOrWhiteSpace($script:SetupToken)) {
    Write-Err "El token de first-run es obligatorio."
    exit 1
}
Write-Ok "Token de first-run recibido."

# ═════════════════════════════════════════════════════════════════════════════
# ETAPA 2 · PROPIETARIO DE LA PLATAFORMA
# ═════════════════════════════════════════════════════════════════════════════
if ($st.hasInternalPlatformOwner) {
    Write-Stage "Propietario de plataforma (ya configurado — omitiendo)"
    Write-Ok "El propietario de la plataforma ya fue configurado en un first-run anterior."
} else {
    Write-Stage "Configurar propietario de la plataforma (Subscriber + Company interna)"
    Write-Info "Estos datos identifican a TU empresa como operadora del SaaS."
    Write-Info "Son independientes de los datos de los tenants ERP que administrarás."
    Write-Host ""

    $ownerName    = if ($env:ERP_OWNER_PLATFORM_NAME) { Prompt-Line "Nombre de la plataforma" $env:ERP_OWNER_PLATFORM_NAME } else { Prompt-Line "Nombre de la plataforma (ej: Acme SaaS)" }
    $ownerTaxId   = if ($env:ERP_OWNER_TAXID)         { Prompt-Line "RUC / TaxId" $env:ERP_OWNER_TAXID }                   else { Prompt-Line "RUC / TaxId de tu empresa (13 dígitos)" }
    $ownerLegal   = if ($env:ERP_OWNER_LEGAL_NAME)     { Prompt-Line "Razón social" $env:ERP_OWNER_LEGAL_NAME }             else { Prompt-Line "Razón social (ej: Acme S.A.S.)" }
    $ownerTrade   = Prompt-Line "Nombre comercial (ENTER para omitir)"
    $ownerAddr    = if ($env:ERP_OWNER_ADDRESS)         { Prompt-Line "Dirección principal" $env:ERP_OWNER_ADDRESS }        else { Prompt-Line "Dirección principal" }
    $ownerEmail   = if ($env:ERP_OWNER_EMAIL)           { Prompt-Line "Email de contacto" $env:ERP_OWNER_EMAIL }            else { Prompt-Line "Email de contacto (ej: billing@tuempresa.com)" }
    $ownerTz      = Prompt-Line "Zona horaria" "America/Guayaquil"

    Write-Host ""
    Write-Info "Confirmación de datos:"
    Write-Detail "  Nombre plataforma : $ownerName"
    Write-Detail "  RUC               : $ownerTaxId"
    Write-Detail "  Razón social      : $ownerLegal"
    if ($ownerTrade) { Write-Detail "  Nombre comercial  : $ownerTrade" }
    Write-Detail "  Dirección         : $ownerAddr"
    Write-Detail "  Email             : $ownerEmail"
    Write-Detail "  Timezone          : $ownerTz"
    Write-Host ""

    $confirm = Read-Host "  → ¿Confirmar? [S/n]"
    if ($confirm -match "^[Nn]") { Write-Warn "Operación cancelada."; exit 0 }

    Write-Info "Provisionando propietario de plataforma..."
    $ownerBody = @{
        setupToken        = $script:SetupToken
        platformName      = $ownerName
        taxId             = $ownerTaxId
        legalName         = $ownerLegal
        mainAddress       = $ownerAddr
        timezone          = $ownerTz
        email             = $ownerEmail
        tradeName         = if ($ownerTrade) { $ownerTrade } else { $null }
        preferredLanguage = "es"
    }
    $ownerRes = Invoke-Api "POST" "/api/setup/platform-owner" $ownerBody

    if (-not (isOk $ownerRes)) {
        Write-Err "Error al crear el propietario de plataforma."
        $msg = getMsg $ownerRes
        if ($msg) { Write-Err "Detalle: $msg" }
        if ($ownerRes.RawBody) { Write-Err "Respuesta raw: $($ownerRes.RawBody)" }
        Write-Warn "Comprueba el token first-run y los datos ingresados."
        exit 1
    }

    $ownerObj = getObj $ownerRes
    Write-Ok "Propietario de plataforma provisionado."
    if ($ownerObj) {
        Write-Detail "  Subscriber ID : $($ownerObj.subscriberId)"
        Write-Detail "  Company ID    : $($ownerObj.companyId)"
        Write-Detail "  Slug          : $($ownerObj.slug)"
    }
}

# ═════════════════════════════════════════════════════════════════════════════
# ETAPA 3 · PLATFORM OPERATOR USER
# ═════════════════════════════════════════════════════════════════════════════
$opEmail     = $null
$opPassword  = $null
$jwtToken    = $null

if ($st.hasPlatformOperator) {
    Write-Stage "Platform Operator User (ya creado — omitiendo)"
    Write-Ok "Ya existe un operador platform en el sistema."
} else {
    Write-Stage "Crear Platform Operator User"
    Write-Info "Administra tenants, planes y configuración global del SaaS."
    Write-Info "NO es un usuario de ningún tenant ERP. Tiene contexto platform exclusivo."
    Write-Host ""

    # Email
    if ($env:ERP_PLATFORM_EMAIL) {
        $opEmail = $env:ERP_PLATFORM_EMAIL.Trim().ToLower()
        Write-Info "Email: $opEmail (desde ERP_PLATFORM_EMAIL)"
    } else {
        do {
            $opEmail = (Prompt-Line "Email del operador platform").ToLower()
            if ($opEmail -notmatch '^[^@]+@[^@]+\.[^@]+$') { Write-Err "Formato de email inválido." }
        } while ($opEmail -notmatch '^[^@]+@[^@]+\.[^@]+$')
    }

    # Nombre
    $fullName  = Prompt-Line "Nombre completo (ej: Ana García)"
    $parts     = $fullName.Trim() -split '\s+', 2
    $firstName = $parts[0]
    $lastName  = if ($parts.Length -gt 1) { $parts[1] } else { "" }
    if ([string]::IsNullOrWhiteSpace($firstName) -or [string]::IsNullOrWhiteSpace($lastName)) {
        Write-Err "Se requieren nombre Y apellido (mínimo dos palabras)."
        exit 1
    }

    # Contraseña
    if ($env:ERP_PLATFORM_PASSWORD) {
        $opPassword = $env:ERP_PLATFORM_PASSWORD
        Write-Info "Contraseña recibida desde ERP_PLATFORM_PASSWORD."
    } else {
        do {
            $opPassword = Prompt-Secret "Contraseña (mínimo 10 caracteres)"
            if ($opPassword.Length -lt 10) { Write-Err "Mínimo 10 caracteres." }
        } while ($opPassword.Length -lt 10)
        $confirm2 = Prompt-Secret "Confirmar contraseña"
        if ($opPassword -ne $confirm2) {
            Write-Err "Las contraseñas no coinciden."
            $opPassword = $null; $confirm2 = $null; [System.GC]::Collect(); exit 1
        }
        $confirm2 = $null
    }

    Write-Info "Creando Platform Operator..."
    $opBody = @{
        setupToken = $script:SetupToken
        firstName  = $firstName
        lastName   = $lastName
        email      = $opEmail
        password   = $opPassword
    }
    $opRes = Invoke-Api "POST" "/api/setup/platform-operator" $opBody

    if (-not (isOk $opRes)) {
        Write-Err "Error al crear el operador platform."
        $msg = getMsg $opRes
        if ($msg) { Write-Err "Detalle: $msg" }
        if ($opRes.RawBody) { Write-Err "Respuesta: $($opRes.RawBody)" }
        Write-Host ""
        Write-Warn "Causas comunes:"
        Write-Warn "  1) Token expirado — obtén uno nuevo con /api/dev/reset-first-run"
        Write-Warn "  2) Ya existe un operador platform"
        Write-Warn "  3) Email ya registrado"
        exit 1
    }

    $jwtToken = getToken $opRes
    Write-Ok "Operador platform creado: $opEmail"
    Write-Ok "user_type=Platform · platform_role=PlatformOperator · subscriber_id=Guid.Empty"
}

# ═════════════════════════════════════════════════════════════════════════════
# ETAPA 4 · VERIFICACIÓN DE LOGIN
# ═════════════════════════════════════════════════════════════════════════════
Write-Stage "Verificación de login platform"

if (-not [string]::IsNullOrWhiteSpace($jwtToken)) {
    Write-Ok "JWT recibido en la respuesta del setup — login verificado implícitamente."
} else {
    if ([string]::IsNullOrWhiteSpace($opEmail))    { $opEmail    = Prompt-Line "Email del operador platform" }
    if ([string]::IsNullOrWhiteSpace($opPassword)) { $opPassword = Prompt-Secret "Contraseña del operador platform" }

    Write-Info "Verificando login en /api/platform/auth/login ..."
    $loginRes = Invoke-Api "POST" "/api/platform/auth/login" @{ email=$opEmail; password=$opPassword }
    $jwtToken = getToken $loginRes

    if ((isOk $loginRes) -and -not [string]::IsNullOrWhiteSpace($jwtToken)) {
        Write-Ok "Login platform verificado correctamente."
    } else {
        Write-Warn "No se pudo verificar login. Comprueba Deployment:PlatformPanelEnabled=true"
        $msg = getMsg $loginRes; if ($msg) { Write-Warn "Detalle: $msg" }
    }
}

if ($jwtToken) {
    $preview = $jwtToken.Substring(0, [Math]::Min(60, $jwtToken.Length))
    Write-Detail "JWT preview: $preview..."
}

# ─── Limpiar secretos ─────────────────────────────────────────────────────────
$script:SetupToken = $null; $opPassword = $null; $jwtToken = $null
[System.GC]::Collect()

# ═════════════════════════════════════════════════════════════════════════════
# ETAPA 5 · RESUMEN FINAL
# ═════════════════════════════════════════════════════════════════════════════
Write-Stage "Resumen final del aprovisionamiento"

$finalRes = Invoke-Api "GET" "/api/setup/platform/status"
$final    = getObj $finalRes
$ownerOk  = if ($final) { $final.hasInternalPlatformOwner } else { $true }
$opOk     = if ($final) { $final.hasPlatformOperator      } else { $true }
$allOk    = $ownerOk -and $opOk

Write-Host ""
Write-Host "  ┌─────────────────────────────────────────────────────────────┐" -ForegroundColor DarkCyan
Write-Host "  │  ESTADO FINAL DEL SISTEMA                                   │" -ForegroundColor DarkCyan
Write-Host "  ├─────────────────────────────────────────────────────────────┤" -ForegroundColor DarkCyan
$ownerStatus = if ($ownerOk) {"✓ Configurado  "} else {"✗ Pendiente    "}
$opStatus    = if ($opOk)    {"✓ Creado       "} else {"✗ Pendiente    "}
$sysStatus   = if ($allOk)   {"✓ LISTO PARA PRODUCCION"} else {"⚠ INCOMPLETO           "}
Write-Host "  │  Propietario de plataforma : $ownerStatus                   │" -ForegroundColor $(if ($ownerOk) {"Green"} else {"Red"})
Write-Host "  │  Operador platform         : $opStatus                   │" -ForegroundColor $(if ($opOk) {"Green"} else {"Red"})
Write-Host "  │  Estado general            : $sysStatus     │" -ForegroundColor $(if ($allOk) {"Green"} else {"Yellow"})
Write-Host "  └─────────────────────────────────────────────────────────────┘" -ForegroundColor DarkCyan
Write-Host ""

if ($allOk) {
    Write-Ok "Plataforma SaaS aprovisionada y lista para operar."
    Write-Host ""
    Write-Info "Próximos pasos:"
    Write-Detail "  1. Abre el panel platform en el frontend"
    Write-Detail "  2. Inicia sesión con el email/contraseña del operador platform"
    Write-Detail "  3. Crea el primer tenant: Panel → Suscriptores → Nuevo Suscriptor"
    Write-Detail "  4. El tenant configurará su empresa ERP en el onboarding wizard"
    Write-Host ""
    Write-Warn "El token first-run ya fue consumido y no puede reutilizarse."
    exit 0
} else {
    Write-Warn "El aprovisionamiento quedó incompleto. Revisa los errores anteriores."
    Write-Info "El script es idempotente — puedes volver a ejecutarlo."
    exit 2
}

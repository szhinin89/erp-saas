<#
.SYNOPSIS
    First-Run Platform Provisioning Script — ERP SaaS.

.DESCRIPTION
    Aprovisiona la plataforma SaaS desde cero en un único flujo:

      ETAPA 1  Conexión y estado del sistema
      ETAPA 2  Token first-run
      ETAPA 3  Propietario de plataforma (Subscriber + Company interna)
      ETAPA 4  Platform Operator User
      ETAPA 5  Verificación de login y JWT
      ETAPA 6  Resumen final

    IDEMPOTENCIA:
      Si la plataforma ya está provisionada, el script informa y sale de forma segura.
      Si solo falta el Platform Operator, continúa desde la etapa 4.

    MODO NO INTERACTIVO (CI/CD):
      Establece ERP_SETUP_NON_INTERACTIVE=true y todas las variables obligatorias.
      El script fallará inmediatamente si falta alguna variable requerida.
      No habrá prompts de confirmación ni Read-Host.

    VARIABLES DE ENTORNO:
      ERP_API_URL                URL base de la API (ej. http://localhost:5003)
      ERP_PLATFORM_SETUP_TOKEN   Token efímero de first-run (consola del API)
      ERP_PLATFORM_EMAIL         Email del operador platform
      ERP_PLATFORM_PASSWORD      Contraseña del operador platform
      ERP_OWNER_PLATFORM_NAME    Nombre de la plataforma (ej. "Acme SaaS")
      ERP_OWNER_TAXID            RUC / TaxId de la empresa operadora
      ERP_OWNER_LEGAL_NAME       Razón social
      ERP_OWNER_TRADE_NAME       Nombre comercial (opcional)
      ERP_OWNER_ADDRESS          Dirección principal
      ERP_OWNER_EMAIL            Email de billing/contacto
      ERP_OWNER_TIMEZONE         Zona horaria (default: America/Guayaquil)
      ERP_OPERATOR_FIRSTNAME     Nombre del operador platform
      ERP_OPERATOR_LASTNAME      Apellido del operador platform
      ERP_SETUP_NON_INTERACTIVE  Establece a "true" para modo CI/CD sin prompts

.EXAMPLE
    # Interactive (development)
    .\scripts\setup\Crear-PlatformOperator.ps1

.EXAMPLE
    # Non-interactive (CI/CD)
    $env:ERP_SETUP_NON_INTERACTIVE = "true"
    $env:ERP_API_URL               = "https://api.midominio.com"
    $env:ERP_PLATFORM_SETUP_TOKEN  = "base64token=="
    $env:ERP_OWNER_PLATFORM_NAME   = "Acme SaaS"
    $env:ERP_OWNER_TAXID           = "0999999999001"
    $env:ERP_OWNER_LEGAL_NAME      = "Acme S.A.S."
    $env:ERP_OWNER_ADDRESS         = "Av. Principal 001"
    $env:ERP_OWNER_EMAIL           = "billing@acme.com"
    $env:ERP_PLATFORM_EMAIL        = "admin@acme.com"
    $env:ERP_PLATFORM_PASSWORD     = "Str0ng!Pass#2026"
    $env:ERP_OPERATOR_FIRSTNAME    = "Admin"
    $env:ERP_OPERATOR_LASTNAME     = "Platform"
    .\scripts\setup\Crear-PlatformOperator.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$script:ApiBase        = $null
$script:SetupToken     = $null
$script:StageNum       = 0
$NonInteractive        = ($env:ERP_SETUP_NON_INTERACTIVE -eq "true")

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
    Write-Host $line                                   -ForegroundColor DarkGray
    Write-Host "  ETAPA $($script:StageNum) · $title" -ForegroundColor White
    Write-Host $line                                   -ForegroundColor DarkGray
}
function Write-Info($m)   { Write-Host "  i  $m" -ForegroundColor Cyan    }
function Write-Ok($m)     { Write-Host "  +  $m" -ForegroundColor Green   }
function Write-Warn($m)   { Write-Host "  !  $m" -ForegroundColor Yellow  }
function Write-Err($m)    { Write-Host "  X  $m" -ForegroundColor Red     }
function Write-Detail($m) { Write-Host "     $m" -ForegroundColor DarkCyan }

function Get-RequiredEnvVar([string]$name) {
    $val = [System.Environment]::GetEnvironmentVariable($name)
    if ([string]::IsNullOrWhiteSpace($val)) {
        Write-Err "Variable de entorno requerida en modo no-interactivo: $name"
        exit 1
    }
    return $val.Trim()
}

function Prompt-Line($label, $default = "") {
    if ($NonInteractive) {
        Write-Err "Se esperaba prompt '$label' en modo no-interactivo. Use variable de entorno."
        exit 1
    }
    $display = if ($default) { "$label [$default]" } else { $label }
    $val = (Read-Host "  -> $display").Trim()
    if ([string]::IsNullOrWhiteSpace($val) -and $default) { return $default }
    return $val
}

function Prompt-Secret($label) {
    if ($NonInteractive) {
        Write-Err "Se esperaba entrada de secreto '$label' en modo no-interactivo. Use variable de entorno."
        exit 1
    }
    $sec = Read-Host "  -> $label" -AsSecureString
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
if ($NonInteractive) {
Write-Host "║    Modo: NO INTERACTIVO (CI/CD)                                 ║" -ForegroundColor Yellow
} else {
Write-Host "║    Modo: INTERACTIVO (Development)                              ║" -ForegroundColor Green
}
Write-Host "╚══════════════════════════════════════════════════════════════════╝" -ForegroundColor DarkCyan

# ═════════════════════════════════════════════════════════════════════════════
# ETAPA 1 · CONEXIÓN Y ESTADO
# ═════════════════════════════════════════════════════════════════════════════
Write-Stage "Conexión y estado de la plataforma"

$defaultUrl = if ($env:ERP_API_URL) { $env:ERP_API_URL } else { "http://localhost:5003" }

if ($NonInteractive) {
    $script:ApiBase = $defaultUrl.TrimEnd('/')
    Write-Info "URL API (ERP_API_URL): $($script:ApiBase)"
} else {
    Write-Info "URL de la API (vacío = $defaultUrl):"
    $inputUrl       = Read-Host "  -> URL"
    $script:ApiBase = if ([string]::IsNullOrWhiteSpace($inputUrl)) { $defaultUrl } else { $inputUrl.TrimEnd('/') }
}

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
    Write-Warn "¿Está corriendo el API? -> dotnet run --project backend/src/ERP.API"
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
Write-Detail "  Propietario de plataforma : $(if ($st.hasInternalPlatformOwner){'+ Configurado'}else{'X Pendiente'})"
Write-Detail "  Operador platform         : $(if ($st.hasPlatformOperator){'+ Creado'}else{'X Pendiente'})"
Write-Detail "  Estado general            : $(if ($st.isFullyProvisioned){'+ Completamente provisionado'}else{'! First-run pendiente'})"

# Show provisioning lock state (informational)
$lockRes = Invoke-Api "GET" "/api/setup/platform/provisioning-status"
$lockObj = if (isOk $lockRes) { getObj $lockRes } else { $null }
if ($lockObj -and $lockObj.lock -and $lockObj.lock.isLocked) {
    Write-Warn "AVISO: El lock de provisioning está activo (retenido por: $($lockObj.lock.lockedByInstance), expira: $($lockObj.lock.expiresAtUtc))"
    if ($NonInteractive) {
        Write-Err "Lock de provisioning activo en modo no-interactivo. Espere o limpie con /api/dev/reset-platform-provisioning."
        exit 1
    }
}

if ($st.isFullyProvisioned) {
    Write-Host ""
    Write-Ok "La plataforma ya está completamente provisionada. No hay nada que hacer."
    Write-Info "Para restablecer (Development): POST $($script:ApiBase)/api/dev/reset-platform-provisioning"
    exit 0
}

# ═════════════════════════════════════════════════════════════════════════════
# ETAPA 2 · TOKEN FIRST-RUN
# ═════════════════════════════════════════════════════════════════════════════
Write-Stage "Token de first-run"

if ($NonInteractive) {
    $script:SetupToken = Get-RequiredEnvVar "ERP_PLATFORM_SETUP_TOKEN"
    Write-Ok "ERP_PLATFORM_SETUP_TOKEN recibido desde variable de entorno."
} else {
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
}

if ([string]::IsNullOrWhiteSpace($script:SetupToken)) {
    Write-Err "El token de first-run es obligatorio."
    exit 1
}
Write-Ok "Token de first-run recibido."

# ═════════════════════════════════════════════════════════════════════════════
# ETAPA 3 · PROPIETARIO DE LA PLATAFORMA
# ═════════════════════════════════════════════════════════════════════════════
if ($st.hasInternalPlatformOwner) {
    Write-Stage "Propietario de plataforma (ya configurado — omitiendo)"
    Write-Ok "El propietario de la plataforma ya fue configurado en un first-run anterior."
} else {
    Write-Stage "Configurar propietario de la plataforma (Subscriber + Company interna)"

    if ($NonInteractive) {
        Write-Info "Leyendo datos del propietario desde variables de entorno..."
        $ownerName  = Get-RequiredEnvVar "ERP_OWNER_PLATFORM_NAME"
        $ownerTaxId = Get-RequiredEnvVar "ERP_OWNER_TAXID"
        $ownerLegal = Get-RequiredEnvVar "ERP_OWNER_LEGAL_NAME"
        $ownerTrade = $env:ERP_OWNER_TRADE_NAME
        $ownerAddr  = Get-RequiredEnvVar "ERP_OWNER_ADDRESS"
        $ownerEmail = Get-RequiredEnvVar "ERP_OWNER_EMAIL"
        $ownerTz    = if ($env:ERP_OWNER_TIMEZONE) { $env:ERP_OWNER_TIMEZONE } else { "America/Guayaquil" }
    } else {
        Write-Info "Estos datos identifican a TU empresa como operadora del SaaS."
        Write-Info "Son independientes de los datos de los tenants ERP que administrarás."
        Write-Host ""

        $ownerName  = if ($env:ERP_OWNER_PLATFORM_NAME) { Prompt-Line "Nombre de la plataforma"     $env:ERP_OWNER_PLATFORM_NAME } else { Prompt-Line "Nombre de la plataforma (ej: Acme SaaS)" }
        $ownerTaxId = if ($env:ERP_OWNER_TAXID)         { Prompt-Line "RUC / TaxId"                 $env:ERP_OWNER_TAXID         } else { Prompt-Line "RUC / TaxId de tu empresa (13 digitos)" }
        $ownerLegal = if ($env:ERP_OWNER_LEGAL_NAME)     { Prompt-Line "Razon social"                $env:ERP_OWNER_LEGAL_NAME   } else { Prompt-Line "Razon social (ej: Acme S.A.S.)" }
        $ownerTrade = Prompt-Line "Nombre comercial (ENTER para omitir)"
        $ownerAddr  = if ($env:ERP_OWNER_ADDRESS)         { Prompt-Line "Direccion principal"         $env:ERP_OWNER_ADDRESS      } else { Prompt-Line "Direccion principal" }
        $ownerEmail = if ($env:ERP_OWNER_EMAIL)           { Prompt-Line "Email de contacto"           $env:ERP_OWNER_EMAIL        } else { Prompt-Line "Email de contacto (ej: billing@tuempresa.com)" }
        $ownerTz    = Prompt-Line "Zona horaria" "America/Guayaquil"

        Write-Host ""
        Write-Info "Confirmacion de datos:"
        Write-Detail "  Nombre plataforma : $ownerName"
        Write-Detail "  RUC               : $ownerTaxId"
        Write-Detail "  Razon social      : $ownerLegal"
        if ($ownerTrade) { Write-Detail "  Nombre comercial  : $ownerTrade" }
        Write-Detail "  Direccion         : $ownerAddr"
        Write-Detail "  Email             : $ownerEmail"
        Write-Detail "  Timezone          : $ownerTz"
        Write-Host ""

        $confirm = Read-Host "  -> Confirmar? [S/n]"
        if ($confirm -match "^[Nn]") { Write-Warn "Operacion cancelada."; exit 0 }
    }

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
# ETAPA 4 · PLATFORM OPERATOR USER
# ═════════════════════════════════════════════════════════════════════════════
$opEmail     = $null
$opPassword  = $null
$jwtToken    = $null

if ($st.hasPlatformOperator) {
    Write-Stage "Platform Operator User (ya creado — omitiendo)"
    Write-Ok "Ya existe un operador platform en el sistema."
} else {
    Write-Stage "Crear Platform Operator User"

    if ($NonInteractive) {
        Write-Info "Leyendo credenciales del operador desde variables de entorno..."
        $opEmail     = (Get-RequiredEnvVar "ERP_PLATFORM_EMAIL").ToLower()
        $opPassword  = Get-RequiredEnvVar "ERP_PLATFORM_PASSWORD"
        $firstName   = Get-RequiredEnvVar "ERP_OPERATOR_FIRSTNAME"
        $lastName    = Get-RequiredEnvVar "ERP_OPERATOR_LASTNAME"

        if ($opPassword.Length -lt 10) {
            Write-Err "ERP_PLATFORM_PASSWORD debe tener al menos 10 caracteres."
            exit 1
        }
    } else {
        Write-Info "Administra tenants, planes y configuracion global del SaaS."
        Write-Info "NO es un usuario de ningun tenant ERP. Tiene contexto platform exclusivo."
        Write-Host ""

        # Email
        if ($env:ERP_PLATFORM_EMAIL) {
            $opEmail = $env:ERP_PLATFORM_EMAIL.Trim().ToLower()
            Write-Info "Email: $opEmail (desde ERP_PLATFORM_EMAIL)"
        } else {
            do {
                $opEmail = (Prompt-Line "Email del operador platform").ToLower()
                if ($opEmail -notmatch '^[^@]+@[^@]+\.[^@]+$') { Write-Err "Formato de email invalido." }
            } while ($opEmail -notmatch '^[^@]+@[^@]+\.[^@]+$')
        }

        # Nombre
        $fullName  = Prompt-Line "Nombre completo (ej: Ana Garcia)"
        $parts     = $fullName.Trim() -split '\s+', 2
        $firstName = $parts[0]
        $lastName  = if ($parts.Length -gt 1) { $parts[1] } else { "" }
        if ([string]::IsNullOrWhiteSpace($firstName) -or [string]::IsNullOrWhiteSpace($lastName)) {
            Write-Err "Se requieren nombre Y apellido (minimo dos palabras)."
            exit 1
        }

        # Contrasena
        if ($env:ERP_PLATFORM_PASSWORD) {
            $opPassword = $env:ERP_PLATFORM_PASSWORD
            Write-Info "Contrasena recibida desde ERP_PLATFORM_PASSWORD."
        } else {
            do {
                $opPassword = Prompt-Secret "Contrasena (minimo 10 caracteres)"
                if ($opPassword.Length -lt 10) { Write-Err "Minimo 10 caracteres." }
            } while ($opPassword.Length -lt 10)
            $confirm2 = Prompt-Secret "Confirmar contrasena"
            if ($opPassword -ne $confirm2) {
                Write-Err "Las contrasenas no coinciden."
                $opPassword = $null; $confirm2 = $null; [System.GC]::Collect(); exit 1
            }
            $confirm2 = $null
        }
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
    Write-Ok "user_type=Platform | platform_role=PlatformOperator | subscriber_id=Guid.Empty"
}

# ═════════════════════════════════════════════════════════════════════════════
# ETAPA 5 · VERIFICACIÓN DE LOGIN
# ═════════════════════════════════════════════════════════════════════════════
Write-Stage "Verificacion de login platform"

if (-not [string]::IsNullOrWhiteSpace($jwtToken)) {
    Write-Ok "JWT recibido en la respuesta del setup — login verificado implicitamente."
} else {
    if ([string]::IsNullOrWhiteSpace($opEmail)) {
        if ($NonInteractive) { $opEmail = $env:ERP_PLATFORM_EMAIL }
        else { $opEmail = Prompt-Line "Email del operador platform" }
    }
    if ([string]::IsNullOrWhiteSpace($opPassword)) {
        if ($NonInteractive) { $opPassword = $env:ERP_PLATFORM_PASSWORD }
        else { $opPassword = Prompt-Secret "Contrasena del operador platform" }
    }

    Write-Info "Verificando login en /api/platform/auth/login ..."
    $loginRes = Invoke-Api "POST" "/api/platform/auth/login" @{ email=$opEmail; password=$opPassword }
    $jwtToken = getToken $loginRes

    if ((isOk $loginRes) -and -not [string]::IsNullOrWhiteSpace($jwtToken)) {
        Write-Ok "Login platform verificado correctamente."
    } else {
        Write-Warn "No se pudo verificar login. Comprueba Deployment:PlatformPanelEnabled=true"
        $msg = getMsg $loginRes; if ($msg) { Write-Warn "Detalle: $msg" }
        if ($NonInteractive) {
            Write-Err "Verificacion de login fallida en modo no-interactivo."
            exit 1
        }
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
# ETAPA 6 · RESUMEN FINAL
# ═════════════════════════════════════════════════════════════════════════════
Write-Stage "Resumen final del aprovisionamiento"

$finalRes = Invoke-Api "GET" "/api/setup/platform/status"
$final    = getObj $finalRes
$ownerOk  = if ($final) { $final.hasInternalPlatformOwner } else { $true }
$opOk     = if ($final) { $final.hasPlatformOperator      } else { $true }
$allOk    = $ownerOk -and $opOk

Write-Host ""
Write-Host "  +-------------------------------------------------------------+" -ForegroundColor DarkCyan
Write-Host "  |  ESTADO FINAL DEL SISTEMA                                   |" -ForegroundColor DarkCyan
Write-Host "  +-------------------------------------------------------------+" -ForegroundColor DarkCyan
$ownerStatus = if ($ownerOk) { "+ Configurado  " } else { "X Pendiente    " }
$opStatus    = if ($opOk)    { "+ Creado       " } else { "X Pendiente    " }
$sysStatus   = if ($allOk)   { "+ LISTO PARA PRODUCCION" } else { "! INCOMPLETO           " }
Write-Host "  |  Propietario de plataforma : $ownerStatus                   |" -ForegroundColor $(if ($ownerOk) { "Green" } else { "Red"    })
Write-Host "  |  Operador platform         : $opStatus                   |" -ForegroundColor $(if ($opOk)    { "Green" } else { "Red"    })
Write-Host "  |  Estado general            : $sysStatus     |" -ForegroundColor $(if ($allOk)   { "Green" } else { "Yellow" })
Write-Host "  +-------------------------------------------------------------+" -ForegroundColor DarkCyan
Write-Host ""

if ($allOk) {
    Write-Ok "Plataforma SaaS aprovisionada y lista para operar."
    Write-Host ""
    Write-Info "Proximos pasos:"
    Write-Detail "  1. Abre el panel platform en el frontend"
    Write-Detail "  2. Inicia sesion con el email/contrasena del operador platform"
    Write-Detail "  3. Crea el primer tenant: Panel -> Suscriptores -> Nuevo Suscriptor"
    Write-Detail "  4. El tenant configurara su empresa ERP en el onboarding wizard"
    Write-Host ""
    Write-Warn "El token first-run ya fue consumido y no puede reutilizarse."
    exit 0
} else {
    Write-Warn "El aprovisionamiento quedo incompleto. Revisa los errores anteriores."
    Write-Info "El script es idempotente — puedes volver a ejecutarlo."
    if ($NonInteractive) { exit 2 }
    exit 2
}

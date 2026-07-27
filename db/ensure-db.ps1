<#
.SYNOPSIS
    Creates the FAT_DB database if it does not exist yet. Never touches it
    otherwise.

.DESCRIPTION
    Meant to run automatically before every build (see App.csproj) so a fresh
    clone gets a working database without anyone typing a command by hand.

    This is intentionally the opposite of setup-db.ps1: it NEVER drops or
    reseeds an existing database. If FAT_DB is already there, this script
    does nothing and exits immediately - schema upgrades (SchemaUpgrader) and
    FLM data (DataSeeder) are handled by the app itself at startup, not here.

    Every failure path here is non-fatal (exit 0) on purpose: a build must
    still succeed on a machine with no local SQL Server (CI, a teammate who
    only writes code that doesn't touch the database, etc).

.PARAMETER Server
    SQL Server instance name. Defaults to whatever ConnectionStrings:AppDatabase
    in src/App/appsettings.Local.json (or appsettings.json) points to, falling
    back to 'localhost' if that cannot be read - see Resolve-ServerFromAppSettings.

.PARAMETER SqlUser
    SQL Authentication login. Leave empty to use Windows Authentication.
#>
[CmdletBinding()]
param(
    [string] $Server = '',
    [string] $SqlUser = '',
    [string] $SqlPassword = ''
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

function Resolve-ServerFromAppSettings {
    # appsettings.Local.json is per-developer and gitignored (see docs/TEAM.md) -
    # it is the actual source of truth for "which SQL Server does my app use",
    # so ensure-db.ps1 must agree with it instead of assuming localhost.
    $appDir = Join-Path $scriptDir '..\src\App'
    $candidates = @(
        (Join-Path $appDir 'appsettings.Local.json'),
        (Join-Path $appDir 'appsettings.json')
    )

    foreach ($path in $candidates) {
        if (-not (Test-Path $path)) {
            continue
        }

        try {
            $json = Get-Content -Raw -Path $path | ConvertFrom-Json
            $connectionString = $json.ConnectionStrings.AppDatabase
            if ([string]::IsNullOrWhiteSpace($connectionString)) {
                continue
            }

            foreach ($part in $connectionString -split ';') {
                $kv = $part -split '=', 2
                if ($kv.Length -eq 2 -and $kv[0].Trim() -eq 'Server') {
                    return $kv[1].Trim()
                }
            }
        }
        catch {
            # Malformed appsettings should not block the build - just fall
            # through to the next candidate / the localhost default.
            continue
        }
    }

    return 'localhost'
}

if ([string]::IsNullOrWhiteSpace($Server)) {
    $Server = Resolve-ServerFromAppSettings
}

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    Write-Host "[ensure-db] sqlcmd not found on PATH - skipping database bootstrap." -ForegroundColor DarkGray
    exit 0
}

$commonArgs = @('-S', $Server, '-C', '-b')
if ([string]::IsNullOrWhiteSpace($SqlUser)) {
    $commonArgs += '-E'
}
else {
    $commonArgs += @('-U', $SqlUser, '-P', $SqlPassword)
}

# -l 5 caps the login timeout so an unreachable server fails fast instead of
# hanging the build for a long time.
$existsQuery = "SET NOCOUNT ON; SELECT CASE WHEN DB_ID('FAT_DB') IS NULL THEN 0 ELSE 1 END;"
$result = & sqlcmd @commonArgs -l 5 -h -1 -W -Q $existsQuery 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ensure-db] Could not reach SQL Server on '$Server' - skipping database bootstrap." -ForegroundColor DarkGray
    Write-Host "[ensure-db] (Run '.\db\setup-db.ps1' manually once SQL Server is available.)" -ForegroundColor DarkGray
    exit 0
}

$dbExists = ($result | Select-Object -Last 1).Trim() -eq '1'
if ($dbExists) {
    Write-Host "[ensure-db] FAT_DB already exists - leaving it untouched." -ForegroundColor DarkGray
    exit 0
}

Write-Host "[ensure-db] FAT_DB not found on '$Server' - creating it via setup-db.ps1..." -ForegroundColor Cyan
$setupScript = Join-Path $scriptDir 'setup-db.ps1'
& $setupScript -Server $Server -SqlUser $SqlUser -SqlPassword $SqlPassword

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ensure-db] setup-db.ps1 failed - build will continue, but the app has no database yet." -ForegroundColor Yellow
    Write-Host "[ensure-db] Fix the error above and re-run '.\db\setup-db.ps1' manually." -ForegroundColor Yellow
}

exit 0

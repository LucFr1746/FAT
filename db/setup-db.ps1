<#
.SYNOPSIS
    Rebuilds the FAT_DB database from scratch.

.DESCRIPTION
    Runs 01_schema.sql, then 02_seed_master.sql, then 03_seed_demo.sql.
    The script STOPS at the first error (sqlcmd -b) instead of carrying on and
    leaving behind a half-built database that is painful to diagnose.

    WARNING: this DROPS the existing FAT_DB database. Any data you entered is lost.

.PARAMETER Server
    SQL Server instance name. Defaults to 'localhost' (the default instance).
    For SQL Server Express pass '.\SQLEXPRESS'.

.PARAMETER SqlUser
    SQL Authentication login. Leave empty to use Windows Authentication.

.EXAMPLE
    .\db\setup-db.ps1

.EXAMPLE
    .\db\setup-db.ps1 -Server ".\SQLEXPRESS"
#>
[CmdletBinding()]
param(
    [string] $Server = 'localhost',
    [string] $SqlUser = '',
    [string] $SqlPassword = ''
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    Write-Error @"
'sqlcmd' was not found on PATH.

What to do:
  1. Install the SQL Server Command Line Utilities (they ship with SSMS), OR
  2. Skip this script and run the files manually in SSMS: open
     01_schema.sql, 02_seed_master.sql and 03_seed_demo.sql in that order
     and press F5 on each.
"@
    exit 1
}

# -C = trust the server certificate. Required with the modern driver, otherwise
#      it reports a certificate error even against localhost.
# -b = return a non-zero exit code on a T-SQL error so PowerShell can stop.
$commonArgs = @('-S', $Server, '-C', '-b')
if ([string]::IsNullOrWhiteSpace($SqlUser)) {
    $commonArgs += '-E'                                  # Windows Authentication
}
else {
    $commonArgs += @('-U', $SqlUser, '-P', $SqlPassword) # SQL Authentication
}

$scripts = @(
    '01_schema.sql',
    '02_seed_master.sql',
    '03_seed_demo.sql'
)

Write-Host ""
Write-Host "Rebuilding the FAT_DB database on '$Server'..." -ForegroundColor Cyan
Write-Host ""

foreach ($file in $scripts) {
    $path = Join-Path $scriptDir $file
    if (-not (Test-Path $path)) {
        Write-Error "Missing file: $path"
        exit 1
    }

    Write-Host "  -> $file" -NoNewline
    $output = & sqlcmd @commonArgs -i $path 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  FAILED" -ForegroundColor Red
        Write-Host ""
        $output | ForEach-Object { Write-Host "     $_" -ForegroundColor Red }
        Write-Host ""
        Write-Host "Hint: for 'Cannot open database' or 'Login failed', check the" -ForegroundColor Yellow
        Write-Host "instance name with:  sqlcmd -S $Server -E -C -Q `"SELECT @@SERVERNAME`"" -ForegroundColor Yellow
        exit 1
    }
    Write-Host "  OK" -ForegroundColor Green
}

# Verify against real row counts rather than trusting the exit code alone
$verifyQuery = @"
SET NOCOUNT ON;
SELECT CONCAT(
    (SELECT COUNT(*) FROM dbo.Course),     '|',
    (SELECT COUNT(*) FROM dbo.Student),    '|',
    (SELECT COUNT(*) FROM dbo.Enrollment), '|',
    (SELECT COUNT(*) FROM dbo.Grade),      '|',
    (SELECT COUNT(*) FROM dbo.Material));
"@
$counts = (& sqlcmd @commonArgs -d FAT_DB -h -1 -W -Q $verifyQuery | Select-Object -First 1) -split '\|'

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "  Courses     : $($counts[0])"
Write-Host "  Students    : $($counts[1])"
Write-Host "  Enrollments : $($counts[2])"
Write-Host "  Grades      : $($counts[3])"
Write-Host "  Materials   : $($counts[4])"
Write-Host ""
Write-Host "Demo accounts (passwords are case sensitive):" -ForegroundColor Cyan
Write-Host "  admin      / Admin@123"
Write-Host "  student01  / Student@123    (final year,  ~78% progress, 1 retake)"
Write-Host "  student02  / Student@123    (second year, ~53% progress)"
Write-Host "  student03  / Student@123    (first year,  ~14% progress)"
Write-Host ""

if ($Server -ne 'localhost') {
    Write-Host "You are using the non-default instance '$Server'. Remember to create" -ForegroundColor Yellow
    Write-Host "src\App\appsettings.Local.json to override the connection string." -ForegroundColor Yellow
    Write-Host ""
}

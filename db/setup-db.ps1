<#
.SYNOPSIS
    Dựng lại toàn bộ database SAT từ đầu.

.DESCRIPTION
    Chạy lần lượt 01_schema.sql -> 02_seed_master.sql -> 03_seed_demo.sql.
    Script DỪNG NGAY ở lỗi đầu tiên (sqlcmd -b) thay vì chạy tiếp và để lại
    một database nửa vời khó chẩn đoán.

    CẢNH BÁO: script XÓA database SAT hiện có. Mọi dữ liệu bạn tự nhập sẽ mất.

.PARAMETER Server
    Tên SQL Server instance. Mặc định 'localhost' (default instance).
    Dùng SQL Express thì truyền '.\SQLEXPRESS'.

.PARAMETER SqlUser
    Tài khoản SQL Authentication. Bỏ trống để dùng Windows Authentication.

.EXAMPLE
    .\db\setup-db.ps1

.EXAMPLE
    .\db\setup-db.ps1 -Server ".\SQLEXPRESS"
#>
[CmdletBinding()]
param(
    [string] $Server  = 'localhost',
    [string] $SqlUser = '',
    [string] $SqlPassword = ''
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    Write-Error @"
Khong tim thay 'sqlcmd' trong PATH.

Cach xu ly:
  1. Cai 'SQL Server Command Line Utilities' (di kem SSMS), HOAC
  2. Bo qua script nay va chay thu cong trong SSMS: mo lan luot
     01_schema.sql, 02_seed_master.sql, 03_seed_demo.sql roi nhan F5.
"@
    exit 1
}

# -C = tin certificate cua server. Bat buoc voi driver moi, neu khong se bao
#      loi certificate ngay ca khi ket noi localhost.
# -b = tra ve exit code khac 0 khi T-SQL loi, de PowerShell biet ma dung.
$commonArgs = @('-S', $Server, '-C', '-b')
if ([string]::IsNullOrWhiteSpace($SqlUser)) {
    $commonArgs += '-E'                                  # Windows Authentication
} else {
    $commonArgs += @('-U', $SqlUser, '-P', $SqlPassword) # SQL Authentication
}

$scripts = @(
    '01_schema.sql',
    '02_seed_master.sql',
    '03_seed_demo.sql'
)

Write-Host ""
Write-Host "Dung lai database SAT tren '$Server'..." -ForegroundColor Cyan
Write-Host ""

foreach ($file in $scripts) {
    $path = Join-Path $scriptDir $file
    if (-not (Test-Path $path)) {
        Write-Error "Thieu file: $path"
        exit 1
    }

    Write-Host "  -> $file" -NoNewline
    $output = & sqlcmd @commonArgs -i $path 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  THAT BAI" -ForegroundColor Red
        Write-Host ""
        $output | ForEach-Object { Write-Host "     $_" -ForegroundColor Red }
        Write-Host ""
        Write-Host "Goi y: neu loi 'Cannot open database' hoac 'Login failed', kiem tra" -ForegroundColor Yellow
        Write-Host "ten instance bang lenh:  sqlcmd -S $Server -E -C -Q `"SELECT @@SERVERNAME`"" -ForegroundColor Yellow
        exit 1
    }
    Write-Host "  OK" -ForegroundColor Green
}

# Kiem tra lai bang so lieu that, khong chi tin vao exit code
$verifyQuery = @"
SET NOCOUNT ON;
SELECT CONCAT(
    (SELECT COUNT(*) FROM dbo.Course),     '|',
    (SELECT COUNT(*) FROM dbo.Student),    '|',
    (SELECT COUNT(*) FROM dbo.Enrollment), '|',
    (SELECT COUNT(*) FROM dbo.Grade));
"@
$counts = (& sqlcmd @commonArgs -d SAT -h -1 -W -Q $verifyQuery | Select-Object -First 1) -split '\|'

Write-Host ""
Write-Host "Hoan tat." -ForegroundColor Green
Write-Host "  Mon hoc   : $($counts[0])"
Write-Host "  Sinh vien : $($counts[1])"
Write-Host "  Dang ky   : $($counts[2])"
Write-Host "  Diem      : $($counts[3])"
Write-Host ""
Write-Host "Tai khoan demo (mat khau phan biet hoa thuong):" -ForegroundColor Cyan
Write-Host "  admin      / Admin@123"
Write-Host "  student01  / Student@123    (nam cuoi, ~78% tien do, co 1 mon hoc lai)"
Write-Host "  student02  / Student@123    (nam 2,    ~53% tien do)"
Write-Host "  student03  / Student@123    (nam nhat, ~14% tien do)"
Write-Host ""

if ($Server -ne 'localhost') {
    Write-Host "Ban dung instance '$Server' khac mac dinh. Nho tao file" -ForegroundColor Yellow
    Write-Host "src\SAT.App\appsettings.Local.json de ghi de connection string." -ForegroundColor Yellow
    Write-Host ""
}

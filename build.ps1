$ErrorActionPreference = "Stop"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "Building BatteryGuard Pro (Lightweight Portable Battery Alarm)" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path $csc)) {
    Write-Error "Microsoft .NET Framework 4.8 C# compiler not found."
    exit 1
}

$sources = @(
    "src\AssemblyInfo.cs",
    "src\AppSettings.cs",
    "src\AudioAlert.cs",
    "src\BatteryHealthService.cs",
    "src\BatteryMonitor.cs",
    "src\Controls\ModernToggle.cs",
    "src\Controls\ModernSlider.cs",
    "src\Controls\ModernCard.cs",
    "src\BatteryWidget.cs",
    "src\AlertPopupForm.cs",
    "src\SystemTrayManager.cs",
    "src\MainForm.cs",
    "src\Program.cs"
)

$args = @(
    "/target:winexe",
    "/optimize+",
    "/platform:anycpu",
    "/win32icon:app.ico",
    "/out:BatteryGuard.exe",
    "/r:System.dll",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.Data.dll",
    "/r:System.Xml.dll"
) + $sources

& $csc $args

if ($LASTEXITCODE -eq 0) {
    $size = (Get-Item "BatteryGuard.exe").Length / 1KB
    Write-Host "`nBuild SUCCESS!" -ForegroundColor Green
    Write-Host ("Executable created: BatteryGuard.exe ({0:N1} KB)" -f $size) -ForegroundColor Green
    Write-Host "Ready to transfer to any Windows 10/11 laptop!" -ForegroundColor Green
} else {
    Write-Host "`nBuild FAILED!" -ForegroundColor Red
}

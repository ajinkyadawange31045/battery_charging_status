@echo off
setlocal
echo ========================================================
echo Building BatteryGuard Pro (Lightweight Portable Battery Alarm)
echo ========================================================

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist %CSC% (
    set CSC="C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if not exist %CSC% (
    echo Error: .NET Framework C# compiler was not found!
    pause
    exit /b 1
)

echo Compiling using built-in Windows C# Compiler...
%CSC% /target:winexe /optimize+ /platform:anycpu /win32icon:app.ico /out:BatteryGuard.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Data.dll /r:System.Xml.dll src\AssemblyInfo.cs src\AppSettings.cs src\AudioAlert.cs src\BatteryHealthService.cs src\BatteryMonitor.cs src\Controls\ModernToggle.cs src\Controls\ModernSlider.cs src\Controls\ModernCard.cs src\BatteryWidget.cs src\AlertPopupForm.cs src\SystemTrayManager.cs src\MainForm.cs src\Program.cs

if %ERRORLEVEL% equ 0 (
    echo.
    echo ========================================================
    echo Build SUCCESS! Output: BatteryGuard.exe
    echo You can now run BatteryGuard.exe or transfer it to any PC!
    echo ========================================================
) else (
    echo.
    echo Build FAILED with error code %ERRORLEVEL%.
)
endlocal

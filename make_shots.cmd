@echo off
rem ===========================================================================
rem  Рендерит картинки виджета для README в docs\ (прозрачный фон, x2).
rem  Требует собранных исходников в этой же папке. Запускать при запущенном плеере.
rem ===========================================================================
setlocal
set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
set "WINMD=%WINDIR%\System32\WinMetadata"

"%FW%\csc.exe" /nologo /target:exe /out:Shots.exe ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:"%FW%\System.Runtime.WindowsRuntime.dll" /r:"%FW%\System.Runtime.dll" /r:"%FW%\System.Runtime.Extensions.dll" ^
  /r:"%WINMD%\Windows.Foundation.winmd" /r:"%WINMD%\Windows.Storage.winmd" /r:"%WINMD%\Windows.Media.winmd" ^
  Config.cs MediaApi.cs Renderer.cs Shots.cs
if errorlevel 1 (echo BUILD FAILED & exit /b 1)

Shots.exe
if errorlevel 1 exit /b 1

if not exist docs mkdir docs
move /y shot-widget.png docs\widget-dark.png >nul
move /y shot-blur.png   docs\widget-blur.png >nul
move /y shot-tint.png   docs\widget-tint.png >nul
move /y shot-color.png  docs\widget-color.png >nul
del /q Shots.exe 2>nul
echo.
echo Done -^> docs\

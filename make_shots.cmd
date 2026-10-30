@echo off
rem ===========================================================================
rem  Renders the widget gallery for README into docs\ (transparent background, x2).
rem  Album art is synthetic, so no music player is required. 10 style variants.
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
for %%f in (01-dark 02-left-no-border 03-blur 04-blur-tint 05-cover-color 06-light 07-glass 08-blur-shadow 09-compact 10-gradient-border) do (
  if exist %%f.png move /y %%f.png "docs\%%f.png" >nul
)
del /q Shots.exe 2>nul
echo.
echo Done -^> docs\

@echo off
setlocal
cd /d "%~dp0"
set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
  echo [ERROR] ISCC.exe not found. Install Inno Setup 6: https://jrsoftware.org/isdl.php
  exit /b 1
)
if not exist MusicBarWin11.exe (
  echo [ERROR] MusicBarWin11.exe not found. Run build.cmd first.
  exit /b 1
)
"%ISCC%" installer.iss
if errorlevel 1 exit /b 1
echo.
echo Installer -^> dist\

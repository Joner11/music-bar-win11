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

rem Копия с постоянным именем. На неё ссылается README:
rem /releases/latest/download/MusicBarWin11-Setup.exe
rem Так ссылка не меняется от версии к версии.
for /f "delims=" %%f in ('dir /b /o-d "dist\MusicBarWin11-Setup-*.exe" 2^>nul') do (
  copy /y "dist\%%f" "dist\MusicBarWin11-Setup.exe" >nul
  goto :done
)
:done

echo.
echo Installer -^> dist\

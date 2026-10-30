@echo off
setlocal
if not exist icon.ico (echo MISSING icon.ico & exit /b 1)
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set REFDIR=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
set WINMD=C:\Windows\System32\WinMetadata
"%CSC%" /nologo /target:winexe /optimize+ /out:MusicBarWin11.exe /win32icon:icon.ico ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:"%REFDIR%\System.Runtime.WindowsRuntime.dll" ^
  /r:"%REFDIR%\System.Runtime.dll" ^
  /r:"%REFDIR%\System.Runtime.Extensions.dll" ^
  /r:"%WINMD%\Windows.Foundation.winmd" ^
  /r:"%WINMD%\Windows.Storage.winmd" ^
  /r:"%WINMD%\Windows.Media.winmd" ^
  Config.cs MediaApi.cs Renderer.cs Widget.cs SettingsForm.cs Version.cs
if errorlevel 1 (
  echo BUILD FAILED
  exit /b 1
)
echo BUILD OK

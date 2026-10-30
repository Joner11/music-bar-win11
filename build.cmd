@echo off
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set REFDIR=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
set WINMD=C:\Windows\System32\WinMetadata
taskkill /F /IM MusicBarWin11.exe >nul 2>&1
"%CSC%" /nologo /target:winexe /optimize+ /out:MusicBarWin11.exe /win32icon:icon.ico ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:"%REFDIR%\System.Runtime.WindowsRuntime.dll" ^
  /r:"%REFDIR%\System.Runtime.dll" ^
  /r:"%REFDIR%\System.Runtime.Extensions.dll" ^
  /r:"%WINMD%\Windows.Foundation.winmd" ^
  /r:"%WINMD%\Windows.Storage.winmd" ^
  /r:"%WINMD%\Windows.Media.winmd" ^
  Config.cs MediaApi.cs Renderer.cs Widget.cs Ui.cs Ui2.cs SettingsForm.cs Version.cs
if errorlevel 1 (echo BUILD FAILED & exit /b 1)
echo BUILD OK

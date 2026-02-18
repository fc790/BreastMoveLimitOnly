@echo off
setlocal

REM ===== 修改这里 =====
set "GAME_DIR=D:\Games\Koikatu"
set "SRC=BreastMoveLimitOnly_Ver02.cs"
set "OUT=BreastMoveLimitOnlyForKK.dll"
REM ====================

set "CSC=C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe"

echo Using CSC : "%CSC%"
echo Using SRC : "%CD%\%SRC%"
echo Using OUT : "%GAME_DIR%\BepInEx\plugins\%OUT%"
echo.

if not exist "%CSC%" (
  echo [ERROR] csc.exe not found: "%CSC%"
  pause
  exit /b 1
)

if not exist "%SRC%" (
  echo [ERROR] source not found in current folder: "%CD%\%SRC%"
  dir /b *.cs
  pause
  exit /b 1
)

set "OUT_PATH=%GAME_DIR%\BepInEx\plugins\%OUT%"

if exist "%OUT_PATH%" (
  echo Removing old dll...
  del /f /q "%OUT_PATH%"
)

REM ---- required refs ----
set "REFS=/r:%GAME_DIR%\BepInEx\core\BepInEx.dll /r:%GAME_DIR%\BepInEx\core\0Harmony.dll /r:%GAME_DIR%\Koikatu_Data\Managed\Assembly-CSharp.dll /r:%GAME_DIR%\Koikatu_Data\Managed\UnityEngine.dll"

REM ---- optional: ConfigurationManager (skip if missing) ----
if exist "%GAME_DIR%\BepInEx\core\BepInEx.ConfigurationManager.dll" (
  set "REFS=%REFS% /r:%GAME_DIR%\BepInEx\core\BepInEx.ConfigurationManager.dll"
) else (
  echo [WARN] optional ref missing: BepInEx.ConfigurationManager.dll (skip)
)

REM ---- optional: UnityEngine.CoreModule (Unity 2018+ has it; Unity 5 usually not) ----
if exist "%GAME_DIR%\Koikatu_Data\Managed\UnityEngine.CoreModule.dll" (
  set "REFS=%REFS% /r:%GAME_DIR%\Koikatu_Data\Managed\UnityEngine.CoreModule.dll"
) else (
  echo [WARN] optional ref missing: UnityEngine.CoreModule.dll (skip)
)

echo Refs: %REFS%
echo.

"%CSC%" /target:library /optimize+ /platform:anycpu %REFS% /out:"%OUT_PATH%" "%SRC%"

if errorlevel 1 (
  echo.
  echo [ERROR] compile failed
  pause
  exit /b 1
)

echo.
echo [OK] Built "%OUT_PATH%"
pause

@echo off
chcp 65001 >nul
setlocal EnableExtensions

REM =========================
REM  KKS build script (TEMP/TMP forced)
REM =========================

set "GAME_DIR=D:\Games\KoikatsuSunshine"
set "SRC=%~dp0BreastMoveLimitOnly_Ver02.cs"
set "OUT=%GAME_DIR%\BepInEx\plugins\BreastMoveLimitOnlyForKKS.dll"

set "CSC=%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe"
if not exist "%CSC%" (
  echo [ERROR] csc.exe not found: "%CSC%"
  goto :FAIL
)

REM ---- find Managed folder ----
set "MANAGED="
for %%D in (KoikatsuSunshine_Data KoikatsuSunshine64_Data KKS_Data) do (
  if exist "%GAME_DIR%\%%D\Managed\Assembly-CSharp.dll" (
    set "MANAGED=%GAME_DIR%\%%D\Managed"
    goto :FOUND
  )
)
:FOUND
if "%MANAGED%"=="" (
  echo [ERROR] Cannot find Managed folder under: "%GAME_DIR%"
  goto :FAIL
)

if not exist "%SRC%" (
  echo [ERROR] source not found: "%SRC%"
  goto :FAIL
)

REM ---- refs ----
set "REFS=/r:%GAME_DIR%\BepInEx\core\BepInEx.dll /r:%GAME_DIR%\BepInEx\core\0Harmony.dll /r:%MANAGED%\Assembly-CSharp.dll /r:%MANAGED%\UnityEngine.dll"

if exist "%GAME_DIR%\BepInEx\core\BepInEx.ConfigurationManager.dll" (
  set "REFS=%REFS% /r:%GAME_DIR%\BepInEx\core\BepInEx.ConfigurationManager.dll"
) else (
  echo [WARN] optional ref missing: BepInEx.ConfigurationManager.dll - skip
)

if exist "%MANAGED%\UnityEngine.CoreModule.dll" (
  set "REFS=%REFS% /r:%MANAGED%\UnityEngine.CoreModule.dll"
) else (
  echo [WARN] optional ref missing: UnityEngine.CoreModule.dll - skip
)

REM Ensure output dir exists
for %%P in ("%OUT%") do if not exist "%%~dpP" mkdir "%%~dpP" >nul 2>&1

REM ---- CRITICAL FIX: force compiler temp dir ----
set "CTMP=C:\_csc_tmp"
if not exist "%CTMP%" mkdir "%CTMP%" >nul 2>&1

REM Make sure TEMP/TMP are valid for csc internal use
set "TEMP=%CTMP%"
set "TMP=%CTMP%"

REM Also use short path if available (helps old tools)
for %%S in ("%CTMP%") do set "CTMP_SHORT=%%~sS"
if not "%CTMP_SHORT%"=="" (
  set "TEMP=%CTMP_SHORT%"
  set "TMP=%CTMP_SHORT%"
)

REM We'll compile to OUT directly (avoid extra copy)
echo Using CSC : "%CSC%"
echo Using SRC : "%SRC%"
echo Using OUT : "%OUT%"
echo Using MAN : "%MANAGED%"
echo TEMP/TMP : "%TEMP%"
echo Refs: %REFS%
echo.

"%CSC%" /nologo /target:library /optimize+ %REFS% /out:"%OUT%" "%SRC%"
if errorlevel 1 (
  echo.
  echo [ERROR] compile failed
  goto :FAIL
)

echo OK: Built "%OUT%"
echo.
pause
exit /b 0

:FAIL
echo.
echo [FAIL]
pause
exit /b 1

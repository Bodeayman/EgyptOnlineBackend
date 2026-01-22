@echo off
REM Run Login Tests Batch Script
REM This script runs only UserLoginServiceTests

setlocal enabledelayedexpansion

echo.
echo ================================
echo    Login Tests Runner
echo ================================
echo.

cd /d "%~dp0"
echo Working Directory: %CD%
echo.

echo Running Login Tests...
echo.

call dotnet test ^
    --filter "FullyQualifiedName~UserLoginServiceTests" ^
    --logger "console;verbosity=normal" ^
    --configuration Debug ^
    --verbosity normal

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ================================
    echo [OK] Login Tests Passed!
    echo ================================
) else (
    echo.
    echo ================================
    echo [FAILED] Login Tests Failed
    echo ================================
)

exit /b %ERRORLEVEL%

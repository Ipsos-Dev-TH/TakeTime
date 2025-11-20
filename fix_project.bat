@echo off
echo ========================================
echo Visual Studio Project Cache Cleaner
echo ========================================
echo.

REM Close Visual Studio if running
echo Closing Visual Studio...
taskkill /F /IM devenv.exe 2>nul
timeout /t 2 /nobreak >nul

echo.
echo Deleting Visual Studio cache folders...
echo.

REM Delete .vs folder
if exist ".vs" (
    echo [1/3] Deleting .vs folder...
    rd /s /q ".vs"
    echo       Done!
) else (
    echo [1/3] .vs folder not found (OK)
)

REM Delete bin folder
if exist "Take Time BangPhra\bin" (
    echo [2/3] Deleting bin folder...
    rd /s /q "Take Time BangPhra\bin"
    echo       Done!
) else (
    echo [2/3] bin folder not found (OK)
)

REM Delete obj folder
if exist "Take Time BangPhra\obj" (
    echo [3/3] Deleting obj folder...
    rd /s /q "Take Time BangPhra\obj"
    echo       Done!
) else (
    echo [3/3] obj folder not found (OK)
)

echo.
echo ========================================
echo SUCCESS! Cache cleared.
echo ========================================
echo.
echo Next Steps:
echo   1. Open Visual Studio
echo   2. File -^> Open -^> Project/Solution
echo   3. Select: Take Time BangPhra.sln
echo   4. Build -^> Clean Solution
echo   5. Build -^> Rebuild Solution
echo.
echo The project should now load without errors.
echo.
pause

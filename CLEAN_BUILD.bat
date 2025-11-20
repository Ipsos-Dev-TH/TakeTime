@echo off
echo ========================================
echo Clean Build - Fix Tesseract Errors
echo ========================================
echo.
echo This script will:
echo 1. Close Visual Studio
echo 2. Delete bin and obj folders (cached compiled files)
echo 3. Delete .vs folder (Visual Studio cache)
echo 4. Instructions for rebuild
echo.
pause

cd /d "%~dp0"

echo.
echo Step 1: Closing Visual Studio...
taskkill /F /IM devenv.exe 2>nul
if %ERRORLEVEL% EQU 0 (
    echo Visual Studio closed successfully.
    timeout /t 2 /nobreak >nul
) else (
    echo Visual Studio was not running or already closed.
)

echo.
echo Step 2: Deleting bin folder...
rd /s /q "Take Time BangPhra\bin" 2>nul
echo Bin folder deleted.

echo.
echo Step 3: Deleting obj folder...
rd /s /q "Take Time BangPhra\obj" 2>nul
echo Obj folder deleted.

echo.
echo Step 4: Deleting .vs folder (Visual Studio cache)...
rd /s /q ".vs" 2>nul
echo VS cache deleted.

echo.
echo ========================================
echo Cleanup Complete!
echo ========================================
echo.
echo Next Steps:
echo 1. Open Visual Studio
echo 2. Open Take Time BangPhra.sln
echo 3. Build menu -^> Clean Solution
echo 4. Build menu -^> Rebuild Solution
echo.
echo Expected result: 0 errors
echo.
pause

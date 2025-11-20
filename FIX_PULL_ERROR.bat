@echo off
echo ========================================
echo Fix Git Pull Error - Reset to Remote
echo ========================================
echo.
echo This will:
echo 1. Save your current changes (stash)
echo 2. Reset to match remote repository
echo 3. Pull latest changes
echo.
pause

cd /d "%~dp0"

echo.
echo Step 1: Stashing local changes...
git stash save "Backup before pull fix"

echo.
echo Step 2: Resetting to remote state...
git reset --hard origin/claude/fix-booking-page-011CUngeR2KnxCeKQWjxHRkt

echo.
echo Step 3: Pulling latest changes...
git pull origin claude/fix-booking-page-011CUngeR2KnxCeKQWjxHRkt

echo.
echo ========================================
echo Done! Your repository is now up to date.
echo ========================================
echo.
echo To view your stashed changes: git stash list
echo To apply stashed changes: git stash pop
echo.
pause

@echo off
rem ============================================================
rem  WildCourt local dev server: stop server + MySQL
rem ============================================================
setlocal

set "MYSQL_DIR=%USERPROFILE%\mysql-8.0.29-winx64"

rem ---- 1. Stop server (process listening on 5000) ----
for /f "tokens=5" %%p in ('netstat -ano ^| findstr ":5000 " ^| findstr "LISTENING"') do (
    echo [Server] stopping PID %%p
    taskkill /PID %%p /F >nul 2>nul
)

rem ---- 2. Stop MySQL ----
"%MYSQL_DIR%\bin\mysqladmin.exe" -u root shutdown >nul 2>nul
if errorlevel 1 (
    echo [MySQL] not running or shutdown failed
) else (
    echo [MySQL] stopped
)

echo Done.
echo Press any key to close this window...
pause >nul
endlocal

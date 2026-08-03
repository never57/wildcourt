@echo off
rem ============================================================
rem  WildCourt local dev server: start MySQL + Web API
rem  Usage : double-click, or run start_server.bat in cmd
rem  Stop  : press Ctrl+C to stop the server, or run stop_server.bat
rem ============================================================
setlocal

set "MYSQL_DIR=%USERPROFILE%\mysql-8.0.29-winx64"
set "SERVER_DIR=%~dp0.."

rem ---- 1. Start MySQL (skip if already running) ----
"%MYSQL_DIR%\bin\mysqladmin.exe" -u root --connect-timeout=3 ping >nul 2>nul
if errorlevel 1 (
    echo [MySQL] starting ...
    start "" "%MYSQL_DIR%\bin\mysqld.exe" --defaults-file="%MYSQL_DIR%\my.ini"
    timeout /t 5 /nobreak >nul
) else (
    echo [MySQL] already running
)

rem ---- 2. Start server (foreground, Ctrl+C to stop) ----
where dotnet >nul 2>nul
if errorlevel 1 (
    set "DOTNET_CMD=%USERPROFILE%\.dotnet\dotnet.exe"
) else (
    set "DOTNET_CMD=dotnet"
)

cd /d "%SERVER_DIR%"
set "ASPNETCORE_ENVIRONMENT=Development"
echo [Server] starting on http://localhost:5000 ... (Ctrl+C to stop)
"%DOTNET_CMD%" run --project src\WlidCourt.Api

echo.
echo [Server] stopped. Press any key to close this window...
pause >nul
endlocal

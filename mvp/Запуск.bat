@echo off
chcp 65001 >nul
cd /d "%~dp0"
title Master Bidder — design server

echo.
echo ========================================
echo   Master Bidder — запуск
echo ========================================
echo.

where node >nul 2>&1
if errorlevel 1 (
  echo [ОШИБКА] Node.js не найден.
  echo Установите LTS с https://nodejs.org/
  echo Затем перезапустите терминал и снова откройте этот файл.
  echo.
  pause
  exit /b 1
)

set "PORT=8935"
set "LAN_IP="

for /f "tokens=2 delims=:" %%a in ('ipconfig ^| findstr /c:"IPv4"') do (
  for /f "tokens=*" %%b in ("%%a") do (
    if not defined LAN_IP set "LAN_IP=%%b"
  )
)

echo Game:   http://localhost:%PORT%/
echo Editor: http://localhost:%PORT%/gamedesign.html
echo.
if defined LAN_IP (
  echo С телефона в той же Wi-Fi:
  echo   http://%LAN_IP%:%PORT%/
  echo.
) else (
  echo IP не определён. Смотрите IPv4 через: ipconfig
  echo.
)
echo Ноутбук и телефон — одна Wi-Fi ^(не гостевая^).
echo В Firewall разрешите Node.js для частной сети.
echo.
echo Окно не закрывайте — сервер работает здесь.
echo Остановка: Ctrl+C
echo ========================================
echo.

start "" "http://localhost:%PORT%/"
start "" "http://localhost:%PORT%/gamedesign.html"

node design-server.js
if errorlevel 1 (
  echo.
  echo Сервер завершился с ошибкой.
  pause
)

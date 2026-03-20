@echo off
chcp 65001 >nul
title ExcelConvert 工具

set EXE_PATH=%~dp0Assets\Tools\excelConvert~\excelConvert\bin\Release\publish\excelConvert.exe

if not exist "%EXE_PATH%" (
    echo [错误] 未找到 excelConvert.exe
    echo 请先编译项目，在项目根目录执行：
    echo   dotnet publish Assets\Tools\excelConvert~\excelConvert\excelConvert.csproj -c Release -r win-x64 --self-contained false
    echo.
    pause
    exit /b 1
)

echo 正在启动 ExcelConvert...
start "" "%EXE_PATH%"

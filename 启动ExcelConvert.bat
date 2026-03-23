@echo off
chcp 65001 >nul
title ExcelConvert 工具

set EXPORTER_EXE=%~dp0Tools\excelConvert\ExcelExporter\publish\ExcelExporter.exe
set WPF_EXE=%~dp0Tools\excelConvert\excelConvert\publish\excelConvert.exe

if not exist "%EXPORTER_EXE%" (
    echo [错误] 未找到 ExcelExporter.exe
    echo 请先编译项目：
    echo   dotnet publish Tools\excelConvert\ExcelExporter\ExcelExporter.csproj -c Release -r win-x64 --self-contained false -o Tools\excelConvert\ExcelExporter\publish
    echo.
    pause
    exit /b 1
)

echo 请选择操作：
echo   1. 一键批量导出所有 Excel（推荐）
echo   2. 打开 ExcelConvert 界面
echo.
set /p choice=请输入选项 (1/2): 

if "%choice%"=="1" (
    echo 正在批量导出所有 Excel...
    "%EXPORTER_EXE%"
    echo.
    pause
) else (
    if not exist "%WPF_EXE%" (
        echo [错误] 未找到 excelConvert.exe，请先编译 WPF 项目
        pause
        exit /b 1
    )
    echo 正在启动 ExcelConvert...
    start "" "%WPF_EXE%"
)

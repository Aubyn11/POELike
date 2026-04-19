@echo off
chcp 65001 >nul
setlocal

title ExcelConvert 工具

set EXPORTER_EXE=%~dp0Tools\excelConvert\ExcelExporter\publish\ExcelExporter.exe
set WPF_EXE=%~dp0Tools\excelConvert\excelConvert\publish\excelConvert.exe
set EQUIPMENT_EXE=%~dp0Tools\GenEquipmentExcel\bin\Debug\net9.0\GenEquipmentExcel.exe
set PYTHON_EXPORTER=%~dp0Tools\excelConvert\export_excel_to_cfg.py

set ACTIVE_EXPORTER=
set EXPORTER_NAME=

if exist "%EXPORTER_EXE%" (
    set ACTIVE_EXPORTER=%EXPORTER_EXE%
    set EXPORTER_NAME=ExcelExporter
)

if not defined ACTIVE_EXPORTER (
    if exist "%EQUIPMENT_EXE%" if exist "%PYTHON_EXPORTER%" (
        set ACTIVE_EXPORTER=FALLBACK_PIPELINE
        set EXPORTER_NAME=FallbackPipeline
    )
)

if not defined ACTIVE_EXPORTER (
    echo [错误] 未找到可用的导表程序。
    echo.
    echo 已尝试以下路径：
    echo   %EXPORTER_EXE%
    echo   %EQUIPMENT_EXE%
    echo   %PYTHON_EXPORTER%
    echo.
    echo 当前仓库未提供可直接运行的内部 ExcelConvert 发布产物，且仓库内降级导表链也不完整。
    echo.
    pause
    exit /b 1
)

echo 检测到可用导表程序：%EXPORTER_NAME%
if /i "%EXPORTER_NAME%"=="FallbackPipeline" (
    echo [提示] 未检测到内部 ExcelExporter，当前将自动降级为仓库内全量导表链。
)

echo.
echo 请选择操作：
echo   1. 一键批量导出所有 Excel（推荐）
echo   2. 打开 ExcelConvert 界面（未提供时自动降级为命令行导表）
echo.
set /p choice=请输入选项 (1/2): 

if "%choice%"=="1" goto run_export
if "%choice%"=="2" goto run_gui

echo [错误] 请输入 1 或 2
pause
exit /b 1

:run_export
if /i "%EXPORTER_NAME%"=="ExcelExporter" (
    echo 正在执行导表：%EXPORTER_NAME%...
    "%ACTIVE_EXPORTER%"
    set EXIT_CODE=%ERRORLEVEL%
    echo.
    pause
    exit /b %EXIT_CODE%
)

echo 正在刷新 equipment.xlsx...
"%EQUIPMENT_EXE%"
if errorlevel 1 (
    set EXIT_CODE=%ERRORLEVEL%
    echo [错误] 刷新 equipment.xlsx 失败，错误码：%EXIT_CODE%
    echo.
    pause
    exit /b %EXIT_CODE%
)

echo 正在导出所有 Excel 到 Assets\Cfg...
python "%PYTHON_EXPORTER%"
set EXIT_CODE=%ERRORLEVEL%
echo.
pause
exit /b %EXIT_CODE%

:run_gui
if exist "%WPF_EXE%" (
    echo 正在启动 ExcelConvert...
    start "" "%WPF_EXE%"
    exit /b 0
)

echo [提示] 未找到 excelConvert.exe，当前改为执行命令行导表。
goto run_export

@echo off
chcp 65001 >nul
setlocal EnableExtensions

title ExcelConvert 工具

set "EXPORTER_EXE=%~dp0Tools\excelConvert\ExcelExporter\publish\ExcelExporter.exe"
set "EXPORTER_PROJECT=%~dp0Tools\excelConvert\ExcelExporter\ExcelExporter.csproj"
set "EXPORTER_PUBLISH_DIR=%~dp0Tools\excelConvert\ExcelExporter\publish"

set "WPF_EXE=%~dp0Tools\excelConvert\excelConvert\publish\excelConvert.exe"
set "WPF_PROJECT=%~dp0Tools\excelConvert\excelConvert\excelConvert.csproj"
set "WPF_PUBLISH_DIR=%~dp0Tools\excelConvert\excelConvert\publish"

set "EQUIPMENT_EXE=%~dp0Tools\GenEquipmentExcel\bin\Debug\net9.0\GenEquipmentExcel.exe"
set "PYTHON_EXPORTER=%~dp0Tools\excelConvert\export_excel_to_cfg.py"

set "DOTNET_AVAILABLE="
where dotnet >nul 2>nul
if not errorlevel 1 (
    set "DOTNET_AVAILABLE=1"
)

set "EXPORT_MODE="
set "EXPORTER_NAME="
set "GUI_MODE="
set "GUI_NAME="

if exist "%EXPORTER_EXE%" (
    set "EXPORT_MODE=PUBLISHED_EXPORTER"
    set "EXPORTER_NAME=ExcelExporter（发布产物）"
) else (
    if exist "%EXPORTER_PROJECT%" (
        if defined DOTNET_AVAILABLE (
            set "EXPORT_MODE=SOURCE_EXPORTER"
            set "EXPORTER_NAME=ExcelExporter（源码仓库，自动发布）"
        )
    )
)

if not defined EXPORT_MODE (
    if exist "%WPF_EXE%" (
        set "EXPORT_MODE=PUBLISHED_WPF_EXPORT"
        set "EXPORTER_NAME=excelConvert（发布产物，--export-all）"
    ) else (
        if exist "%WPF_PROJECT%" (
            if defined DOTNET_AVAILABLE (
                set "EXPORT_MODE=SOURCE_WPF_EXPORT"
                set "EXPORTER_NAME=excelConvert（源码仓库，自动发布后 --export-all）"
            )
        )
    )
)

if exist "%WPF_EXE%" (
    set "GUI_MODE=PUBLISHED_GUI"
    set "GUI_NAME=excelConvert（发布产物）"
) else (
    if exist "%WPF_PROJECT%" (
        if defined DOTNET_AVAILABLE (
            set "GUI_MODE=SOURCE_GUI"
            set "GUI_NAME=excelConvert（源码仓库，自动发布）"
        )
    )
)

if not defined EXPORT_MODE (
    if exist "%EQUIPMENT_EXE%" if exist "%PYTHON_EXPORTER%" (
        set "EXPORT_MODE=FALLBACK_PIPELINE"
        set "EXPORTER_NAME=FallbackPipeline（仓库内降级链）"
    )
)

if not defined EXPORT_MODE (
    if not defined GUI_MODE (
        echo [错误] 未找到可用的导表程序。
        echo.
        echo 已尝试以下路径：
        echo   发布版命令行：%EXPORTER_EXE%
        echo   源码命令行工程：%EXPORTER_PROJECT%
        echo   发布版界面：%WPF_EXE%
        echo   源码界面工程：%WPF_PROJECT%
        echo   降级链程序：%EQUIPMENT_EXE%
        echo   降级链脚本：%PYTHON_EXPORTER%
        if not defined DOTNET_AVAILABLE (
            echo.
            echo [提示] 当前未检测到 dotnet，源码仓库模式无法自动发布。
        )
        echo.
        pause
        exit /b 1
    )
)

if defined EXPORTER_NAME echo 检测到可用导表方式：%EXPORTER_NAME%
if defined GUI_NAME echo 检测到可用界面方式：%GUI_NAME%
if /i "%EXPORT_MODE%"=="FALLBACK_PIPELINE" (
    echo [提示] 未检测到可直接运行的 ExcelExporter / excelConvert，当前将自动降级为仓库内全量导表链。
)

echo.
echo 请选择操作：
if defined EXPORT_MODE (
    echo   1. 一键批量导出所有 Excel（推荐）
) else (
    echo   1. 一键批量导出所有 Excel（当前不可用）
)
if defined GUI_MODE (
    echo   2. 打开 ExcelConvert 界面
) else (
    echo   2. 打开 ExcelConvert 界面（未提供时自动降级为命令行导表）
)
echo.
set /p choice=请输入选项 (1/2): 

if "%choice%"=="1" goto run_export
if "%choice%"=="2" goto run_gui

echo [错误] 请输入 1 或 2
pause
exit /b 1

:run_export
if not defined EXPORT_MODE (
    echo [错误] 当前没有可用的命令行导表方式。
    echo.
    pause
    exit /b 1
)

if /i "%EXPORT_MODE%"=="PUBLISHED_EXPORTER" (
    echo 正在执行导表：%EXPORTER_NAME%...
    "%EXPORTER_EXE%"
    set "EXIT_CODE=%ERRORLEVEL%"
    echo.
    pause
    exit /b %EXIT_CODE%
)

if /i "%EXPORT_MODE%"=="SOURCE_EXPORTER" (
    call :publish_project "%EXPORTER_PROJECT%" "%EXPORTER_PUBLISH_DIR%" "%EXPORTER_EXE%" "ExcelExporter"
    if errorlevel 1 (
        set "EXIT_CODE=%ERRORLEVEL%"
        echo.
        pause
        exit /b %EXIT_CODE%
    )
    echo 正在执行导表：%EXPORTER_NAME%...
    "%EXPORTER_EXE%"
    set "EXIT_CODE=%ERRORLEVEL%"
    echo.
    pause
    exit /b %EXIT_CODE%
)

if /i "%EXPORT_MODE%"=="PUBLISHED_WPF_EXPORT" (
    echo 正在执行导表：%EXPORTER_NAME%...
    "%WPF_EXE%" --export-all
    set "EXIT_CODE=%ERRORLEVEL%"
    echo.
    pause
    exit /b %EXIT_CODE%
)

if /i "%EXPORT_MODE%"=="SOURCE_WPF_EXPORT" (
    call :publish_project "%WPF_PROJECT%" "%WPF_PUBLISH_DIR%" "%WPF_EXE%" "excelConvert"
    if errorlevel 1 (
        set "EXIT_CODE=%ERRORLEVEL%"
        echo.
        pause
        exit /b %EXIT_CODE%
    )
    echo 正在执行导表：%EXPORTER_NAME%...
    "%WPF_EXE%" --export-all
    set "EXIT_CODE=%ERRORLEVEL%"
    echo.
    pause
    exit /b %EXIT_CODE%
)

echo 正在刷新 equipment.xlsx...
"%EQUIPMENT_EXE%"
if errorlevel 1 (
    set "EXIT_CODE=%ERRORLEVEL%"
    echo [错误] 刷新 equipment.xlsx 失败，错误码：%EXIT_CODE%
    echo.
    pause
    exit /b %EXIT_CODE%
)

echo 正在导出所有 Excel 到 Assets\Cfg...
python "%PYTHON_EXPORTER%"
set "EXIT_CODE=%ERRORLEVEL%"
echo.
pause
exit /b %EXIT_CODE%

:run_gui
if /i "%GUI_MODE%"=="PUBLISHED_GUI" (
    echo 正在启动 ExcelConvert...
    start "" "%WPF_EXE%"
    exit /b 0
)

if /i "%GUI_MODE%"=="SOURCE_GUI" (
    call :publish_project "%WPF_PROJECT%" "%WPF_PUBLISH_DIR%" "%WPF_EXE%" "excelConvert"
    if errorlevel 1 (
        set "EXIT_CODE=%ERRORLEVEL%"
        echo.
        pause
        exit /b %EXIT_CODE%
    )
    echo 正在启动 ExcelConvert...
    start "" "%WPF_EXE%"
    exit /b 0
)

echo [提示] 当前未找到可用的 ExcelConvert 界面，改为执行命令行导表。
goto run_export

:publish_project
set "PROJECT_FILE=%~1"
set "PUBLISH_DIR=%~2"
set "EXPECTED_OUTPUT=%~3"
set "PROJECT_NAME=%~4"

echo 正在从源码仓库发布 %PROJECT_NAME%...
dotnet publish "%PROJECT_FILE%" -c Release -o "%PUBLISH_DIR%"
set "PUBLISH_EXIT_CODE=%ERRORLEVEL%"
if not "%PUBLISH_EXIT_CODE%"=="0" (
    echo [错误] 发布 %PROJECT_NAME% 失败，错误码：%PUBLISH_EXIT_CODE%
    exit /b %PUBLISH_EXIT_CODE%
)

if exist "%EXPECTED_OUTPUT%" exit /b 0

echo [错误] %PROJECT_NAME% 发布完成，但未找到预期输出文件：
echo   %EXPECTED_OUTPUT%
exit /b 1

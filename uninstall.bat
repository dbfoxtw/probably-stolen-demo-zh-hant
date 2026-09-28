@echo off
rem Remove the mod (MelonLoader itself is left alone).
rem This file is ASCII-only on purpose: cmd.exe parses .bat files byte by byte,
rem and non-ASCII text (UTF-8 or Big5) can break the parser.
setlocal
cd /d "%~dp0"
chcp 65001 >nul
set PYTHONUTF8=1
set VPY=.venv\Scripts\python.exe
if not exist "%VPY%" (
    where py >nul 2>nul
    if not errorlevel 1 (
        py -3 -m venv .venv
    ) else (
        python -m venv .venv
    )
)
if not exist "%VPY%" (
    echo Python 3.10+ is required: https://www.python.org/downloads/
    goto end
)
"%VPY%" -c "import opencc" >nul 2>nul
if errorlevel 1 "%VPY%" -m pip install -q -r requirements.txt
"%VPY%" tools\install.py uninstall %*
:end
echo.
pause

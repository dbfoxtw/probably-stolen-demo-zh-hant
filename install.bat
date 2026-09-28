@echo off
rem Build the mod from source and install it (needs MelonLoader 0.7.3 and one game start first; see README.md).
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
"%VPY%" tools\install.py install %*
:end
echo.
pause

@echo off
rem 從原始碼建置 mod 並安裝到遊戲（需先裝好 MelonLoader 0.7.3 並啟動過一次遊戲，見 README.md）
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
    echo 需要 Python 3.10 以上：https://www.python.org/downloads/
    goto end
)
"%VPY%" -c "import opencc" >nul 2>nul
if errorlevel 1 "%VPY%" -m pip install -q -r requirements.txt
"%VPY%" tools\install.py install %*
:end
echo.
pause

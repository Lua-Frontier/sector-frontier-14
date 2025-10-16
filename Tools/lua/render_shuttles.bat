@echo off
chcp 65001 >nul
echo ========================================
echo Автоматический рендеринг шаттлов Lua и Mono
echo ========================================
echo.

python --version >nul 2>&1
if errorlevel 1 (
    echo X Ошибка: Python не найден!
    echo Установите Python 3.7+ и добавьте в PATH
    pause
    exit /b 1
)

echo Запускаем скрипт рендеринга...
python render_shuttles.py

echo.
echo Рендеринг завершен!
pause

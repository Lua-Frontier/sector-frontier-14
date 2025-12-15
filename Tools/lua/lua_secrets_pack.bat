@echo off
setlocal enabledelayedexpansion
pushd "%~dp0\..\.."

set "SECRETS_DIR=lua-secrets"

if not exist "%SECRETS_DIR%\Resources" (
    echo [ERROR] "%SECRETS_DIR%\Resources" not found. Make sure lua-secrets is checked out in the repo root.
    goto :end
)

echo [INFO] Packing Resources back into lua-secrets...

if not exist "%SECRETS_DIR%\Resources\Audio\_Lua" mkdir "%SECRETS_DIR%\Resources\Audio\_Lua"
xcopy "Resources\Audio\_LuaSecrets\*" "%SECRETS_DIR%\Resources\Audio\_Lua\" /E /I /Y >nul

if not exist "%SECRETS_DIR%\Resources\Prototypes\_Lua" mkdir "%SECRETS_DIR%\Resources\Prototypes\_Lua"
xcopy "Resources\Prototypes\_LuaSecrets\*" "%SECRETS_DIR%\Resources\Prototypes\_Lua\" /E /I /Y >nul

if not exist "%SECRETS_DIR%\Resources\Locale\_Lua" mkdir "%SECRETS_DIR%\Resources\Locale\_Lua"
xcopy "Resources\Locale\ru-RU\_LuaSecrets\*" "%SECRETS_DIR%\Resources\Locale\_Lua\" /E /I /Y >nul

if not exist "%SECRETS_DIR%\Resources\Textures\_Lua" mkdir "%SECRETS_DIR%\Resources\Textures\_Lua"
xcopy "Resources\Textures\_LuaSecrets\*" "%SECRETS_DIR%\Resources\Textures\_Lua\" /E /I /Y >nul

echo [INFO] Cleaning _LuaSecrets folders from main repo...

if exist "Resources\Audio\_LuaSecrets" rmdir /S /Q "Resources\Audio\_LuaSecrets"
if exist "Resources\Prototypes\_LuaSecrets" rmdir /S /Q "Resources\Prototypes\_LuaSecrets"
if exist "Resources\Locale\ru-RU\_LuaSecrets" rmdir /S /Q "Resources\Locale\ru-RU\_LuaSecrets"
if exist "Resources\Textures\_LuaSecrets" rmdir /S /Q "Resources\Textures\_LuaSecrets"

echo [INFO] Pack completed.

:end
popd
endlocal
exit /b 0



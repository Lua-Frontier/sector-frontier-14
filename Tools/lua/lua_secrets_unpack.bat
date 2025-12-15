@echo off
setlocal enabledelayedexpansion

pushd "%~dp0\..\.."

set "SECRETS_DIR=lua-secrets"

if not exist "%SECRETS_DIR%\Resources" (
    echo [ERROR] "%SECRETS_DIR%\Resources" not found. Make sure lua-secrets is checked out in the repo root.
    goto :end
)

echo [INFO] Unpacking lua-secrets into Resources...

if not exist "Resources\Audio\_LuaSecrets" mkdir "Resources\Audio\_LuaSecrets"
xcopy "%SECRETS_DIR%\Resources\Audio\_Lua\*" "Resources\Audio\_LuaSecrets\" /E /I /Y >nul

if not exist "Resources\Prototypes\_LuaSecrets" mkdir "Resources\Prototypes\_LuaSecrets"
xcopy "%SECRETS_DIR%\Resources\Prototypes\_Lua\*" "Resources\Prototypes\_LuaSecrets\" /E /I /Y >nul

if not exist "Resources\Locale\ru-RU\_LuaSecrets" mkdir "Resources\Locale\ru-RU\_LuaSecrets"
xcopy "%SECRETS_DIR%\Resources\Locale\_Lua\*" "Resources\Locale\ru-RU\_LuaSecrets\" /E /I /Y >nul

if not exist "Resources\Textures\_LuaSecrets" mkdir "Resources\Textures\_LuaSecrets"
xcopy "%SECRETS_DIR%\Resources\Textures\_Lua\*" "Resources\Textures\_LuaSecrets\" /E /I /Y >nul

echo [INFO] Unpack completed.

:end
popd
endlocal
exit /b 0



import os
import sys
import shutil
import time
import base64
import subprocess
import requests
import xml.etree.ElementTree as ET
from colorama import init, Fore, Style

init()

ENV_KEYS = ("cdnUrl", "updateToken", "privateUsername", "privatePassword", "githubToken")
CONTENT_ZIP_PREFIXES = ("SS14.Client", "SS14.Server_")


def backup_bin_directory(bin_path):
    if os.path.exists(bin_path):
        backup_path = bin_path + "Backup"
        if os.path.exists(backup_path):
            print(Fore.RED + "Папка binBackup уже существует. Пожалуйста, проверьте содержимое или удалите её вручную." + Style.RESET_ALL)
            sys.exit(1)
        max_retries = 3
        retry_count = 0
        success = False
        while (not success) and (retry_count < max_retries):
            try:
                os.rename(bin_path, backup_path)
                success = True
            except Exception as e:
                retry_count += 1
                print(Fore.RED + f"Ошибка при переименовании папки bin в binBackup. Попытка {retry_count} из {max_retries}." + Style.RESET_ALL)
                time.sleep(5)
        if not success:
            print(Fore.RED + f"Не удалось переименовать папку bin в binBackup после {max_retries} попыток. Скрипт остановлен." + Style.RESET_ALL)
            sys.exit(1)
        print(Fore.YELLOW + "Папка bin успешно переименована в binBackup." + Style.RESET_ALL)
    else:
        print(Fore.YELLOW + "Папка bin не найдена." + Style.RESET_ALL)


def restore_bin_directory(bin_path):
    backup_path = bin_path + "Backup"
    if os.path.exists(bin_path):
        print(Fore.YELLOW + "Удаление папки bin..." + Style.RESET_ALL)
        shutil.rmtree(bin_path, ignore_errors=True)
        while os.path.exists(bin_path):
            time.sleep(1)
        print(Fore.GREEN + "Папка bin удалена." + Style.RESET_ALL)
    if os.path.exists(backup_path):
        os.rename(backup_path, bin_path)
        print(Fore.GREEN + "Папка binBackup переименована обратно в bin." + Style.RESET_ALL)
    else:
        print(Fore.RED + "Резервная папка binBackup не найдена." + Style.RESET_ALL)


def read_config_file(file_path):
    if not os.path.exists(file_path):
        print(Fore.RED + f"Файл конфигурации не найден: {file_path}" + Style.RESET_ALL)
        return None
    config = {}
    try:
        with open(file_path, encoding="utf-8") as f:
            for line_no, line in enumerate(f, start=1):
                line = line.strip()
                if not line or line.startswith("#"):
                    continue
                if "=" not in line:
                    raise Exception(f"Ошибка в строке {line_no}: {line}")
                key, value = line.split("=", 1)
                config[key.strip()] = value.strip().strip('"').strip("'")
        return config
    except Exception as e:
        print(Fore.RED + f"Ошибка при чтении файла конфигурации: {e}" + Style.RESET_ALL)
        return None


def get_engine_version(file_path):
    if not os.path.exists(file_path):
        print(Fore.RED + f"Файл версии движка не найден по пути {file_path}." + Style.RESET_ALL)
        return None
    try:
        tree = ET.parse(file_path)
        root = tree.getroot()
        for elem in root.iter():
            if elem.tag == "Version" or elem.tag.endswith("}Version"):
                return elem.text
        return None
    except Exception as e:
        print(Fore.RED + f"Ошибка при чтении версии движка: {e}" + Style.RESET_ALL)
        return None


def run_subprocess(command, cwd=None):
    result = subprocess.run(command, shell=True, cwd=cwd)
    if result.returncode != 0:
        raise Exception(f"Команда {command} завершилась с ошибкой.")
    return result


def copy_directory_contents(src_dir, dst_dir):
    if not os.path.exists(src_dir):
        print(Fore.YELLOW + f"Источник {src_dir} не найден, пропуск копирования." + Style.RESET_ALL)
        return

    os.makedirs(dst_dir, exist_ok=True)

    for name in os.listdir(src_dir):
        src_path = os.path.join(src_dir, name)
        dst_path = os.path.join(dst_dir, name)
        if os.path.isdir(src_path):
            if os.path.exists(dst_path):
                copy_directory_contents(src_path, dst_path)
            else:
                shutil.copytree(src_path, dst_path)
        else:
            shutil.copy2(src_path, dst_path)


def setup_lua_secrets(root_path):
    print(Fore.YELLOW + "Проверка и загрузка lua-secrets..." + Style.RESET_ALL)
    candidates = ["LuaSecrets", "lua-secrets"]
    lua_secrets_dir = None
    for name in candidates:
        candidate_path = os.path.join(root_path, name)
        if os.path.isdir(candidate_path):
            lua_secrets_dir = candidate_path
            break
    if lua_secrets_dir is None:
        lua_secrets_dir = os.path.join(root_path, "lua-secrets")
        try:
            print(Fore.YELLOW + "Репозиторий lua-secrets не найден, выполняю git clone..." + Style.RESET_ALL)
            run_subprocess(
                "git clone https://github.com/HacksLua/lua-secrets.git lua-secrets",
                cwd=root_path
            )
            print(Fore.GREEN + "Репозиторий lua-secrets успешно клонирован." + Style.RESET_ALL)
        except Exception as e:
            print(Fore.RED + f"Не удалось клонировать lua-secrets: {e}" + Style.RESET_ALL)
            return []

    mappings = [
        (os.path.join("Resources", "Audio", "_Lua"),      os.path.join("Resources", "Audio", "_LuaSecrets")),
        (os.path.join("Resources", "Prototypes", "_Lua"), os.path.join("Resources", "Prototypes", "_LuaSecrets")),
        (os.path.join("Resources", "Locale", "_Lua"),     os.path.join("Resources", "Locale", "ru-RU", "_LuaSecrets")),
        (os.path.join("Resources", "Textures", "_Lua"),   os.path.join("Resources", "Textures", "_LuaSecrets")),
    ]

    created_dirs = []

    for rel_src, rel_dst in mappings:
        src_dir = os.path.join(lua_secrets_dir, rel_src)
        dst_dir = os.path.join(root_path, rel_dst)
        print(Fore.YELLOW + f"Копирование lua-secrets из {src_dir} в {dst_dir}..." + Style.RESET_ALL)
        copy_directory_contents(src_dir, dst_dir)
        created_dirs.append(dst_dir)
    print(Fore.GREEN + "lua-secrets успешно подготовлен." + Style.RESET_ALL)
    return created_dirs


def cleanup_lua_secrets_dirs(dirs):
    if not dirs:
        return
    print(Fore.YELLOW + "Удаление временных директорий lua-secrets..." + Style.RESET_ALL)
    for dst_dir in set(dirs):
        if os.path.exists(dst_dir):
            shutil.rmtree(dst_dir, ignore_errors=True)

    print(Fore.GREEN + "Временные директории lua-secrets удалены." + Style.RESET_ALL)


def trigger_github_action(github_token, repo_owner, repo_name, workflow_file, ref="master"):
    try:
        url = f"https://api.github.com/repos/{repo_owner}/{repo_name}/actions/workflows/{workflow_file}/dispatches"
        headers = {
            "Accept": "application/vnd.github.v3+json",
            "Authorization": f"token {github_token}",
            "Content-Type": "application/json"
        }
        payload = {"ref": ref}

        print(Fore.YELLOW + f"Запуск GitHub Action: {workflow_file}..." + Style.RESET_ALL)
        response = requests.post(url, headers=headers, json=payload)

        if response.status_code == 204:
            print(Fore.GREEN + f"GitHub Action {workflow_file} успешно запущен." + Style.RESET_ALL)
            return True
        else:
            print(Fore.RED + f"Ошибка при запуске GitHub Action: {response.status_code} - {response.text}" + Style.RESET_ALL)
            return False

    except Exception as e:
        print(Fore.RED + f"Ошибка при запуске GitHub Action: {e}" + Style.RESET_ALL)
        return False


def resolve_paths():
    if getattr(sys, "frozen", False):
        tools_path = os.path.dirname(sys.executable)
    else:
        tools_path = os.path.dirname(os.path.abspath(__file__))

    root_path = os.path.abspath(os.path.join(tools_path, ".."))
    return tools_path, root_path


def main():
    tools_path, root_path = resolve_paths()
    os.chdir(root_path)

    env_path = os.path.join(tools_path, ".env")
    bin_path = os.path.join(root_path, "bin")
    backup_bin_directory(bin_path)

    lua_secrets_dirs = []
    release_dir = os.path.join(root_path, "release")
    temp_client_dir = os.path.join(root_path, "temp_client")

    config = read_config_file(env_path)
    if config is None:
        print(Fore.YELLOW + f"Ожидается конфиг: {env_path}" + Style.RESET_ALL)
        print(Fore.YELLOW + "Скопируйте Tools/.env.example -> Tools/.env и заполните токены." + Style.RESET_ALL)
        restore_bin_directory(bin_path)
        sys.exit(1)

    missing_keys = [key for key in ENV_KEYS if key not in config or not config[key]]
    if missing_keys:
        print(Fore.RED + "Следующие параметры отсутствуют в .env:" + Style.RESET_ALL)
        for key in missing_keys:
            print(Fore.RED + f"- {key}" + Style.RESET_ALL)
        restore_bin_directory(bin_path)
        return

    cdn_url = config["cdnUrl"].rstrip("/")
    update_token = config["updateToken"]
    private_username = config["privateUsername"]
    private_password = config["privatePassword"]
    github_token = config["githubToken"]

    version = time.strftime("%d%m%Y%H%M%S")
    engine_version_file = os.path.join(root_path, "RobustToolbox", "MSBuild", "Robust.Engine.Version.props")
    if not os.path.exists(engine_version_file):
        print(Fore.RED + f"Не удалось найти файл версии движка: {engine_version_file}" + Style.RESET_ALL)
        restore_bin_directory(bin_path)
        return

    engine_version = None
    try:
        with open(engine_version_file, encoding="utf-8") as f:
            for line in f:
                if "<Version>" in line and "</Version>" in line:
                    start = line.find("<Version>") + len("<Version>")
                    end = line.find("</Version>", start)
                    engine_version = line[start:end].strip()
                    break
    except Exception as e:
        print(Fore.RED + f"Ошибка при чтении версии движка: {e}" + Style.RESET_ALL)
    if engine_version is None:
        engine_version = get_engine_version(engine_version_file)

    print(Fore.GREEN + f"Repo root: {root_path}" + Style.RESET_ALL)
    print(Fore.GREEN + f"Установлена версия: {version}" + Style.RESET_ALL)
    print(Fore.GREEN + f"Установлен движок: {engine_version}" + Style.RESET_ALL)

    try:
        lua_secrets_dirs = setup_lua_secrets(root_path)
        print(Fore.GREEN + "Начало сборки..." + Style.RESET_ALL)

        print(Fore.YELLOW + "Проверка доступности Robust.Cdn..." + Style.RESET_ALL)
        auth_header = {
            "Authorization": "Basic " + base64.b64encode(f"{private_username}:{private_password}".encode("utf-8")).decode("utf-8")
        }
        response = requests.get(f"{cdn_url}/manifest", headers=auth_header)
        response.raise_for_status()
        print(Fore.GREEN + "Robust.Cdn доступен." + Style.RESET_ALL)

        for dir_path in [release_dir, temp_client_dir]:
            if os.path.exists(dir_path):
                shutil.rmtree(dir_path, ignore_errors=True)
            os.makedirs(dir_path, exist_ok=True)

        print(Fore.GREEN + "Сборка клиентского пакета..." + Style.RESET_ALL)
        run_subprocess("dotnet build Content.Packaging --configuration Release")
        run_subprocess("dotnet run --project Content.Packaging client --no-wipe-release")

        print(Fore.YELLOW + "Перемещение клиентских файлов..." + Style.RESET_ALL)
        for item in os.listdir(release_dir):
            if item.startswith("SS14.Client"):
                shutil.move(os.path.join(release_dir, item), os.path.join(temp_client_dir, item))

        print(Fore.GREEN + "Сборка серверных пакетов..." + Style.RESET_ALL)
        run_subprocess("dotnet run --project Content.Packaging server --platform linux-x64")

        print(Fore.YELLOW + "Возврат клиентских файлов..." + Style.RESET_ALL)
        for item in os.listdir(temp_client_dir):
            shutil.move(os.path.join(temp_client_dir, item), os.path.join(release_dir, item))
        shutil.rmtree(temp_client_dir, ignore_errors=True)

        print(Fore.YELLOW + "Повторная проверка доступности Robust.Cdn..." + Style.RESET_ALL)
        response = requests.get(f"{cdn_url}/manifest", headers=auth_header)
        response.raise_for_status()
        print(Fore.GREEN + "Robust.Cdn доступен." + Style.RESET_ALL)

        print(Fore.GREEN + "Начало публикации в CDN..." + Style.RESET_ALL)
        headers = {
            "Authorization": f"Bearer {update_token}",
            "Content-Type": "application/json"
        }
        payload = {"version": version, "engineVersion": engine_version}
        response = requests.post(f"{cdn_url}/publish/start", headers=headers, json=payload)
        response.raise_for_status()
        print(Fore.GREEN + "Публикация успешно начата." + Style.RESET_ALL)

        print(Fore.GREEN + "Загрузка файлов..." + Style.RESET_ALL)
        for filename in os.listdir(release_dir):
            if not filename.endswith(".zip"):
                continue
            if not filename.startswith(CONTENT_ZIP_PREFIXES):
                print(Fore.YELLOW + f"Пропуск (не content): {filename}" + Style.RESET_ALL)
                continue
            file_path = os.path.join(release_dir, filename)
            try:
                with open(file_path, "rb") as f:
                    file_content = f.read()
                file_headers = {
                    "Authorization": f"Bearer {update_token}",
                    "Robust-Cdn-Publish-File": filename,
                    "Robust-Cdn-Publish-Version": version,
                    "Content-Type": "application/octet-stream"
                }
                file_response = requests.post(f"{cdn_url}/publish/file", headers=file_headers, data=file_content)
                file_response.raise_for_status()
                print(Fore.GREEN + f"{filename} успешно загружен." + Style.RESET_ALL)
            except Exception as e:
                print(Fore.RED + f"Ошибка при загрузке {filename}: {e}" + Style.RESET_ALL)

        print(Fore.GREEN + "Завершение публикации..." + Style.RESET_ALL)
        finish_headers = {
            "Authorization": f"Bearer {update_token}",
            "Content-Type": "application/json"
        }
        finish_payload = {"version": version}
        finish_response = requests.post(f"{cdn_url}/publish/finish", headers=finish_headers, json=finish_payload)
        finish_response.raise_for_status()
        print(Fore.GREEN + "Публикация успешно завершена." + Style.RESET_ALL)

        github_success = trigger_github_action(
            github_token=github_token,
            repo_owner="Lua-Frontier",
            repo_name="sector-frontier-14",
            workflow_file="publish-changelog.yml",
            ref="master"
        )

        if github_success:
            print(Fore.GREEN + "GitHub Action для публикации changelog успешно запущен." + Style.RESET_ALL)
        else:
            print(Fore.YELLOW + "Предупреждение: Не удалось запустить GitHub Action для публикации changelog." + Style.RESET_ALL)

        print(Fore.YELLOW + "Очистка временных файлов..." + Style.RESET_ALL)
        if os.path.exists(release_dir):
            shutil.rmtree(release_dir, ignore_errors=True)
        print(Fore.GREEN + "Очистка завершена." + Style.RESET_ALL)
        print(Fore.GREEN + "Сборка завершена." + Style.RESET_ALL)
    except Exception as main_e:
        print(Fore.RED + f"Ошибка при сборке: {main_e}" + Style.RESET_ALL)
    finally:
        print(Fore.YELLOW + "Удаление временных папок..." + Style.RESET_ALL)
        for dir_path in [release_dir, temp_client_dir]:
            if os.path.exists(dir_path):
                shutil.rmtree(dir_path, ignore_errors=True)
        print(Fore.GREEN + "Временные папки удалены." + Style.RESET_ALL)

        cleanup_lua_secrets_dirs(lua_secrets_dirs)

        restore_bin_directory(bin_path)
        os.chdir(tools_path)

    input(Fore.YELLOW + "Нажмите Enter для выхода..." + Style.RESET_ALL)


if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        print(Fore.RED + f"Произошла ошибка: {e}" + Style.RESET_ALL)
        input(Fore.YELLOW + "Нажмите Enter для выхода..." + Style.RESET_ALL)

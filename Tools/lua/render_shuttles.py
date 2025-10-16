#!/usr/bin/env python3

import os
import subprocess
import sys
import shutil
import json
import time
import hashlib
from pathlib import Path
from typing import List, Tuple, Dict
from datetime import datetime

RENDER_INFO_FILE = "render_shuttles_info.json"
ERROR_LOG_FILE = "render_shuttles_errors.log"

def get_project_root() -> str:
    script_dir = os.path.dirname(os.path.abspath(__file__))
    project_root = os.path.dirname(os.path.dirname(script_dir))
    return project_root

def calculate_file_hash(file_path: str) -> str:
    try:
        hash_md5 = hashlib.md5()
        with open(file_path, "rb") as f:
            for chunk in iter(lambda: f.read(4096), b""):
                hash_md5.update(chunk)
        return hash_md5.hexdigest()
    except Exception as e:
        print(f"!!! Ошибка вычисления хеша для {file_path}: {e}")
        return ""

def log_error(error_msg: str, shuttle_name: str = "", details: str = ""):
    try:
        log_path = os.path.join(os.path.dirname(__file__), ERROR_LOG_FILE)
        timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')

        with open(log_path, 'a', encoding='utf-8') as f:
            f.write(f"[{timestamp}] {error_msg}")
            if shuttle_name:
                f.write(f" | Шаттл: {shuttle_name}")
            if details:
                f.write(f" | Детали: {details}")
            f.write("\n")

    except Exception as e:
        print(f"!!! Ошибка записи в лог: {e}")

def load_render_info() -> Dict[str, Dict]:
    try:
        info_path = os.path.join(os.path.dirname(__file__), RENDER_INFO_FILE)
        if os.path.exists(info_path):
            with open(info_path, 'r', encoding='utf-8') as f:
                data = json.load(f)
                if data and isinstance(next(iter(data.values())), (int, float)):
                    new_data = {}
                    for path, timestamp in data.items():
                        new_data[path] = {
                            "timestamp": timestamp,
                            "hash": "",
                            "size": 0
                        }
                    return new_data
                return data
    except Exception as e:
        print(f"!!!  Ошибка загрузки информации о рендеринге: {e}")
        log_error(f"Ошибка загрузки информации о рендеринге: {e}")

    return {}

def save_render_info(render_info: Dict[str, Dict]):
    try:
        info_path = os.path.join(os.path.dirname(__file__), RENDER_INFO_FILE)
        with open(info_path, 'w', encoding='utf-8') as f:
            json.dump(render_info, f, indent=2, ensure_ascii=False)
        print(f"V Информация о рендеринге сохранена в {info_path}")
    except Exception as e:
        print(f"X Ошибка сохранения информации о рендеринге: {e}")
        log_error(f"Ошибка сохранения информации о рендеринге: {e}")

def get_file_info(file_path: str) -> Dict[str, any]:
    try:
        stat = os.stat(file_path)
        return {
            "timestamp": stat.st_mtime,
            "size": stat.st_size,
            "hash": calculate_file_hash(file_path)
        }
    except Exception as e:
        print(f"!!! Ошибка получения информации о файле {file_path}: {e}")
        return {"timestamp": 0, "size": 0, "hash": ""}

def find_shuttle_files(project_root: str) -> List[Tuple[str, str, str]]:
    shuttle_files = []
    shuttle_paths = [
        os.path.join(project_root, "Resources", "Maps", "_Lua", "Shuttles"),
        os.path.join(project_root, "Resources", "Maps", "_Mono", "Shuttles")
    ]

    for base_path in shuttle_paths:
        if not os.path.exists(base_path):
            print(f"Папка {base_path} не найдена")
            continue

        print(f"Сканируем папку: {base_path}")
        for root, dirs, files in os.walk(base_path):
            for file in files:
                if file.endswith('.yml'):
                    full_path = os.path.join(root, file)
                    relative_path = os.path.relpath(full_path, os.path.join(project_root, "Resources", "Maps"))
                    name_without_ext = os.path.splitext(file)[0]

                    shuttle_files.append((relative_path, name_without_ext, full_path))
                    print(f"  Найден шаттл: {relative_path} -> {name_without_ext}")

    return shuttle_files

def check_files_for_changes(shuttle_files: List[Tuple[str, str, str]], render_info: Dict[str, Dict]) -> List[Tuple[str, str, str]]:
    changed_files = []
    unchanged_count = 0

    print(f"\nПроверяем изменения в файлах...")

    for relative_path, name, full_path in shuttle_files:
        current_info = get_file_info(full_path)
        last_info = render_info.get(relative_path, {})

        timestamp_changed = current_info["timestamp"] > last_info.get("timestamp", 0)
        size_changed = current_info["size"] != last_info.get("size", 0)
        hash_changed = current_info["hash"] != last_info.get("hash", "")

        if timestamp_changed or size_changed or hash_changed:
            changed_files.append((relative_path, name, full_path))

            changes = []
            if timestamp_changed:
                last_time = datetime.fromtimestamp(last_info.get("timestamp", 0)).strftime('%Y-%m-%d %H:%M:%S') if last_info.get("timestamp", 0) > 0 else 'никогда'
                changes.append(f"время: {last_time} -> {datetime.fromtimestamp(current_info['timestamp']).strftime('%Y-%m-%d %H:%M:%S')}")
            if size_changed:
                last_size = last_info.get("size", 0)
                changes.append(f"размер: {last_size} -> {current_info['size']} байт")
            if hash_changed:
                last_hash = last_info.get("hash", "")[:8] if last_info.get("hash") else "нет"
                current_hash = current_info["hash"][:8] if current_info["hash"] else "ошибка"
                changes.append(f"хеш: {last_hash} -> {current_hash}")

            print(f"  +- Изменен: {name} ({', '.join(changes)})")
        else:
            unchanged_count += 1
            last_time = datetime.fromtimestamp(last_info.get("timestamp", 0)).strftime('%Y-%m-%d %H:%M:%S') if last_info.get("timestamp", 0) > 0 else 'никогда'
            print(f"  V Не изменен: {name} (последний рендер: {last_time})")

    print(f"\n#### Статистика изменений:")
    print(f"  Всего файлов: {len(shuttle_files)}")
    print(f"  Изменено: {len(changed_files)}")
    print(f"  Не изменено: {unchanged_count}")

    return changed_files

def render_shuttle(shuttle_path: str, output_dir: str, project_root: str) -> Tuple[bool, str]:
    try:
        maprender_exe = os.path.join(project_root, "bin", "Content.MapRenderer", "Content.MapRenderer.exe")
        cmd = [
            maprender_exe,
            "-o", output_dir,
            "-f", f"Maps/{shuttle_path}"
        ]

        print(f"Рендерим: {shuttle_path}")
        print(f"Команда: {' '.join(cmd)}")

        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            cwd=project_root,
            timeout=300
        )

        if result.returncode == 0:
            print(f"V Успешно отрендерен: {shuttle_path}")
            return True, ""
        else:
            error_msg = f"Ошибка рендеринга {shuttle_path}"
            print(f"X {error_msg}:")
            print(f"  stdout: {result.stdout}")
            print(f"  stderr: {result.stderr}")
            details = f"stdout: {result.stdout}, stderr: {result.stderr}"
            log_error(error_msg, shuttle_path, details)

            return False, f"Код ошибки: {result.returncode}"

    except subprocess.TimeoutExpired:
        error_msg = f"Таймаут при рендеринге {shuttle_path}"
        print(f"X {error_msg}")
        log_error(error_msg, shuttle_path, "Таймаут 5 минут")
        return False, "Таймаут 5 минут"

    except Exception as e:
        error_msg = f"Исключение при рендеринге {shuttle_path}"
        print(f"X {error_msg}: {e}")
        log_error(error_msg, shuttle_path, str(e))
        return False, str(e)

# Чисто мой прикол, вам яндекс не нужен
def copy_to_yandex_disk(tmp_dir: str) -> bool:
    try:
        yandex_path = os.path.expanduser(r"%USERPROFILE%\YandexDisk\Job\Render")
        if not os.path.exists(yandex_path):
            yandex_path = os.path.expanduser(r"~\YandexDisk\Job\Render")
        if not os.path.exists(yandex_path):
            print(f"!!!  Папка Яндекс.Диска не найдена: {yandex_path}")
            print("Результаты остаются в папке tmp")
            return False
        print(f"\nКопируем результаты в Яндекс.Диск: {yandex_path}")
        os.makedirs(yandex_path, exist_ok=True)
        for item in os.listdir(tmp_dir):
            src_path = os.path.join(tmp_dir, item)
            dst_path = os.path.join(yandex_path, item)

            if os.path.isdir(src_path):
                if os.path.exists(dst_path):
                    shutil.rmtree(dst_path)
                shutil.copytree(src_path, dst_path)
                print(f"  V Скопирована папка: {item}")
            else:
                shutil.copy2(src_path, dst_path)
                print(f"  V Скопирован файл: {item}")
        print(f"VVV Все результаты скопированы в Яндекс.Диск!")
        return True

    except Exception as e:
        print(f"X Ошибка при копировании в Яндекс.Диск: {e}")
        log_error(f"Ошибка при копировании в Яндекс.Диск: {e}")
        return False

def cleanup_tmp_files(tmp_dir: str):
    try:
        print(f"\nОчищаем временные файлы...")
        for i in range(1, 31):
            pattern = f"*-{i}.png"
            deleted_count = 0

            for root, dirs, files in os.walk(tmp_dir):
                for file in files:
                    if file.endswith(f"-{i}.png"):
                        file_path = os.path.join(root, file)
                        try:
                            os.remove(file_path)
                            deleted_count += 1
                        except:
                            pass

            if deleted_count > 0:
                print(f"  Удалено файлов {pattern}: {deleted_count}")

        print("V Временные файлы очищены")

    except Exception as e:
        print(f"!!!  Ошибка при очистке временных файлов: {e}")
        log_error(f"Ошибка при очистке временных файлов: {e}")

def update_render_info(changed_files: List[Tuple[str, str, str]], render_info: Dict[str, Dict]):
    current_time = time.time()

    for relative_path, name, full_path in changed_files:
        file_info = get_file_info(full_path)
        render_info[relative_path] = {
            "timestamp": current_time,
            "hash": file_info["hash"],
            "size": file_info["size"]
        }

    print(f"V Обновлена информация о времени рендеринга для {len(changed_files)} файлов")

def show_error_summary(failed_shuttles: List[Tuple[str, str, str, str]]):
    if not failed_shuttles:
        return

    print(f"\nX СВОДКА ПО ОШИБКАМ РЕНДЕРИНГА:")
    print(f"=" * 60)

    for i, (shuttle_path, shuttle_name, full_path, error_msg) in enumerate(failed_shuttles, 1):
        print(f"{i:2d}. {shuttle_name}")
        print(f"    Путь: {shuttle_path}")
        print(f"    Ошибка: {error_msg}")
        print()

    print(f"Всего ошибок: {len(failed_shuttles)}")
    print(f"Подробности записаны в файл: {ERROR_LOG_FILE}")
    print(f"=" * 60)

def main():
    print("=== Автоматический рендеринг шаттлов ===")
    project_root = get_project_root()
    print(f"Корневая папка проекта: {project_root}")
    print(f"Текущая рабочая директория: {os.getcwd()}")

    maprender_path = os.path.join(project_root, "bin", "Content.MapRenderer", "Content.MapRenderer.exe")
    if not os.path.exists(maprender_path):
        error_msg = f"Мапрендер не найден (билди проект!!): {maprender_path}"
        print(f"X Ошибка: {error_msg}")
        print("Убедитес, что проект собран и Мапрендер доступен")
        log_error(error_msg, "", f"Файл не найден: {maprender_path}")
        sys.exit(1)

    output_dir = os.path.join(project_root, "bin", "Content.MapRenderer", "tmp")
    os.makedirs(output_dir, exist_ok=True)
    print(f"Папка вывода: {output_dir}")

    render_info = load_render_info()
    print(f"Загружена информация о {len(render_info)} ранее отрендеренных файлах")

    print("\nПоиск файлов шаттлов...")
    shuttle_files = find_shuttle_files(project_root)

    if not shuttle_files:
        print("X Файлы шаттлов не найдены!")
        sys.exit(1)

    print(f"\nНайдено шаттлов: {len(shuttle_files)}")

    changed_files = check_files_for_changes(shuttle_files, render_info)

    if not changed_files:
        print(f"\nVVV Все шаттлы уже отрендерены и не изменились!")
        print("Никаких действий не требуется.")
        print(f"\n>>> Рендеренные шаттлы доступны <<<")
        print(f"  >>> Яндекс.Диск: https://disk.yandex.ru/d/AqwhAxgvM9oafQ")
        print(f"  >>> Если новые шаттлы, то локальная папка: {output_dir}")
        return

    successful = 0
    failed = 0
    failed_shuttles = []

    print(f"\nНачинаем рендеринг измененных файлов...")
    start_time = time.time()

    for i, (shuttle_path, shuttle_name, full_path) in enumerate(changed_files, 1):
        print(f"\n[{i}/{len(changed_files)}] Рендерим {shuttle_name}")

        success, error_msg = render_shuttle(shuttle_path, output_dir, project_root)

        if success:
            successful += 1
        else:
            failed += 1
            failed_shuttles.append((shuttle_path, shuttle_name, full_path, error_msg))

        time.sleep(1)

    end_time = time.time()
    total_time = end_time - start_time

    print(f"\n=== ИТОГИ РЕНДЕРИНГА ===")
    print(f"Всего измененных шаттлов: {len(changed_files)}")
    print(f"V Успешно: {successful}")
    print(f"X Ошибок: {failed}")
    print(f"Время выполнения: {total_time:.1f}")
    if failed_shuttles:
        show_error_summary(failed_shuttles)
        print(f"\n!!!  {failed} шаттлов не удалось отрендерить")
        print(f"Проверьте лог ошибок: {ERROR_LOG_FILE}")
    else:
        print(f"\nVVV Все измененные шаттлы успешно отрендерены!")
        print(f"Результаты сохранены в: {output_dir}")

    successful_files = [(path, name, full) for path, name, full in changed_files
                        if (path, name, full, "") not in failed_shuttles]

    if successful_files:
        update_render_info(successful_files, render_info)
        save_render_info(render_info)

    cleanup_tmp_files(output_dir)
    copy_to_yandex_disk(output_dir)

    print(f"\n>>> Рендеренные шаттлы доступны <<<")
    print(f"  >>> Яндекс.Диск: https://disk.yandex.ru/d/AqwhAxgvM9oafQ")
    print(f"  >>> Если новые шаттлы, то локальная папка: {output_dir}")

    if failed > 0:
        print(f"\n==> РЕЗЮМЕ:")
        print(f"V Успешно отрендерено: {successful} шаттлов")
        print(f"X Ошибок рендеринга: {failed} шаттлов")
        print(f"<=> Лог ошибок: {ERROR_LOG_FILE}")
        print(f"^^^ Информация о рендеринге: {RENDER_INFO_FILE}")
        sys.exit(1)
    else:
        print(f"\nVVV РЕНДЕРИНГ ЗАВЕРШЕН УСПЕШНО!")
        print(f"Все {successful} шаттлов отрендерены без ошибок!")

if __name__ == "__main__":
    main()

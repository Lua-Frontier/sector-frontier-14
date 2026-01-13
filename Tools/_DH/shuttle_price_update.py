import re
import os

# Текст ошибки
errors_text = """
"""

BASE_DIR = os.path.abspath(
    os.path.join(os.path.dirname(__file__), "..", "..")
)

# Парсинг ошибок
pattern = r"Arbitrage possible on (\w+)\. Minimal price should be ([\d.,]+)"
matches = re.findall(pattern, errors_text)

price_map = {}
for name, val in matches:
    val = val.replace(',', '.').rstrip('.')
    float_val = float(val)
    int_price = int(float_val) + 650
    price_map[name] = int_price

print("Parsed prices:", price_map)

list_of_directory_ship = ["_DH", "_Lua", "_Mono", "_NF"]

for dir_name in list_of_directory_ship:
    directory = os.path.join(
        BASE_DIR, "Resources", "Prototypes", dir_name, "Shipyard"
    )

    if not os.path.isdir(directory):
        print(f"[SKIP] No directory: {directory}")
        continue

    for root, _, files in os.walk(directory):
        for file in files:
            if not file.endswith(('.yml', '.yaml')):
                continue

            file_path = os.path.join(root, file)

            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()

            updated = False

            for name, price in price_map.items():
                pattern_price = rf"(id:\s*{name}[\s\S]*?price:\s*)\d+"

                if re.search(pattern_price, content):
                    content = re.sub(
                        pattern_price,
                        rf"\g<1>{price}",
                        content,
                        count=1
                    )
                    updated = True

            if updated:
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.write(content)
                print(f"[UPDATED] {file_path}")
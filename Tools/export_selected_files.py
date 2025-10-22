import os
import shutil


def main() -> None:
    base_dir = os.path.abspath(os.getcwd())
    desktop = os.path.join(os.path.expanduser('~'), 'Desktop')
    dest_root = os.path.join(desktop, 'sf14-export')

    file_paths = [
        r"Resources/Locale/ru-RU/_NF/guidebook/guides.ftl",
        r"Resources/Locale/ru-RU/guidebook/guides.ftl",
        r"Resources/Locale/ru-RU/ss14-ru/fishing.ftl",
        r"Resources/Locale/ru-RU/ss14-ru/prototypes/_nf/entities/objects/consumable/food/meat.ftl",
        r"Resources/Locale/ru-RU/ss14-ru/prototypes/_nf/entities/objects/consumable/food/sushi.ftl",
        r"Resources/Locale/ru-RU/ss14-ru/prototypes/actions/fishing.ftl",
        r"Resources/Locale/ru-RU/ss14-ru/prototypes/entities/objects/consumable/food/baked/bread.ftl",
        r"Resources/Locale/ru-RU/ss14-ru/prototypes/entities/objects/consumable/food/baked/misc.ftl",
        r"Resources/Locale/ru-RU/ss14-ru/prototypes/entities/objects/consumable/food/burger.ftl",
        r"Resources/Locale/ru-RU/ss14-ru/prototypes/entities/objects/consumable/food/meat.ftl",
        r"Resources/Locale/ru-RU/ss14-ru/prototypes/entities/objects/consumable/food/taco.ftl",
        r"Resources/Locale/ru-RU/ss14-ru/prototypes/entities/objects/specific/fishing/fish.ftl",
        r"Resources/Locale/ru-RU/ss14-ru/prototypes/entities/objects/specific/fishing/fishing_spot.ftl",
        r"Resources/Locale/ru-RU/ss14-ru/prototypes/entities/objects/tools/fishing_rods.ftl",
        r"Resources/Prototypes/_Goobstation/Entities/Objects/Specific/Fishing/fish.yml",
        r"Resources/Prototypes/_Goobstation/Entities/Objects/Specific/Fishing/loot_tables.yml",
        r"Resources/Prototypes/_NF/Recipes/Cooking/meal_recipes.yml",
        r"Resources/Prototypes/Entities/Objects/Consumable/Food/meat.yml",
        r"Resources/Prototypes/Recipes/Cooking/meal_recipes.yml",
    ]

    os.makedirs(dest_root, exist_ok=True)

    missing: list[str] = []
    copied: list[str] = []

    for rel_path in file_paths:
        src_path = os.path.join(base_dir, rel_path)
        if not os.path.exists(src_path):
            missing.append(rel_path)
            continue

        dest_path = os.path.join(dest_root, rel_path)
        os.makedirs(os.path.dirname(dest_path), exist_ok=True)
        shutil.copy2(src_path, dest_path)
        copied.append(rel_path)

    print(f"Copied to: {dest_root}")
    if copied:
        print("Copied files:")
        for p in copied:
            print(f"  {p}")
    if missing:
        print("Missing files:")
        for p in missing:
            print(f"  {p}")


if __name__ == "__main__":
    main()



lathe-menu-title = Меню станка
lathe-menu-queue = Очередь
lathe-menu-server-list = Список серверов
lathe-menu-sync = Синхр.
lathe-menu-search-designs = Поиск проектов
lathe-menu-category-all = Всё
lathe-menu-search-filter = Фильтр:
lathe-menu-amount = Кол-во:
lathe-menu-loop = Зациклить
lathe-menu-skip = Пропуск при нехватке
lathe-menu-recipe-count = { $count ->
    [1] {$count} рецепт
    [few] {$count} рецепта
    *[other] {$count} рецептов
}
lathe-menu-reagent-slot-examine = Сбоку имеется отверстие для мензурки.
lathe-reagent-dispense-no-container = Жидкость выливается из {THE($name)} на пол!
lathe-menu-result-reagent-display = {$reagent} ({$amount}ед.)
lathe-menu-material-display = {$material} ({$amount})
lathe-menu-tooltip-display = {$amount} {$material}
lathe-menu-description-display = [italic]{$description}[/italic]
lathe-menu-material-amount = { $amount ->
    [1] {NATURALFIXED($amount, 2)} {$unit}
    *[other] {NATURALFIXED($amount, 2)} {MAKEPLURAL($unit)}
}
lathe-menu-material-amount-missing = { $amount ->
    [1] {NATURALFIXED($amount, 2)} {$unit} {$material} ([color=red]не хватает {NATURALFIXED($missingAmount, 2)} {$unit}[/color])
    *[other] {NATURALFIXED($amount, 2)} {MAKEPLURAL($unit)} {$material} ([color=red]не хватает {NATURALFIXED($missingAmount, 2)} {MAKEPLURAL($unit)}[/color])
}
lathe-menu-entity-amount-missing = {$amount} {$material} ([color=red]не хватает {$missingAmount}[/color])
lathe-menu-reagent-amount-missing = {$amount}ед. {$material} ([color=red]не хватает {$missingAmount}ед.[/color])
lathe-menu-no-materials-message = Материалы не загружены.
lathe-menu-silo-linked-message = Сило связано
lathe-menu-fabricating-message = Производится...
lathe-menu-materials-title = Материалы
lathe-menu-queue-title = Очередь производства

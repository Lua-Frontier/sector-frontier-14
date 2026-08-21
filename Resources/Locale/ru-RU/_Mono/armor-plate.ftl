armor-plate-break = Ваша {$plateName} раскололась!
armor-plate-examine-with-plate = Установлена [color=yellow]{$plateName}[/color]. Прочность: [color={$durabilityColor}]{$percent}%[/color]
armor-plate-examine-with-plate-simple = Установлена [color=yellow]{$plateName}[/color].
armor-plate-examine-no-plate = Бронепластина не установлена.
armor-plate-examine-no-storage = Нет отсека для бронепластин.

armor-plate-examinable-verb-text = Параметры пластины
armor-plate-examinable-verb-message = Осмотреть характеристики защиты и прочности.

armor-plate-attributes-examine = Эта бронепластина:
armor-plate-initial-durability = Рассчитана на [color=yellow]{ $durability }[/color] стандартных единиц урона.

armor-plate-item-durability = Прочность: [color={$durabilityColor}]{$percent}%[/color]

armor-plate-gait-speed = скорость
armor-plate-gait-walk = скорость ходьбы
armor-plate-gait-sprint = скорость бега

armor-plate-speed-display =
    { $deltasign ->
        [-1] Увеличивает {$gait} на [color=yellow]{$speedPercent}%[/color].
         [0] Не влияет на вашу скорость.
         [1] Уменьшает {$gait} на [color=yellow]{$speedPercent}%[/color].
        *[other] Некорректное значение скорости!
    }

armor-plate-ratios-display =
    { $deltasign ->
        [-1] [color=cyan]Поглощает[/color] [color=yellow]{$ratioPercent}%[/color] урона типа [color=yellow]{$dmgType}[/color] и получает [color=yellow]x{$multiplier}[/color] урона прочности.
         [0] Не реагирует на {$dmgType}
         [1] [color=fuchsia]Усиливает[/color] [color=yellow]{$dmgType}[/color] на [color=yellow]{$ratioPercent}%[/color] и получает добавленный урон как [color=yellow]x{$multiplier}[/color] урона прочности.
        *[other] Некорректное значение поглощения {$dmgType}!
    }
armor-plate-stamina-value = Наносит [color=yellow]{$multiplier}%[/color] поглощённого урона как урон выносливости.

starmap-computer-title = Консоль звездной карты
starmap-details-display-label = Общая информация
starmap-star-details-current-star = Текущая звезда:
starmap-star-details-spin-range = Дальность спина:
starmap-crystal-integrity = Целостность кристалла:
starmap-star-details-display-label = Детали звезды
starmap-star-details-name = Имя:
starmap-star-details-coordinates = Координаты:
starmap-star-details-button-warp = БСС
starmap-star-details-position = X: { $x }, Y: { $y }
starmap-center-station = Центральная станция
starmap-ui-title = Карта звёзд
starmap-refresh = Обновить
starmap-warp-status = Статус БСС
starmap-warp-ready = Готово
starmap-warp-seconds = { $seconds }с
starmap-warp-cooldown = Межзвёздный двигатель перезаряжается: { $seconds }с
starmap-warp-cooldown-time = Межзвёздный двигатель перезаряжается: { $minutes }м { $seconds }с
starmap-already-here = Вы уже в этом секторе
starmap-no-hyperlane = Нельзя прыгнуть: нет гиперлинии к цели
starmap-no-warpdrive = Нет межзвёздного двигателя или он обесточен
ship-ftl-tag-base = [БАЗА]
ship-ftl-tag-star = [ЗВЕЗДА]
ship-ftl-tag-planet = [ПЛАНЕТА]
ship-ftl-tag-asteroid = [АСТЕРОИД]
ship-ftl-tag-ruin = [РУИНЫ]
ship-ftl-tag-warp = [БСС]
ship-ftl-tag-oor = Вне диапазона
popup-drive-charging = Двигатель теперь заряжается
popup-drive-not-charging = Двигатель больше не заряжается
drive-examined-multiple-drives = Обнаружено несколько двигателей на этой сетке. Поддерживается только один двигатель на сетку.
drive-examined-ready = Двигатель готов к прыжку.
drive-examined = Двигатель { $charging ->
    [true] заряжается
    *[false] не заряжается
} ({ $charge }% завершено). { $destination ->
    [true] Назначение установлено.
    *[false] Назначение не установлено.
}

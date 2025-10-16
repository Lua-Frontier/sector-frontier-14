# Starmap Console
starmap-computer-title = Консоль звездной карты

# Starmap Details Display
starmap-details-display-label = Общая информация
starmap-star-details-current-star = Текущая звезда:
starmap-star-details-spin-range = Дальность спина:
starmap-crystal-integrity = Целостность кристалла:

# Star Details Display
starmap-star-details-display-label = Детали звезды
starmap-star-details-name = Имя:
starmap-star-details-coordinates = Координаты:
starmap-star-details-button-warp = Варп

# Star Details Position
starmap-star-details-position = X: { $x }, Y: { $y }

# Center Station
starmap-center-station = Центральная станция

# Ship FTL Tags
ship-ftl-tag-base = [БАЗА]
ship-ftl-tag-star = [ЗВЕЗДА]
ship-ftl-tag-planet = [ПЛАНЕТА]
ship-ftl-tag-asteroid = [АСТЕРОИД]
ship-ftl-tag-ruin = [РУИНЫ]
ship-ftl-tag-warp = [ВАРП]
ship-ftl-tag-oor = Вне диапазона

# Drive Messages
popup-drive-charging = Двигатель теперь заряжается
popup-drive-not-charging = Двигатель больше не заряжается

# Drive Examination
drive-examined-multiple-drives = Обнаружено несколько двигателей на этой сетке. Поддерживается только один двигатель на сетку.
drive-examined-ready = Двигатель готов к варпу.
drive-examined = Двигатель { $charging -> 
    [true] заряжается
    *[false] не заряжается
} ({ $charge }% завершено). { $destination -> 
    [true] Назначение установлено.
    *[false] Назначение не установлено.
}

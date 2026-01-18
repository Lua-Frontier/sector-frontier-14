## General stuff

ui-options-title = Игровые настройки
ui-options-tab-accessibility = Доступность
ui-options-tab-graphics = Графика
ui-options-tab-controls = Управление
ui-options-tab-audio = Аудио
ui-options-tab-network = Сеть
ui-options-tab-misc = Основные
ui-options-apply = Сохранить и применить
ui-options-reset-all = Сброс изменений
ui-options-default = Сброс к настройкам по умолчанию
ui-options-value-percent = { TOSTRING($value, "P0") }

# Misc/General menu

ui-options-discordrich = Включить Discord Rich Presence
ui-options-general-ui-style = Стиль UI
ui-options-general-discord = Discord
ui-options-general-cursor = Курсор
ui-options-general-speech = Речь
ui-options-general-storage = Инвентарь
ui-options-general-accessibility = Доступность

## Audio menu

ui-options-master-volume = Основная громкость:
ui-options-midi-volume = Громкость MIDI (Муз. инструменты):
ui-options-ambient-music-volume = Громкость музыки окружения:
ui-options-ambience-volume = Громкость окружения:
ui-options-lobby-volume = Громкость лобби и окончания раунда:
ui-options-interface-volume = Громкость интерфейса:
ui-options-ambience-max-sounds = Кол-во одновременных звуков окружения:
ui-options-lobby-music = Музыка в лобби
ui-options-restart-sounds = Звуки перезапуска раунда
ui-options-event-music = Музыка событий
ui-options-volume-percent = { TOSTRING($volume, "P0") }
ui-options-admin-sounds = Музыка админов
ui-options-volume-label = Громкость
ui-options-display-label = Дисплей
ui-options-quality-label = Качество
ui-options-misc-label = Разное
ui-options-interface-label = Интерфейс

## Graphics menu

ui-options-show-held-item = Показать удерживаемый элемент рядом с курсором
ui-options-show-combat-mode-indicators = Показать индикатор боевого режима рядом с курсором
ui-options-opaque-storage-window = Непрозрачность окна хранилища
ui-options-show-ooc-patron-color = Цветной ник в OOC для патронов с Patreon
ui-options-show-looc-on-head = Показывать LOOC-чат над головами персонажей
ui-options-chat-window-opacity-percent = { TOSTRING($opacity, "P0") }
ui-options-fancy-speech = Показывать имена в облачках с текстом
ui-options-screen-shake-percent = { TOSTRING($intensity, "P0") }
ui-options-fancy-name-background = Добавить фон облачкам с текстом
ui-options-vsync = Вертикальная синхронизация
ui-options-fullscreen = Полный экран
ui-options-lighting-label = Качество освещения:
ui-options-lighting-very-low = Очень низкое
ui-options-lighting-low = Низкое
ui-options-lighting-medium = Среднее
ui-options-lighting-high = Высокое
ui-options-scale-label = Масштаб UI:
ui-options-scale-auto = Автоматическое ({ TOSTRING($scale, "P0") })
ui-options-scale-75 = 75%
ui-options-scale-100 = 100%
ui-options-scale-125 = 125%
ui-options-scale-150 = 150%
ui-options-scale-175 = 175%
ui-options-scale-200 = 200%
ui-options-hud-theme = Тема HUD:
ui-options-hud-theme-default = По умолчанию
ui-options-hud-theme-plasmafire = Плазма
ui-options-hud-theme-slimecore = Слаймкор
ui-options-hud-theme-clockwork = Механизм
ui-options-hud-theme-retro = Ретро
ui-options-hud-theme-minimalist = Минимализм
ui-options-hud-theme-ashen = Пепел
ui-options-vp-stretch = Растянуть изображение для соответствия окну игры
ui-options-vp-scale = Фиксированный масштаб окна игры:
ui-options-vp-scale-value = x{ $scale }
ui-options-vp-integer-scaling = Использовать целочисленное масштабирование (может вызывать появление чёрных полос/обрезания)
ui-options-vp-integer-scaling-tooltip =
    Если эта опция включена, область просмотра будет масштабироваться,
    используя целочисленное значение при определённых разрешениях. Хотя это и
    приводит к чётким текстурам, это часто означает, что сверху/снизу экрана будут
    чёрные полосы или что часть окна не будет видна.
ui-options-vp-vertical-fit = Подгон окна просмотра по вертикали
ui-options-vp-vertical-fit-tooltip =
    Когда функция включена, основное окно просмотра не будет учитывать горизонтальную ось
    при подгонке под ваш экран. Если ваш экран меньше, чем окно просмотра,
    то это приведёт к его обрезанию по горизонтальной оси.
ui-options-vp-low-res = Изображение низкого разрешения
ui-options-parallax-low-quality = Низкокачественный параллакс (фон)
ui-options-fps-counter = Показать счётчик FPS
ui-options-vp-width = Ширина окна игры:
ui-options-hud-layout = Тип HUD:

## Controls menu

ui-options-binds-reset-all = Сбросить ВСЕ привязки
ui-options-binds-explanation = ЛКМ — изменить кнопку, ПКМ — убрать кнопку
ui-options-unbound = Пусто
ui-options-bind-reset = Сбросить
ui-options-key-prompt = Нажмите кнопку...
ui-options-header-movement = Перемещение
ui-options-header-camera = Камера
ui-options-header-interaction-basic = Базовые взаимодействия
ui-options-header-interaction-adv = Продвинутые взаимодействия
ui-options-header-ui = Интерфейс
ui-options-header-misc = Разное
ui-options-header-hotbar = Хотбар
ui-options-header-shuttle = Шаттл
ui-options-header-map-editor = Редактор карт
ui-options-header-dev = Разработка
ui-options-header-general = Основное
ui-options-hotkey-keymap = Использовать клавиши QWERTY (США)
ui-options-hotkey-toggle-walk = Переключать шаг\бег
ui-options-function-move-up = Двигаться вверх
ui-options-function-move-left = Двигаться налево
ui-options-function-move-down = Двигаться вниз
ui-options-function-move-right = Двигаться направо
ui-options-function-walk = Идти
ui-options-function-camera-rotate-left = Повернуть налево
ui-options-function-camera-rotate-right = Повернуть направо
ui-options-function-camera-reset = Сбросить камеру
ui-options-function-zoom-in = Приблизить
ui-options-function-zoom-out = Отдалить
ui-options-function-reset-zoom = Сбросить
ui-options-function-use = Использовать
ui-options-function-use-secondary = Использовать вторично
ui-options-function-alt-use = Альтернативное использование
ui-options-function-wide-attack = Размашистая атака
ui-options-function-activate-item-in-hand = Использовать предмет в руке
ui-options-function-alt-activate-item-in-hand = Альтернативно использовать предмет в руке
ui-options-function-activate-item-in-world = Использовать предмет в мире
ui-options-function-alt-activate-item-in-world = Альтернативно использовать предмет в мире
ui-options-function-drop = Положить предмет
ui-options-function-examine-entity = Осмотреть
ui-options-function-swap-hands = Поменять руки
ui-options-function-move-stored-item = Переместить хранящийся объект
ui-options-function-rotate-stored-item = Повернуть хранящийся объект
ui-options-function-save-item-location = Сохранить расположение объекта
ui-options-static-storage-ui = Закрепить интерфейс хранилища на хотбаре
ui-options-function-smart-equip-backpack = Умная экипировка в рюкзак
ui-options-function-smart-equip-belt = Умная экипировка на пояс
ui-options-function-open-backpack = Открыть рюкзак
ui-options-function-open-belt = Открыть пояс
ui-options-function-throw-item-in-hand = Бросить предмет
ui-options-function-try-pull-object = Тянуть объект
ui-options-function-move-pulled-object = Тянуть объект в сторону
ui-options-function-release-pulled-object = Перестать тянуть объект
ui-options-function-point = Указать на что-либо
ui-options-function-focus-chat-input-window = Писать в чат
ui-options-function-focus-local-chat-window = Писать в чат (IC)
ui-options-function-focus-emote = Писать в чат (Emote)
ui-options-function-focus-whisper-chat-window = Писать в чат (Шёпот)
ui-options-function-focus-radio-window = Писать в чат (Радио)
ui-options-function-focus-looc-window = Писать в чат (LOOC)
ui-options-function-focus-ooc-window = Писать в чат (OOC)
ui-options-function-focus-admin-chat-window = Писать в чат (Админ)
ui-options-function-focus-dead-chat-window = Писать в чат (Мёртвые)
ui-options-function-focus-console-chat-window = Писать в чат (Консоль)
ui-options-function-cycle-chat-channel-forward = Переключение каналов чата (Вперёд)
ui-options-function-cycle-chat-channel-backward = Переключение каналов чата (Назад)
ui-options-function-open-character-menu = Открыть меню персонажа
ui-options-function-open-context-menu = Открыть контекстное меню
ui-options-function-open-crafting-menu = Открыть меню строительства
ui-options-function-open-inventory-menu = Открыть снаряжение
ui-options-function-open-a-help = Открыть админ помощь
ui-options-function-open-abilities-menu = Открыть меню действий
ui-options-function-open-emotes-menu = Открыть меню эмоций
ui-options-function-toggle-round-end-summary-window = Переключить окно итогов раунда
ui-options-function-open-entity-spawn-window = Открыть меню спавна сущностей
ui-options-function-open-sandbox-window = Открыть меню песочницы
ui-options-function-open-tile-spawn-window = Открыть меню спавна тайлов
ui-options-function-open-decal-spawn-window = Открыть меню спавна декалей
ui-options-function-open-admin-menu = Открыть админ меню
ui-options-function-open-guidebook = Открыть руководство
ui-options-function-window-close-all = Закрыть все окна
ui-options-function-window-close-recent = Закрыть текущее окно
ui-options-function-show-escape-menu = Переключить игровое меню
ui-options-function-escape-context = Закрыть текущее окно или переключить игровое меню
ui-options-function-take-screenshot = Сделать скриншот
ui-options-function-take-screenshot-no-ui = Сделать скриншот (без интерфейса)
ui-options-function-toggle-fullscreen = Переключить полноэкранный режим
ui-options-function-editor-place-object = Разместить объект
ui-options-function-editor-cancel-place = Отменить размещение
ui-options-function-editor-grid-place = Размещать в сетке
ui-options-function-editor-line-place = Размещать в линию
ui-options-function-editor-rotate-object = Повернуть
ui-options-function-editor-flip-object = Перевернуть
ui-options-function-editor-copy-object = Копировать
ui-options-function-show-debug-console = Открыть консоль
ui-options-function-show-debug-monitors = Показать дебаг информацию
ui-options-function-inspect-entity = Изучить сущность
ui-options-function-hide-ui = Спрятать интерфейс
ui-options-function-hotbar1 = 1 слот хотбара
ui-options-function-hotbar2 = 2 слот хотбара
ui-options-function-hotbar3 = 3 слот хотбара
ui-options-function-hotbar4 = 4 слот хотбара
ui-options-function-hotbar5 = 5 слот хотбара
ui-options-function-hotbar6 = 6 слот хотбара
ui-options-function-hotbar7 = 7 слот хотбара
ui-options-function-hotbar8 = 8 слот хотбара
ui-options-function-hotbar9 = 9 слот хотбара
ui-options-function-hotbar0 = 0 слот хотбара
ui-options-function-loadout1 = 1 страница хотбара
ui-options-function-loadout2 = 2 страница хотбара
ui-options-function-loadout3 = 3 страница хотбара
ui-options-function-loadout4 = 4 страница хотбара
ui-options-function-loadout5 = 5 страница хотбара
ui-options-function-loadout6 = 6 страница хотбара
ui-options-function-loadout7 = 7 страница хотбара
ui-options-function-loadout8 = 8 страница хотбара
ui-options-function-loadout9 = 9 страница хотбара
ui-options-function-loadout0 = 0 страница хотбара
ui-options-function-shuttle-strafe-up = Стрейф вверх
ui-options-function-shuttle-strafe-right = Стрейф вправо
ui-options-function-shuttle-strafe-left = Стрейф влево
ui-options-function-shuttle-strafe-down = Стрейф вниз
ui-options-function-shuttle-rotate-left = Поворот налево
ui-options-function-shuttle-rotate-right = Поворот направо
ui-options-function-shuttle-brake = Торможение

ui-options-net-predict = Клиентское предсказание
ui-options-net-interp-ratio = Размер буфера состояний
ui-options-net-predict-tick-bias = Погрешность тиков предсказания
ui-options-net-pvs-spawn = Бюджет спавна PVS объектов
ui-options-net-pvs-entry = Бюджет входа PVS объектов
ui-options-net-pvs-leave = Скорость отключения PVS

ui-options-net-interp-ratio-tooltip =
    Размер буфера состояний - количество снимков игры для сглаживания.

    Больше значение = устойчивее к потере пакетов, но больше задержка.
    Каждая единица добавляет ~33ms задержки.

    Рекомендуемые значения:
    • 2 - отличное соединение (пинг <30ms)
    • 3-4 - хорошее соединение (пинг 30-80ms)
    • 4-5 - среднее соединение (пинг 80-150ms)
    • 6-8 - плохое соединение (пинг >150ms, packet loss >3%)

    Увеличивайте при "дёрганиях" других игроков или потере пакетов.
    Уменьшайте для минимальной задержки в PvP.

    Значение по умолчанию: 2

ui-options-net-predict-tick-bias-tooltip =
    Погрешность тиков предсказания - дополнительные тики симуляции вперёд.

    Компенсирует потерю пакетов от клиента к серверу.
    Больше значение = устойчивее к upload packet loss.

    Рекомендуемые значения:
    • 0-1 - идеальное соединение
    • 1-2 - обычное соединение (по умолчанию: 1)
    • 2-3 - проблемное соединение (VPN, нестабильный upload)
    • 4+ - очень плохое соединение (не рекомендуется)

    Увеличивайте при "откатах" ваших действий.
    Windows системы имеют дополнительно +16ms автоматически.

ui-options-net-pvs-spawn-tooltip =
    Бюджет спавна PVS - лимит НОВЫХ объектов за тик от сервера.
    PVS = система видимости объектов.

    Меньше значение = плавнее работа, но эффект "pop-in".
    Больше значение = объекты появляются быстрее, но возможны фризы.

    Рекомендуемые значения:
    • 20-30 - слабый ПК
    • 40-60 - средний ПК (по умолчанию: 50)
    • 70-100 - мощный ПК
    • 100-150 - топовый ПК с отличным интернетом

    Уменьшайте при фризах во время взрывов или массового спавна.
    Увеличивайте если объекты медленно появляются.

ui-options-net-pvs-entry-tooltip =
    Бюджет входа PVS - лимит СУЩЕСТВУЮЩИХ объектов, входящих в видимость за тик.

    Контролирует объекты, которые уже были на карте, но стали видимы
    (вы подошли ближе, открыли дверь, летите на шаттле).

    Рекомендуемые значения:
    • 50-100 - слабый ПК
    • 150-250 - средний ПК (по умолчанию: 200)
    • 250-350 - мощный ПК
    • 350-500 - топовый ПК

    Уменьшайте при фризах во время движения по станции.
    Увеличивайте при сильном эффекте "pop-in" или если врезаетесь в "невидимые" объекты.

    Обычно можно ставить выше, чем spawn budget.

ui-options-net-pvs-leave-tooltip =
    Скорость отключения PVS - как быстро удаляются невидимые объекты из памяти.

    Меньше значение = плавнее движение, но больше потребление RAM.
    Больше значение = быстрее освобождение памяти, но возможны микрофризы.

    Рекомендуемые значения:
    • 30-50 - для плавности (езда на шаттле)
    • 60-90 - баланс (по умолчанию: 75)
    • 100-150 - экономия памяти (статичная игра)
    • 120-200 - при нехватке RAM

    Уменьшайте при микрофризах во время ходьбы.
    Увеличивайте при высоком потреблении памяти.
    Не рекомендуется >200 без необходимости.

ui-options-gc-enabled = Автоочистка памяти
ui-options-gc-enabled-tooltip =
    Включает периодическую очистку памяти (GC) для уменьшения использования RAM.
    Может вызывать кратковременные микрофризы в момент очистки.

ui-options-gc-interval = Интервал автоочистки (мин)
ui-options-gc-interval-tooltip =
    Как часто выполнять очистку памяти. Минимум: 5 минут. Максимум: 60 минут.
    По умолчанию: 30 минут.

cmd-options-desc = Открывает меню опций, опционально с конкретно выбранной вкладкой.
cmd-options-help = Использование: options [tab]
ui-options-enable-color-name = Цветные имена персонажей
ui-options-colorblind-friendly = Режим для дальтоников
ui-options-reduced-motion = Снижение интенсивности визуальных эффектов
ui-options-chat-window-opacity = Прозрачность окна чата
ui-options-screen-shake-intensity = Интенсивность дрожания экрана
ui-options-alerts-icon-scale = Размер иконок алертов
ui-options-alerts-position = Позиция алертов:
ui-options-alerts-position-right = Справа
ui-options-alerts-position-bottom = Снизу
ui-options-show-offer-mode-indicators = Показывать индикатор передачи предмета
ui-options-function-offer-item = Передать что-либо

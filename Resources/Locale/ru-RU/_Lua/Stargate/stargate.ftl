stargate-console-title = Наборное устройство
stargate-console-dial = Набрать
stargate-console-clear = Очистить
stargate-console-close-portal = Закрыть портал
stargate-console-copy = Копировать
stargate-console-gate-address-label = Адрес текущих врат
stargate-console-status-idle = Готов
stargate-console-status-input = Ввод адреса...
stargate-console-status-dialing = Установка соединения...
stargate-console-status-active = Червоточина активна
stargate-console-no-gate = Не найдены связанные Звёздные врата.
stargate-console-invalid-address = Неверный адрес.
stargate-console-dial-failed = Не удалось установить соединение.
stargate-console-overloaded = Система врат перегружена
stargate-console-gate-busy = Звёздные врата заняты - активна входящая червоточина.
stargate-controllable-activate = Активировать Звёздные врата
stargate-controllable-deactivate = Деактивировать Звёздные врата
stargate-controllable-activated = Звёздные врата активированы.
stargate-controllable-deactivated = Звёздные врата деактивированы.
stargate-console-iris-lock = Закрыть диафрагму
stargate-console-iris-unlock = Открыть диафрагму
stargate-console-iris-closed = Диафрагма врат закрыта.
stargate-console-disk-title = Адресный диск
stargate-console-disk-save = Сохранить адрес
stargate-console-disk-dial = Набрать
stargate-console-status-auto-dialing = Автонабор...

stargate-editor-title = Консоль данных звёздных врат
stargate-editor-disk-left = Левый диск
stargate-editor-disk-right = Правый диск
stargate-editor-save-left = Сохранить
stargate-editor-save-right = Сохранить
stargate-editor-clone-to-right = Клонировать
stargate-editor-clone-to-left = Клонировать

stargate-minimap-title = Картограф
stargate-minimap-disk-active = Диск: Активен
stargate-minimap-disk-empty = Диск: Пусто
stargate-minimap-disk-ready = Диск: Готов
stargate-minimap-merge-1-to-2 = 1 → 2
stargate-minimap-merge-2-to-1 = 2 → 1
stargate-minimap-status = Картограф Lua Technologies
stargate-minimap-not-planet = Не планета
stargate-minimap-insert-disk = Вставьте картографический диск
stargate-minimap-gate = Врата

ent-StargateConsole = наборное устройство
    .desc = Панель для набора адресов Звёздных врат и установки червоточины.

ent-StargateAddressEditorConsole = консоль данных звёздных врат
    .desc = Рабочая станция для работы с дисками координат. Копирование и редактирование между двумя дисками. Не набирает врата.

ent-StargateControllableLuaTech = звёздные врата
    .desc = Древнее кольцевое устройство для создания стабильных червоточин в другие миры.
    .suffix = LuaTech
ent-StargateControllableSyndicate = { ent-Stargate }
    .desc = { ent-Stargate }
    .suffix = Syndicate
ent-Stargate = звёздные врата
    .desc = Древнее кольцевое устройство для создания стабильных червоточин в другие миры.

ent-StargateAddressDisk = диск координат звёздных врат
    .desc = Шифрованный диск с координатами Звёздных врат. Вставляется в наборное устройство.

ent-StargateDebugPaper = случайные адреса звёздных врат
    .desc = Документ со случайными адресами.
    .suffix = Debug, DO NOT MAP.
ent-StargateAddressPaper = потёртая бумага с символами
    .desc = Обрывок бумаги с начертанным адресом.

ent-SpawnStargateAddressPaper = спавнер бумаги с адресом врат
    .desc = Спавнит бумагу со случайным адресом Звёздных врат. Для редактора карт: розовый маркер + спрайт бумаги для удобного позиционирования.

ent-StargateMinimapTablet = планшет картограф
    .desc = Ручной планшет для картографирования планеты, есть гравировка Lua Technologies на обратной стороне. Есть теория что Lua Technologies собирает информацию о мирах через них.

ent-StargateMinimapDisk = диск картографии
    .desc = Диск данных для хранения картографических данных мира. Вставьте в планшет картограф.

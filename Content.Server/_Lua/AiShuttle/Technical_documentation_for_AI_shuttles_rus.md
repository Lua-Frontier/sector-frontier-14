### Система ИИ шаттлов и огневого контроля: архитектура, поведение и расширение

Версия документа: 1.0
Область: серверная логика ИИ шаттлов (Lua-папки), пресеты, карты и огневой контроль (FireControl)

---

## Обзор

Данный документ описывает устройство и работу серверной системы ИИ шаттлов, взаимодействие с подсистемой огневого контроля, а также способы конфигурации через компоненты, прототипы и карты. Охватываются следующие файлы:

- `Content.Server/_Lua/AiShuttle/AiShuttleBrainSystem.cs`
- `Content.Server/_Mono/FireControl/FireControllableComponent.cs`
- `Content.Server/_Mono/FireControl/FireControlServerComponent.cs`
- `Content.Server/_Mono/FireControl/FireControlSystem.cs`
- `Content.Shared/_Lua/AiShuttle/AiShuttleBrainComponent.cs`
- `Content.Shared/_Lua/AiShuttle/AiShuttlePresetComponent.cs`
- `Content.Shared/_Lua/AiShuttle/AiShuttlePresetPrototype.cs`
- `Resources/Maps/_Lua/ShuttleEvent/AI_shuttle/PerunAI.yml`
- `Resources/Maps/_Lua/ShuttleEvent/AI_shuttle/PozvizdAI.yml`
- `Resources/Maps/_Lua/ShuttleEvent/AI_shuttle/StribogAI.yml`
- `Resources/Prototypes/_Lua/AiShuttlePresets.yml`
- `Resources/Prototypes/_Lua/NPCs/ai_pilot.yml`
- `Resources/Prototypes/_Lua/NPCs/mob.yml`

Ключевые подсистемы:
- ИИ пилотирования и боя шаттла: `AiShuttleBrainSystem` + `AiShuttleBrainComponent`
- Пресеты настройки ИИ: `AiShuttlePresetComponent`, `AiShuttlePresetPrototype`, а также yml файл конфигурации
- Огневой контроль (Gunnery Control System, GCS): `FireControlSystem` + компоненты `FireControllableComponent`, `FireControlServerComponent`
- Карты с размещением ИИ-шаттлов: `PerunAI.yml`, `PozvizdAI.yml`, `StribogAI.yml` в папке Resources\Maps\_Lua\ShuttleEvent\AI_shuttle\
- Пилот-агент для ввода в консоль шаттла: `MobShuttleAIPilotNF`/`MobShuttleAIPilot`

---

## Архитектура ИИ шаттла

### Компоненты конфигурации

- `AiShuttleBrainComponent`- хранит параметры поведения:
  - Патруль: `AnchorEntityName`, `FallbackAnchor`, `MinPatrolRadius`, `MaxPatrolRadius`
  - Дистанции боя и FTL: `FightRangeMin/Max`, `FtlMinDistance`, `FtlExitOffset`, `PostFtlStabilizeSeconds`, `FtlCooldownSeconds`
  - Безопасность/цели: `RetreatHealthFraction`, `AvoidStationFire`, `ForbidRamming`, `TargetShuttlesOnly`, `TargetShuttles`, `TargetDebris`
  - Огневые параметры: `MaxWeaponRange`, `ForwardAngleOffset`
  - Прочее: `CombatTriggerRadius`, `MinSeparation`, параметры патрульных FTL прыжков (`PatrolFtlMin/MaxDistance`, `PatrolWaypointTolerance`)
  - Рантайм состояние (serverOnly): `CurrentTarget`, `LastKnownTargetPos`, `PilotEntity`, `PatrolWaypoint`, `WingLeader`, `WingSlot`, `FacingMarker`, `StabilizeTimer`, `FtlCooldownTimer`, `InCombat`

- `AiShuttlePresetComponent` (сетевой)- настраивает те же поля на сущности грида, применяются на сервере при запуске мозга.

- `AiShuttlePresetPrototype`- прототип пресета, поддерживает `NameMatch` для точного совпадения по `MetaData.EntityName` грида.

### Серверная система мозга

- `AiShuttleBrainSystem`
  - Частоты:
    - Сенсоры: 5 Гц- выбор цели, обновление ситуационной осведомлённости
    - Контроль: 9 Гц- пилотирование, удержание формации, стрельба
  - Пресеты: одноразовое применение либо из `AiShuttlePresetComponent` на гриде, либо по `AiShuttlePresetPrototype` с `NameMatch`.
  - Назначение крыльев (формация) для кораблей с именем "Perun": группами по три (лидер + 2 ведомых), поля `WingLeader`/`WingSlot`.
  - Пилот: гарантирует наличие пилот-агента (`MobShuttleAIPilotNF` предпочтительно; иначе `MobShuttleAIPilot`) и его привязку к ближайшей `ShuttleConsole` на гриде.
  - Логика выбора цели:
    - Ищет чужие гриды на той же карте, с компонентом `ShuttleDeedComponent` и доступной пилотируемой консолью
    - Игнорирует дружественные ИИ-шаттлы (наличие `AiShuttleBrainComponent` на цели)
    - Внутри радиуса `CombatTriggerRadius`, с учётом кольца патруля (`MinPatrolRadius..MaxPatrolRadius`)
    - Предпочтение ближних целей (простой скоринг)
  - Пилотирование/манёвр:
    - В бою: поддержание дистанции в окне `FightRangeMin/Max`, орбитальный дрейф (тангенциальная составляющая), радиальная коррекция, избежание препятствий (raycast), соблюдение `MinSeparation` (анти-таран)
    - FTL микропрыжки: при большой дальности и охлаждении `FtlCooldownTimer` → прыжок "за цель" с оффсетом `FtlExitOffset`, пост-стабилизация на `PostFtlStabilizeSeconds`
    - Патруль: генерация случайной точки-веяпойнта и/или прямолинейные FTL-прыжки в диапазоне `PatrolFtlMin/MaxDistance`
  - Ведение огня: вызывает `FireControlSystem.TryAimAndFireGrid`, при этом мозг целится в позицию консоли противника, если найдена
  - Формирование входов: `DriveViaConsole` вычисляет удерживаемые кнопки пилотажа исходя из направления цели и маркера наведения, с учётом демпфирования и торможения по ситуации

---

## Подсистема огневого контроля (GCS)

### Компоненты

- `FireControlServerComponent`- сервер GCS на гриде:
  - Связь с гридом: `ConnectedGrid`
  - Учёт вооружения: `Controlled`, `UsedProcessingPower`, лимит `ProcessingPower`
  - Консоли GCS: `Consoles`
  - Салвы: `UseSalvos`, `SalvoPeriodSeconds`, `SalvoWindowSeconds`, `SalvoJitterSeconds`

- `FireControllableComponent`- на оружии/турелях:
  - Привязка к серверу: `ControllingServer`
  - КД стрельбы: `NextFire`, `FireCooldown`
  - Огневые сектора: `FireArcDegrees` (собственный), `UseGridNoseArc`, `GridNoseArcDegrees`

### Система

- `FireControlSystem`:
  - Жизненный цикл: подключение/отключение серверов к гриду при наличии питания; слежение за переносом оружия между гридами; очистка невалидных ссылок
  - Регистрация вооружения: автоматическая, если на одном гриде с активным сервером и хватает `ProcessingPower` (стоимость по `ShipGunClassComponent`)
  - Наведение/стрельба:
    - `TryAimAndFireGrid(grid, worldTarget)`- проверяет дуги, FTL-состояние, выдаёт попытки стрельбы для контролируемых орудий
    - `TryAimAndFireGrid(grid, targetGrid, suggestedAim)`- предсказание перехвата по скорости цели; поддержка салв
    - Проверка LOS/препятствий лучами в пределах грида оружия
    - При успешной проверке вызывает `GunSystem.AttemptShoot`
  - Диагностика: визуализация огневых секторов (сервер рассылает событие клиентам)

---

## Карты и пресеты

- Карты:
  - `PerunAI.yml`: грид c именем `CR-GF "Перун"`, содержит `AiShuttleBrain` с жёсткими параметрами (якорь, радиусы, FTL, вооружение, сервера GCS и консоли). Используется формирование крыльев для одноимённых гридов на карте.
  - `PozvizdAI.yml`: грид `CR-GF "Позвид"` с `AiShuttleBrain` (параметры унесены в пресет), GCS, вооружение и питание.
  - `StribogAI.yml`: грид `CR-GF "Стрибог"`, аналогично.

- Пресеты YAML: `Resources/Prototypes/_Lua/AiShuttlePresets.yml`
  - `AiShuttlePreset` с `nameMatch` по точному имени грида (в MetaData)
  - Примеры: `Perun`, `Stribog`, `Pozvizd`- задают радиусы патруля, дистанции боя, `MaxWeaponRange`, `MinSeparation`

- Пилоты:
  - `MobShuttleAIPilotNF`- невидимый пилот с тегом `CanPilot`, используется при спавне пилота мозгом
  - `MobShuttleAIPilot`- видимый тестовый пилот (с HTN), запасной вариант

---

## Потоки данных и взаимодействие

1) `AiShuttleBrainSystem.Update` (сенсорный тик):
   - Поиск цели среди вражеских гридов; игнорируются дружественные AI-гриды; цель должна иметь рабочую консоль управления шаттлом
   - Обновление `CurrentTarget`, `LastKnownTargetPos`, `FacingMarker`
   - Назначение формаций для PerunAI

2) `AiShuttleBrainSystem.Update` (контрольный тик):
   - Поддержание формации (ведомые берут цель лидера, удерживают `FormationSpacing` с возможностью FTL-догонки)
   - Ведение боя: орбита + радиальная коррекция, избегание препятствий (`ComputeAvoidance`), запрет сближения ниже `MinSeparation`
   - Принятие решения о FTL-прыжке (дистанция > max( `FtlMinDistance`, `MaxWeaponRange * 3` ) и охлаждение)
   - Патруль при отсутствии цели: дрейф/FTL к случайным точкам
   - Передача огневой команды: `FireControlSystem.TryAimAndFireGrid`

3) `FireControlSystem`:
   - Проверяет состояние GCS на гриде, FTL, дуги оружия (`TargetWithinArcs`), LOS (`HasLineOfSight`)
   - При салвах- окно и джиттер на оружие
   - Поворачивает оружие `RotateToFaceSystem` при наличии видимости, стреляет через `GunSystem`

---

## Как изменить поведение ИИ

- Быстрые правки значений на карте:
  - Измените поля компонента `AiShuttleBrain` в YAML карте (например, `PerunAI.yml`): радиусы патруля, дистанции боя, параметры FTL.
  - Плюсы: изолировано для конкретной карты; Минусы: не переиспользуется автоматически.

- Использование пресетов по имени грида:
  - Отредактируйте `Resources/Prototypes/_Lua/AiShuttlePresets.yml` или добавьте новый `AiShuttlePreset` с `nameMatch` точным имени грида (`MetaData.name`).
  - Сервер применит пресет автоматически при первом тике мозга.

- Компонентный пресет на гриде:
  - Добавьте `AiShuttlePresetComponent` на грид (например, через маппер или при спавне), задайте нужные поля.
  - Применяется один раз поверх значений мозга.

- Регулировка агрессивности/дистанций:
  - `FightRangeMin/Max`, `MaxWeaponRange`, `MinSeparation`, `CombatTriggerRadius`
  - FTL-поведение: `FtlMinDistance`, `FtlExitOffset`, `PostFtlStabilizeSeconds`, `FtlCooldownSeconds`

- Формирование крыльев (только Perun):
  - Имена гридов должны совпадать с "Perun" (в MetaData). Группировка по три: индекс 0 лидер, 1/2 ведомые.
  - Удержание строя регулируется `FormationSpacing` у мозга лидера (наследуется ведомыми при расчёте позиции).

- Ведение огня:
  - Корректируйте дуги оружия: `FireArcDegrees` (само оружие) и/или разрешите носовую дугу `UseGridNoseArc` + `GridNoseArcDegrees`.
  - Салвы на сервере: `UseSalvos`, `SalvoPeriodSeconds`, `SalvoWindowSeconds`, `SalvoJitterSeconds` в `FireControlServerComponent`.

- Избежание препятствий:
  - Параметры лучей зашиты в `AiShuttleBrainSystem.ComputeAvoidance` (длины зондов, маска коллизий). Для тонкой настройки потребуется изменить код.

---

## Как добавить нового ИИ-шаттла

1) Создайте карту грида в `Resources/Maps/_Lua/ShuttleEvent/AI_shuttle/YourShip.yml`:
   - Укажите `MetaData.name`, `AiShuttleBrain` (можно без параметров- возьмёт дефолт), GCS сервер (`GunneryServer*`), консоли (`ComputerShuttle`, `ComputerGunneryConsole`), оружие.

2) Добавьте пресет (опционально) в `Resources/Prototypes/_Lua/AiShuttlePresets.yml`:
   - `- type: AiShuttlePreset`, `nameMatch: <ваше имя из MetaData>` и требуемые параметры.

3) Убедитесь, что на гриде есть питаемый `GunneryServer*` и вооружение с `FireControllableComponent` (обычно уже проставляется на оружии).

4) Запустите и проверьте:
   - ИИ должен заспавнить пилота (`MobShuttleAIPilotNF`) и привязать его к консоли шаттла.
   - При появлении противника в пределах `CombatTriggerRadius`- начать манёвр и огонь.

---

## Частые проблемы и диагностика

- ИИ не двигается:
  - Нет `ShuttleConsole` на гриде или пилот не привязался. Проверьте наличие `ComputerShuttle` и питание.
  - Пилот не заспавнился: проверьте наличие прототипа `MobShuttleAIPilotNF` (или `MobShuttleAIPilot`).

- Не стреляет:
  - GCS сервер не подключён к гриду: проверьте питание `GunneryServer*` и что на гриде есть `FireControlGridComponent` (создаётся системой при подключении).
  - Оружие не зарегистрировано: проверьте, что достаточно `ProcessingPower` и что оружие имеет корректный класс (`ShipGunClassComponent`).
  - Цель вне дуги/LOS или грид в FTL.

- Слишком близко подлетает/таранит:
  - Увеличьте `MinSeparation` и/или уменьшите `FightRangeMin`.

- Слишком часто прыгает FTL:
  - Увеличьте `FtlCooldownSeconds` или `FtlMinDistance`; уменьшите `MaxWeaponRange` для снижения порога FTL.

---

## Интерфейсы/методы для интеграции

- Из ИИ к огневому контролю:
  - `FireControlSystem.TryAimAndFireGrid(EntityUid gridUid, Vector2 worldTarget)`
  - `FireControlSystem.TryAimAndFireGrid(EntityUid gridUid, EntityUid targetGridUid, Vector2 suggestedAim)`

- Утилиты диагностики огневых дуг:
  - `FireControlSystem.CountWeaponsAbleToFireAt(gridUid, worldTarget)`- оценка доступного огня
  - `FireControlSystem.ToggleVisualization(entityUid)`- вкл/выкл визуализацию направлений

- Регистрация оружия и состояние сервера:
  - `FireControlSystem.RefreshControllables(gridUid)`
  - `FireControlSystem.GetRemainingProcessingPower(server)`

---

## Политики выбора целей

- Игнорирование дружеских ИИ-шаттлов: цель с `AiShuttleBrainComponent` не будет выбрана для атаки.
- Требование наличия пилотируемой консоли у цели (`ShuttleConsoleComponent`): ориентир для точного наведения (`TryGetEnemyShuttleConsolePos`).
- Фокус по карте: лучшие цели кэшируются в `_globalFocus` для согласованности нескольких ИИ-гридов.

---

## Примечания по расширению

- Для добавления сложной логики (приоритет классов кораблей, учёт брони, групповой фокус) расширяйте сенсорный тик в `AiShuttleBrainSystem.Update` и структуру скоринга.
- Для более продвинутого избежания столкновений (навигационные поля, многолучевой лидара), дорабатывайте `ComputeAvoidance` и параметры зондирования.
- Для координации звена из более чем трёх судов- замените `AssignPerunWings` на алгоритм распределения N-кораблей с ролями и расстояниями.
- Для альтернативных стилей боя- введите режимы и переключайте наборы коэффициентов управления в `DriveViaConsole`.

---

## Ссылки на ключевые места кода

- Мозг ИИ шаттла: `AiShuttleBrainSystem.Update`, `EnsurePilot`, `DriveViaConsole`, `ComputeAvoidance`, `ComputeHullDistance`, `AssignPerunWings`
- Конфигурация мозга: `AiShuttleBrainComponent`
- Пресеты: `AiShuttlePresetComponent`, `AiShuttlePresetPrototype`, `Resources/Prototypes/_Lua/AiShuttlePresets.yml`
- Огневой контроль: `FireControlSystem` (+ методы TryAimAndFireGrid/AttemptFire/TargetWithinArcs/HasLineOfSight)
- Карты примеров: `PerunAI.yml`, `PozvizdAI.yml`, `StribogAI.yml`
- Пилоты: `MobShuttleAIPilotNF`, `MobShuttleAIPilot`

---

## Лицензирование

Код и данные подчиняются лицензиям проекта. См. файлы LICENSE-AGPLv3.txt и LICENSE-MIT.TXT в корне репозитория.

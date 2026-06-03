# Вклад в разработку Sector Frontier

Если вы собираетесь внести вклад в разработку Sector Frontier, обратитесь к [руководству по Pull Request’ам от Wizard's Den](https://docs.spacestation14.com/en/general-development/codebase-info/pull-request-guidelines.html) — оно послужит хорошей отправной точкой по качеству кода и работе с ветками. Обратите внимание, что у нас нет разделения на master/stable ветки.

> ⚠️ **Не используйте веб-редактор GitHub.** Pull Request’ы, созданные через веб-редактор, могут быть закрыты без рассмотрения.

"Upstream" означает [репозиторий space-wizards/space-station-14](https://github.com/new-frontiers-14/frontier-station-14/), из которого был сделан форк.

---

## Контент, специфичный для Фронтира

Всё, что вы создаёте с нуля (в отличие от изменений в существующем upstream-коде), должно размещаться в подкаталогах с префиксом `_Lua`.

**Примеры:**
- `Content.Server/_Lua/Shipyard/Systems/ShipyardSystem.cs`
- `Resources/Prototypes/_Lua/Loadouts/role_loadouts.yml`
- `Resources/Audio/_Lua/Voice/Goblin/goblin-scream-03.ogg`
- `Resources/Textures/_Lua/Tips/clippy.rsi/left.png`
- `Resources/Locale/en-US/_Lua/devices/pda.ftl`
- `Resources/ServerInfo/_Lua/Guidebook/Medical/Doc.xml`

---

## Изменения файлов из upstream

Если вы вносите изменения в C#- или YAML-файлы из upstream, **обязательно добавляйте комментарии около изменённых строк**. Это поможет упростить разрешение конфликтов при будущих обновлениях.

Если вы изменяете значения, используйте формат комментария `Lua: СТАРОЕ<НОВОЕ`.

**Для YAML:**
- Если вы добавляете прототип или набор прототипов подряд — используйте блочные комментарии.
- Если изменяете отдельные поля прототипа — комментируйте каждое по отдельности.

**Для C#:**
- Если вы добавляете много кода, рассмотрите возможность вынесения в `partial class`, когда это уместно.
- Если портируете фичи из upstream, указывайте номер PR-а, из которого брали код.

> ⚠️ Fluent-файлы (.ftl) **не поддерживают комментарии на одной строке с переводом** — оставляйте комментарии строкой выше.

---

## Примеры комментариев

**Изменение поля YAML:**
```yml
- type: entity
  id: TorsoHarpy
  name: "harpy torso"
  parent: [PartHarpy, BaseTorso] #Lua: добавлен BaseTorso
```

**Изменение значения:**
```yml
  - type: Gun
    fireRate: 4 #Lua: 3<4
```

**Добавление нового прототипа:**
```yml
  - type: ItemBorgModule
    moduleId: Gardening #Lua
    items:
    - HydroponicsToolMiniHoe
    - HydroponicsToolSpade
    - HydroponicsToolClippers
    # - Bucket #Lua
  #Lua: добавлены выпадающие борг-компоненты
  - type: DroppableBorgModule
    moduleId: Gardening
    items:
    - id: Bucket
      whitelist:
        tags:
        - Bucket
  # End Lua
```

**Добавление using'а в C#:**
```cs
using Content.Client._NF.Emp.Overlays; //Lua
```

**Обёртка над блоком нового кода:**
```cs
component.Capacity = state.Capacity;

component.UIUpdateNeeded = true;

//Lua Start: синхронизация цвета подписи
if (TryComp<StampComponent>(uid, out var stamp))
{
    stamp.StampedColor = state.Color;
}
//Lua End
```

**Изменение строки в локализации:**
```fluent
#Lua: "Job Whitelists"<"Role Whitelists"
player-panel-job-whitelists = Role Whitelists
```

---

## Карты

По кораблям и POI читайте [Ship Submission Guidelines](https://frontierstation.wiki.gg/wiki/Ship_Submission_Guidelines) на вики Frontier.

В общих чертах:

- Frontier использует специальные прототипы для POI и кораблей, содержащие информацию о спавне, цене и категориях.
- Для кораблей используйте `VesselPrototype` в `Resources/Prototypes/_Lua/Shipyard`, для POI — `PointOfInterestPrototype`.

Если вы вносите изменения в существующую карту, согласуйте это с её мейнтейнером или автором. Избегайте одновременной работы нескольких людей над одной картой — это создаёт конфликты, которые сложно разрешить.

В Sector Frontier особая схемотехника шаттлов, сдедуйте определенным дизайнам шаттлостроения, мы понимаем что в космосе нет аэродинамики но требуем её соблюдать в инных случаях шаттл может быть отклонен по причине несоотвествия концепту проекта
Пример:
<img width="300" height="450" alt="image" src="https://github.com/user-attachments/assets/a2c2d347-321a-441d-8696-49e2d1e7d0df" />
---

## Перед отправкой PR

Перед отправкой проверьте diff на GitHub: убедитесь, что нет случайных изменений, лишних коммитов, пробелов или переносов строк.

Если PR висит давно, и в списке изменений есть `RobustToolbox`, нужно его откатить:
```bash
git checkout upstream/master RobustToolbox
```
*(замените `upstream` на имя вашего origin для HacksLua/sector-frontier)*

---

## Ченджлоги

Пока что все изменения идут в общий ченджлог Фронтира. Префикс `ADMIN:` пока не имеет эффекта.

---

## Дополнительные ресурсы

Если вы новичок в разработке SS14:
- Посмотрите [документацию SS14](https://docs.spacestation14.io/)

---

## Генерированный ИИ-контент

Контент, созданный ИИ (код, спрайты и т.п.), **запрещено** добавлять в репозиторий.

Попытка отправить такой контент может привести к **бану на участие в разработке**.

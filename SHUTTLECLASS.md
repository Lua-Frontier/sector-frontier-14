### Таблица классов урона (с численными порогами)

| Класс | DamageScore |
|------|-------------|
| Superlight | < 500 |
| Light | 500–3 000 |
| Medium | 3 000–12 000 |
| Heavy | 12 000–30 000 |
| Superheavy | ≥ 30 000 |

Примечание по формуле: DamageScore = (Structural + Blunt + Piercing + Heat + Radiation + Explosive.totalIntensity) + 0.1 x Ion + 200 x (EMP.range x EMP.seconds)
Коротко про расчеты:
База урона: Structural + Blunt + Piercing + Heat + Radiation + Explosive.totalIntensity
Складываем прямой урон и “масштаб” взрыва (totalIntensity) как эквивалент базового урона.
Ion с коэффициентом 0.1
Ионка в игре чаще бьёт системы, а не корпус. Даём 10% веса от “обычного” урона.
EMP = 200 x (радиус x длительность)
EMP - Мы переводим эффект в “очки урона”: чем больше радиус и дольше действует, тем сильнее. Вес 200 подобран, чтобы ЭМИ-орудия не занижались до “нулевого” класса.
Итог:
DamageScore = (Structural + Blunt + Piercing + Heat + Radiation + Explosive.totalIntensity) + 0.1 x Ion + 200 x (EMP.range x EMP.seconds)
Пример:
Снаряд: Structural 4000, Blunt 3000, Ion 14000, Explosive.totalIntensity 300, Эми нет.
DamageScore = (4000 + 3000 + 0 + 0 + 0 + 300) + 0.1x14000 + 200x0
= 7300 + 1400
= 8700 -> класс Medium (по нашим расчетам).

В случае с необычным оружем таким как тесла
У теслы нет прямого урона урон идёт через молнии
LightningArcShooter за жизнь прототипа пули (5 c) успевает 1 создать 1–2 молнии (Random.Next(1, 3) -> 1 или 2) на ~4.5 с.
Каждая молния бьёт цель с LightningTargetComponent:
Взрыв: TotalIntensity по умолчанию 25
Структурный урон: DamageFromLightning 1
Оценка на выстрел:
Explosive.totalIntensity ~ 25 x 1..2 = 25–50.
Structural добавит ещё 1–2.
Итого: 26–52 это Superlight (<500)

Добавляемое оружие должно проходить через эти расчеты дабы им была присвоена категория

### Таблица оружия с классами
Это полная таблица корабельного вооружения

| ID | Тип | Класс |
|----|-----|-------|
| WeaponTurretL85Autocannon | Ballistic | Light |
| WeaponTurretDravon | Ballistic | Medium |
| WeaponTurretAK570 | Ballistic | Medium |
| WeaponTurretCyrexa | Ballistic | Heavy |
| WeaponTurretHades | Ballistic | Superheavy |
| WeaponTurretCharonette | Ballistic | Heavy |
| WeaponTurretBofors | Ballistic | Heavy |
| WeaponTurretKargil | Ballistic | Medium |
| WeaponTurretTarnyx | Ballistic | Heavy |
| WeaponTurretTarnyxReload | Ballistic | Heavy |
| WeaponTurretCharon | Ballistic | Superheavy |
| WeaponTurretCharonReload | Ballistic | Superheavy |
| WeaponTurretType35 | Energy | Medium |
| WeaponTurretM25 | Energy | Light |
| WeaponTurretM220 | Energy | Medium |
| WeaponTurretDymere | Energy | Superheavy |
| WeaponTurretVespera | Missile | Light |
| WeaponTurretVanyk | Missile | Heavy |
| WeaponTurretASM501 | Missile | Superheavy |
| WeaponTurretTovek | Missile | Medium |
| WeaponTurretASM220 | Missile | Heavy |
| WeaponTurretLightMunitionsBay | Missile | Heavy |
| ShuttleGunSvalinnMachineGun | Energy | Superlight |
| ShuttleGunPerforator | Energy | Superlight |
| ShuttleGunFriendship | Ballistic | Light |
| ShuttleGunDuster | Ballistic | Medium |
| ShuttleGunPirateCannon | Ballistic | Medium |
| ShuttleGunKinetic | Mining | Light |
| TeslaTurretBase | Energy | Superlight |
| TeslaTurretUnanchor | Energy | Superlight |
| ImpulseLaserBase | Energy | Light |
| ImpulseLaserUnanchor | Energy | Light |
| Weapon20mm | Ballistic | Light |
| Weapon20mmPD | Ballistic | Light |
| Weapon53mm | Ballistic | Medium |
| Weapon80mm | Ballistic | Heavy |
| Weapon105mm | Ballistic | Light |
| Weapon120mm | Ballistic | Medium |
| Weapon140mm | Ballistic | Medium |
| WeaponMissileLauncherNightHunter | Missile | Medium |
| WeaponMissileLauncherLancer | Missile | Superheavy |
| WeaponMissileLauncherCerber | Missile | Superheavy |


### Схема конверсии классов
Каждое корабельное вооружение имеет свое количество очков для теста

| Класс | Стоимость (очков) | Эквиваленты |
|------|--------------------|-------------|
| Superlight | 1 | 4 Superlight = 2 Light = 1 Medium |
| Light | 2 | 2 Light = 1 Medium |
| Medium | 4 | 2 Medium = 1 Heavy |
| Heavy | 8 | 2 Heavy = 1 Superheavy |
| Superheavy | 16 | - |

### Лимиты вооружения по размеру корабля

| Размер | Очки       | Max Superlight | Max Light | Max Medium | Max Heavy | Max Superheavy |
|--------|------------|----------------|-----------|------------|-----------|----------------|
| Micro  | 4          | 4              | 1         | 0          | 0         | 0              |
| Small  | 8          | 6              | 2         | 1          | 0         | 0              |
| Medium | 24         | 24             | 12        | 6          | 3         | 0              |
| Large  | 56         | 56             | 28        | 14         | 7         | 3              |

Примеры конфигураций:
- Micro: 2 Light; или 1 Medium; или 4 Superlight.
- Small: 1 Medium; или 2 Light + 4 Superlight; или 8 Superlight.
- Medium: 3 Heavy; или 6 Medium; или 1 Heavy + 2 Medium + 4 Light.
- Large: 2 Superheavy + 3 Heavy; или 1 Superheavy + 5 Heavy; или 3 Superheavy + 4 Light.


ship-repair-tool-insufficient-ammo = недостаточно зарядов
ship-repair-tool-entity-exists = невозможно починить: оригинал существует
ship-repair-tool-no-data = нет данных ремонта для грида
ship-repair-tool-fail-whitelist = невозможно починить грид

repair-ghost-name = призрак ремонта ({$proto})

# Entities
ent-ShipRepairDeviceBase = СРК
    .desc = Устройство ремонта кораблей, способное восстанавливать разрушенные секции судов.

ent-ShipRepairDevice = СРК
    .desc = Устройство ремонта кораблей, способное восстанавливать разрушенные секции судов. Вмещает 300 зарядов.

ent-ShipRepairDeviceEmpty = СРК
    .desc = { ent-ShipRepairDevice.desc }
    .suffix = Пустой

ent-ShipRepairDeviceRecharging = СРК
    .desc = Устройство ремонта кораблей, способное восстанавливать разрушенные секции судов. Вмещает 300 зарядов и медленно перезаряжается.
    .suffix = Перезарядка

ent-ShipRepairDeviceAdmin = СРК
    .desc = { ent-ShipRepairDeviceBase.desc }
    .suffix = Админ

ent-ShipRepairDeviceRedacted = дофрактурный СРК
    .desc = Загадочное ремонтное устройство, способное чинить корабли ADS.
    .suffix = Перезарядка

ent-ShipRepairDeviceAmmo = материя ремонта кораблей
    .desc = Картридж с зарядами для устройства ремонта кораблей.
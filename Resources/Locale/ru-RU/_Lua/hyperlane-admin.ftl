cmd-starmap-list-desc = Выводит список всех звёзд карты с их MapId и именами.
cmd-starmap-list-help = Использование: starmap_list Печатает индекс, MapId, имя и позицию каждой звезды, известной звёздной карте.
cmd-starmap-list-no-stars = На звёздной карте нет звёзд.
cmd-starmap-list-line = { $index }: MapId={ $mapId }, Имя="{ $name }", Позиция={ $position }

cmd-regenhyperline-desc = Пересобирает гиперлинии звёздной карты и обновляет навигационные консоли.
cmd-regenhyperline-help = Использование: regenhyperline Перестраивает звёзды на карте и полностью пересчитывает все гиперлинии.
cmd-regenhyperline-done = Гиперлинии звёздной карты пересозданы.

cmd-hypeline-desc = Создаёт гиперлинию между двумя звёздами по их MapId.
cmd-hypeline-help = Использование: hyperline <mapIdA> <mapIdB>
cmd-hypeline-star-not-found = Не удалось найти звезду для карт { $mapA } и/или { $mapB }.
cmd-hypeline-added = Гиперлиния между картами { $mapA } и { $mapB } добавлена.
cmd-hypeline-exists = Гиперлиния между картами { $mapA } и { $mapB } уже существует или была принудительно добавлена ранее.

cmd-unhyperline-desc = Удаляет или блокирует гиперлинию между двумя звёздами по их MapId.
cmd-unhyperline-help = Использование: unhyperline <mapIdA> <mapIdB>
cmd-unhyperline-removed = Гиперлиния между картами { $mapA } и { $mapB } удалена и заблокирована.
cmd-unhyperline-blocked-only = Между картами { $mapA } и { $mapB } не было гиперлинии, но её автоматическое создание теперь заблокировано.



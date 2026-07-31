### Popups
reactor-smoke-start = {CAPITALIZE($owner)} начинает дымиться!
reactor-smoke-stop = {CAPITALIZE($owner)} перестаёт дымиться.
reactor-fire-start = {CAPITALIZE($owner)} начинает гореть!
reactor-fire-stop = {CAPITALIZE($owner)} перестаёт гореть.

reactor-unanchor-melted = Нельзя открепить ядерный реактор — он вплавился в корпус!
reactor-unanchor-warning = Нельзя открепить ядерный реактор, пока он не пуст или горячее 80°C!
reactor-anchor-warning = Недопустимое положение крепления.

### Messages
reactor-smoke-start-message = ОПАСНОСТЬ!: {$owner} НА ОПАСНОЙ РАБОЧЕЙ ТЕМПЕРАТУРЕ! РАБОЧАЯ ТЕМПЕРАТУРА: {$temperature}K. РЕКОМЕНДУЕТСЯ НЕМЕДЛЕННОЕ ВМЕШАТЕЛЬСТВО!
reactor-smoke-stop-message = {$owner} вернулся к номинальной рабочей температуре.
reactor-fire-start-message = ВНИМАНИЕ! ВНИМАНИЕ!: {$owner} В КРИТИЧЕСКОМ РЕЖИМЕ! РАБОЧАЯ ТЕМПЕРАТУРА: {$temperature}K. РАСПЛАВЛЕНИЕ РЕАКТОРА НЕМИНУЕМО!
reactor-fire-stop-message = {$owner} вернулся к номинальной рабочей температуре. Расплавление ядра предотвращено.

reactor-temperature-dangerous-message = {$owner} на опасной температуре: {$temperature}K.
reactor-temperature-critical-message = {$owner} на критической температуре: {$temperature}K.
reactor-temperature-cooling-message = {$owner} охлаждается: {$temperature}K.

reactor-melting-announcement = РАСПЛАВЛЕНИЕ ЯДЕРНОГО РЕАКТОРА НЕМИНУЕМО! РЕКОМЕНДУЕТСЯ НЕМЕДЛЕННАЯ ЭВАКУАЦИЯ!
reactor-melting-announcement-sender = ЯДЕРНАЯ АВАРИЯ

reactor-meltdown-announcement = РАСПЛАВЛЕНИЕ ЯДЕРНОГО РЕАКТОРА НАЧАЛОСЬ! ТРЕБУЕТСЯ НЕМЕДЛЕННАЯ ЭВАКУАЦИЯ!
reactor-meltdown-announcement-sender = ЯДЕРНОЕ РАСПЛАВЛЕНИЕ

### UI
comp-nuclear-reactor-ui-locked = ЗАБЛОКИРОВАНО
comp-nuclear-reactor-ui-insert-button = ВСТАВИТЬ
comp-nuclear-reactor-ui-remove-button = ИЗВЛЕЧЬ
comp-nuclear-reactor-ui-eject-button = ВЫБРОСИТЬ

comp-nuclear-reactor-ui-view-change = СМЕНИТЬ ВИД
comp-nuclear-reactor-ui-view-temp = ПРОСМОТР ТЕМПЕРАТУРЫ
comp-nuclear-reactor-ui-view-neutron = ПРОСМОТР НЕЙТРОНОВ
comp-nuclear-reactor-ui-view-target = ПРОСМОТР ЦЕЛИ

comp-nuclear-reactor-ui-status-panel = СТАТУС РЕАКТОРА:
comp-nuclear-reactor-ui-reactor-temp = ВНУТР. ТЕМП.
comp-nuclear-reactor-ui-reactor-rads = ОК. РАДИАЦИЯ
comp-nuclear-reactor-ui-reactor-therm = ТЕПЛ. ЭНЕРГИЯ
comp-nuclear-reactor-ui-reactor-control = УПРАВЛЯЮЩИЕ СТЕРЖНИ
comp-nuclear-reactor-ui-therm-format = { POWERWATTS($power) }t

comp-nuclear-reactor-ui-footer-left = Опасность: высокая радиация.
comp-nuclear-reactor-ui-footer-right = 0.8 REV 3

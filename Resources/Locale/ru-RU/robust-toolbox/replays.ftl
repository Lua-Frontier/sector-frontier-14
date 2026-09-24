# Playback Commands

cmd-replay-play-desc = Возобновить воспроизведение повтора.
cmd-replay-play-help = replay_play
cmd-replay-pause-desc = Приостановить воспроизведение повтора.
cmd-replay-pause-help = replay_pause
cmd-replay-toggle-desc = Возобновить или приостановить воспроизведение повтора.
cmd-replay-toggle-help = replay_toggle
cmd-replay-toggle-screenshot-mode-desc = Переключает режим скриншотов для повторов, скрывая виджет управления.
cmd-replay-toggle-screenshot-mode-help = replay_toggle_screenshot_mode
cmd-replay-stop-desc = Остановить и выгрузить повтор.
cmd-replay-stop-help = replay_stop
cmd-replay-load-desc = Загрузить и запустить повтор.
cmd-replay-load-help = replay_load <папка повтора>
cmd-replay-load-hint = Папка повтора
cmd-replay-skip-desc = Перемотать вперёд или назад по времени.
cmd-replay-skip-help = replay_skip <тик или промежуток времени>
cmd-replay-skip-hint = Тики или промежуток времени (ЧЧ:ММ:СС).
cmd-replay-set-time-desc = Перейти к указанному моменту времени.
cmd-replay-set-time-help = replay_set <тик или время>
cmd-replay-set-time-hint = Тик или промежуток времени (ЧЧ:ММ:СС) от начала
cmd-replay-error-time = "{ $time }" не является целым числом или промежутком времени.
cmd-replay-error-args = Неверное число аргументов.
cmd-replay-error-no-replay = Повтор сейчас не воспроизводится.
cmd-replay-error-already-loaded = Повтор уже загружен.
cmd-replay-error-run-level = Нельзя загрузить повтор, будучи подключённым к серверу.
cmd-replay-toggleui-desc = Переключает UI управления повтором.

# Recording commands

cmd-replay-recording-start-desc = Начинает запись повтора, опционально с ограничением по времени.
cmd-replay-recording-start-help = Использование: replay_recording_start [имя] [overwrite] [лимит времени]
cmd-replay-recording-start-success = Запись повтора начата.
cmd-replay-recording-start-already-recording = Повтор уже записывается.
cmd-replay-recording-start-error = Ошибка при попытке начать запись.
cmd-replay-recording-start-hint-time = [лимит времени (минуты)]
cmd-replay-recording-start-hint-name = [имя]
cmd-replay-recording-start-hint-overwrite = [overwrite (bool)]
cmd-replay-recording-stop-desc = Останавливает запись повтора.
cmd-replay-recording-stop-help = Использование: replay_recording_stop
cmd-replay-recording-stop-success = Запись повтора остановлена.
cmd-replay-recording-stop-not-recording = Запись повтора сейчас не идёт.
cmd-replay-recording-stats-desc = Показывает информацию о текущей записи повтора.
cmd-replay-recording-stats-help = Использование: replay_recording_stats
cmd-replay-recording-stats-result = Длительность: { $time } мин, Тики: { $ticks }, Размер: { $size } МБ, скорость: { $rate } МБ/мин.

# Time Control UI
replay-time-box-scrubbing-label = Динамическая перемотка
replay-time-box-replay-time-label = Время записи: { $current } / { $end }  ({ $percentage }%)
replay-time-box-server-time-label = Время сервера: { $current } / { $end }
replay-time-box-index-label = Индекс: { $current } / { $total }
replay-time-box-tick-label = Тик: { $current } / { $total }

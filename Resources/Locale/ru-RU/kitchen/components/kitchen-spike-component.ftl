comp-kitchen-spike-deny-occupied = { CAPITALIZE($this) } уже занят!
comp-kitchen-spike-deny-butcher = { CAPITALIZE($victim) } не может быть разделан на { $this }.
comp-kitchen-spike-deny-butcher-knife = { CAPITALIZE($victim) } не может быть разделан на { $this }, используйте нож для разделки.
comp-kitchen-spike-deny-not-dead =
    { CAPITALIZE($victim) } не может быть разделан. { CAPITALIZE(SUBJECT($victim)) } { GENDER($victim) ->
        [male] ещё жив
        [female] ещё жива
        [epicene] ещё живы
       *[neuter] ещё живо
    }!
comp-kitchen-spike-begin-hook-self = Вы начинаете насаживать себя на { $hook }!
comp-kitchen-spike-begin-hook-self-other = { CAPITALIZE($victim) } начинает насаживать себя на { $hook }!
comp-kitchen-spike-begin-hook-other-self = Вы начинаете насаживать { $victim } на { $hook }!
comp-kitchen-spike-begin-hook-other = { CAPITALIZE($user) } начинает насаживать { $victim } на { $hook }!
comp-kitchen-spike-hook-self = Вы насадили себя на { $hook }!
comp-kitchen-spike-hook-self-other = { CAPITALIZE($victim) } насаживает себя на { $hook }!
comp-kitchen-spike-hook-other-self = Вы насадили { $victim } на { $hook }!
comp-kitchen-spike-hook-other = { CAPITALIZE($user) } насаживает { $victim } на { $hook }!
comp-kitchen-spike-begin-unhook-self = Вы начинаете снимать себя с { $hook }!
comp-kitchen-spike-begin-unhook-self-other = { CAPITALIZE($victim) } начинает снимать себя с { $hook }!
comp-kitchen-spike-begin-unhook-other-self = Вы начинаете снимать { $victim } с { $hook }!
comp-kitchen-spike-begin-unhook-other = { CAPITALIZE($user) } начинает снимать { $victim } с { $hook }!
comp-kitchen-spike-unhook-self = Вы сняли себя с { $hook }!
comp-kitchen-spike-unhook-self-other = { CAPITALIZE($victim) } снимает себя с { $hook }!
comp-kitchen-spike-unhook-other-self = Вы сняли { $victim } с { $hook }!
comp-kitchen-spike-unhook-other = { CAPITALIZE($user) } снимает { $victim } с { $hook }!
comp-kitchen-spike-begin-butcher-self = Вы начинаете разделывать { $victim }!
comp-kitchen-spike-begin-butcher = { CAPITALIZE($user) } начинает разделывать { $victim }!
comp-kitchen-spike-butcher-self = Вы срезали кусок мяса с { $victim }!
comp-kitchen-spike-butcher = { CAPITALIZE($user) } срезает кусок мяса с { $victim }!
comp-kitchen-spike-butcher-empty = На { $victim } больше не осталось мяса!
comp-kitchen-spike-suicide-other = { CAPITALIZE($victim) } бросается на мясной крюк!
comp-kitchen-spike-suicide-self = Вы бросаетесь на мясной крюк!
comp-kitchen-spike-knife-needed = Вам нужен нож для этого.
comp-kitchen-spike-unhook-verb = Снять с крюка
comp-kitchen-spike-hooked = [color=red]{ CAPITALIZE($victim) } висит на этом крюке![/color]
comp-kitchen-spike-meat-name = мясо { $victim }
comp-kitchen-spike-victim-examine = [color=orange]С этого тела срезали мясо.[/color]
comp-kitchen-spike-deconstruct-occupied = Сначала [color=red]снимите тело с крюка[/color].

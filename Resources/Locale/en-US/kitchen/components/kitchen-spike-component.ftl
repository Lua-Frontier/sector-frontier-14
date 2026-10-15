comp-kitchen-spike-deny-occupied = { CAPITALIZE(THE($this)) } already has something on it!
comp-kitchen-spike-deny-butcher = { CAPITALIZE(THE($victim)) } can't be butchered on { THE($this) }.
comp-kitchen-spike-deny-butcher-knife = { CAPITALIZE(THE($victim)) } can't be butchered on { THE($this) }, you need to butcher it using a knife.
comp-kitchen-spike-deny-not-dead = { CAPITALIZE(THE($victim)) } can't be butchered. { CAPITALIZE(SUBJECT($victim)) } { CONJUGATE-BE($victim) } is not dead!

comp-kitchen-spike-begin-hook-self = You begin dragging yourself onto { THE($hook) }!
comp-kitchen-spike-begin-hook-self-other = { CAPITALIZE(THE($victim)) } begins dragging { REFLEXIVE($victim) } onto { THE($hook) }!

comp-kitchen-spike-begin-hook-other-self = You begin dragging { THE($victim) } onto { THE($hook) }!
comp-kitchen-spike-begin-hook-other = { CAPITALIZE(THE($user)) } begins dragging { THE($victim) } onto { THE($hook) }!

comp-kitchen-spike-hook-self = You threw yourself on { THE($hook) }!
comp-kitchen-spike-hook-self-other = { CAPITALIZE(THE($victim)) } threw { REFLEXIVE($victim) } on { THE($hook) }!

comp-kitchen-spike-hook-other-self = You threw { THE($victim) } on { THE($hook) }!
comp-kitchen-spike-hook-other = { CAPITALIZE(THE($user)) } threw { THE($victim) } on { THE($hook) }!

comp-kitchen-spike-begin-unhook-self = You begin dragging yourself off { THE($hook) }!
comp-kitchen-spike-begin-unhook-self-other = { CAPITALIZE(THE($victim)) } begins dragging { REFLEXIVE($victim) } off { THE($hook) }!

comp-kitchen-spike-begin-unhook-other-self = You begin dragging { THE($victim) } off { THE($hook) }!
comp-kitchen-spike-begin-unhook-other = { CAPITALIZE(THE($user)) } begins dragging { THE($victim) } off { THE($hook) }!

comp-kitchen-spike-unhook-self = You got yourself off { THE($hook) }!
comp-kitchen-spike-unhook-self-other = { CAPITALIZE(THE($victim)) } got { REFLEXIVE($victim) } off { THE($hook) }!

comp-kitchen-spike-unhook-other-self = You got { THE($victim) } off { THE($hook) }!
comp-kitchen-spike-unhook-other = { CAPITALIZE(THE($user)) } got { THE($victim) } off { THE($hook) }!

comp-kitchen-spike-begin-butcher-self = You begin butchering { THE($victim) }!
comp-kitchen-spike-begin-butcher = { CAPITALIZE(THE($user)) } begins to butcher { THE($victim) }!

comp-kitchen-spike-butcher-self = You butchered { THE($victim) }!
comp-kitchen-spike-butcher = { CAPITALIZE(THE($user)) } butchered { THE($victim) }!

comp-kitchen-spike-butcher-empty = { CAPITALIZE(THE($victim)) } has no meat left to butcher!

comp-kitchen-spike-suicide-other = { CAPITALIZE(THE($victim)) } threw { REFLEXIVE($victim) } on { THE($this) }!
comp-kitchen-spike-suicide-self = You throw yourself on { THE($this) }!

comp-kitchen-spike-knife-needed = You need a knife to do this.

comp-kitchen-spike-unhook-verb = Unhook

comp-kitchen-spike-hooked = [color=red]{ CAPITALIZE(THE($victim)) } is on this spike![/color]

comp-kitchen-spike-meat-name = { $name } ({ $victim })

comp-kitchen-spike-victim-examine = [color=orange]{ CAPITALIZE(SUBJECT($target)) } looks quite lean.[/color]

comp-kitchen-spike-deconstruct-occupied = Next, [color=red]unhook the body[/color].

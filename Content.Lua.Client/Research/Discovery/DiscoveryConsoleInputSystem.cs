using Content.Lua.Shared.Research.Discovery;
using Robust.Client.GameObjects;
using Robust.Client.Player;

namespace Content.Client.Lua.Research.Discovery;

public sealed class DiscoveryConsoleInputSystem : DiscoveryConsoleMovementSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly InputSystem _input = default!;

    protected override void OnOperatorShutdown(EntityUid uid)
    {
        if (_player.LocalEntity != uid)
            return;

        _input.SetEntityContextActive();
    }
}

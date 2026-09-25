// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Client.Lua.CryoTimer;
using Content.Client.UserInterface.Systems.Ghost.Widgets;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.Lua.UserInterface.Systems.CryoTimer;

[UsedImplicitly]
public sealed class CryoGhostUIController : UIController, IOnSystemChanged<CryoReturnTimerSystem>
{
    [UISystemDependency] private readonly CryoReturnTimerSystem? _cryo = default;

    private GhostGui? Gui => UIManager.GetActiveUIWidgetOrNull<GhostGui>();

    public void OnSystemLoaded(CryoReturnTimerSystem system)
    {
        system.CryoReturnReseted += OnCryoReturnReseted;
    }

    public void OnSystemUnloaded(CryoReturnTimerSystem system)
    {
        system.CryoReturnReseted -= OnCryoReturnReseted;
    }

    private void OnCryoReturnReseted()
    {
        Gui?.UpdateCryoReturn(_cryo?.CryoReturnTime);
    }
}

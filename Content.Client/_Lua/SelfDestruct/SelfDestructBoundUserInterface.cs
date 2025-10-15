// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Client._Lua.SelfDestruct;
using Content.Shared._Lua.SelfDestruct;
using Robust.Client.UserInterface;
using System.Numerics;

namespace Content.Client._Lua.SelfDestruct;

public sealed class SelfDestructBoundUserInterface : BoundUserInterface
{
    private SelfDestructWindow? _window;

    public SelfDestructBoundUserInterface(EntityUid owner, Enum key) : base(owner, key)
    { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<SelfDestructWindow>();
        _window.MinSize = new Vector2(300, 200);
        _window.OnDigit += d => SendMessage(new SelfDestructEnterDigitMessage(d));
        _window.OnClear += () => SendMessage(new SelfDestructClearMessage());
        _window.OnConfirmWarning += () => SendMessage(new SelfDestructConfirmWarningMessage());
        _window.OnSavePin += () => SendMessage(new SelfDestructSavePinMessage());
        _window.OnArm += () => SendMessage(new SelfDestructArmMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null) return;
        if (state is SelfDestructUiState s) _window.UpdateState(s);
    }
}



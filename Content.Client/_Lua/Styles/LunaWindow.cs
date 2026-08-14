// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using Content.Client.UserInterface.Controls;

namespace Content.Client._Lua.Styles;

[Virtual]
public class LunaWindow : FancyWindow
{
    protected void ApplyLunaChrome()
    {
        LunaWindowStyle.ApplyWindowChrome(this);
    }

    protected override void OnThemeUpdated()
    {
        base.OnThemeUpdated();
        LunaWindowStyle.ApplyWindowChrome(this);
    }
}

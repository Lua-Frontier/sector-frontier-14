using Robust.Client.UserInterface.CustomControls;

namespace Content.Lua.UIKit.Styles;

[Virtual]
public class LunaWindow : DefaultWindow
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

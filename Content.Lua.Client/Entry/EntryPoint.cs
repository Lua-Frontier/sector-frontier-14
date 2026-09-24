using Content.Client.Lua.Machines;
using Content.Client.Lua.Performance;
using Content.Client.Lua.Shuttles;
using Content.Lua.Client.Options;
using Content.Lua.Client.Shuttles;
using Content.Lua.UIKit.Machines;
using Content.Lua.UIKit.Options;
using Content.Lua.UIKit.Performance;
using Content.Lua.UIKit.Shuttles;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;

namespace Content.Lua.Client.Entry;

public sealed class EntryPoint : GameClient
{
    public override void PreInit()
    {
        Dependencies.Register<INetworkOptionsTabFactory, NetworkOptionsTabFactory>();
        Dependencies.Register<IShuttleConsoleLuaScreensFactory, ShuttleConsoleLuaScreensFactory>();
        Dependencies.Register<ILuaMachineUiFactory, LuaMachineUiFactory>();
        Dependencies.Register<IHudPerfLabelFactory, HudPerfLabelFactory>();
        Dependencies.Register<IAccessibilityAnnouncementOptionsConfigurator, AccessibilityAnnouncementOptionsConfigurator>();
        Dependencies.Register<IShuttleRadarLuaDraw, ShuttleRadarLuaDraw>();
    }
}

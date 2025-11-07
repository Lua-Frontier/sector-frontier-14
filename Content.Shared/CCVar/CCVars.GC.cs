using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> ClientGCEnabled =
        CVarDef.Create("client.gc.enabled", false, CVar.CLIENT | CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<int> ClientGCIntervalMinutes =
        CVarDef.Create("client.gc.interval_minutes", 30, CVar.CLIENT | CVar.ARCHIVE | CVar.CLIENTONLY);
}



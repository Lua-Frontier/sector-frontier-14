using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Shared._Lua.NPC.Components;

namespace Content.Server._Lua.NPC;

public sealed class NpcSmartTurretBootstrapSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HTNComponent, ComponentStartup>(OnHtnStartup);
    }

    private void OnHtnStartup(Entity<HTNComponent> ent, ref ComponentStartup args)
    {
        var root = ent.Comp.RootTask.Task;
        if (!IsTurretRoot(root))
            return;

        var smart = EnsureComp<NpcSmartTurretComponent>(ent);
        var blackboard = ent.Comp.Blackboard;

        SetDefault(blackboard, "VisionRadius", smart.VisionRadius);
        SetDefault(blackboard, "AggroVisionRadius", smart.VisionRadius);
        SetDefault(blackboard, "RangedRange", smart.RangedRange);
    }

    private static bool IsTurretRoot(string root)
    {
        return root.Contains("Turret", StringComparison.OrdinalIgnoreCase)
               || root.Contains("PointDefense", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetDefault<T>(NPCBlackboard blackboard, string key, T value)
    {
        if (!blackboard.ContainsKey(key))
            blackboard.SetValue(key, value!);
    }
}

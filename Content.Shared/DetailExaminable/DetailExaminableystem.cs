using Content.Shared.ERP;
using Content.Shared.Examine;
using Content.Lua.Common.CLVar;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared.DetailExaminable;

public sealed class DetailExaminableSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DetailExaminableComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<DetailExaminableComponent> ent, ref ExaminedEvent args)
    {
        if (!_cfg.GetCVar(CLVars.IsERP))
            return;

        if (!HasComp<ActorComponent>(ent))
            return;

        var color = ent.Comp.ERPStatus switch
        {
            EnumERPStatus.FULL => "green",
            EnumERPStatus.HALF => "yellow",
            _ => "red"
        };

        var statusText = FormattedMessage.EscapeText(ent.Comp.GetERPStatusName());
        args.PushMarkup($"[color={color}]{statusText}[/color]");
    }
}

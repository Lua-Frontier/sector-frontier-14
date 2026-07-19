using Content.Shared.Examine;
using Content.Shared.Lua.CLVar;
using Content.Shared._NF.Bank.Components;
using Robust.Shared.Configuration;
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

        if (!HasComp<BankAccountComponent>(ent))
            return;

        var color = ent.Comp.ERPStatus switch
        {
            Content.Shared._Lua.ERP.EnumERPStatus.FULL => "green",
            Content.Shared._Lua.ERP.EnumERPStatus.HALF => "yellow",
            _ => "red"
        };

        var statusText = FormattedMessage.EscapeText(ent.Comp.GetERPStatusName());
        args.PushMarkup($"[color={color}]{statusText}[/color]");
    }
}

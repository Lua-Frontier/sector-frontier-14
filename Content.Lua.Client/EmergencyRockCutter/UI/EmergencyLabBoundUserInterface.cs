// LuaCorp - This file is licensed under AGPLv3
using Content.Lua.Shared.EmergencyRockCutter;
using Content.Shared.EmergencyRockCutter;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Lua.EmergencyRockCutter.UI;

[UsedImplicitly]
public sealed class EmergencyLabBoundUserInterface : BoundUserInterface
{
    private EmergencyLabMenu? _menu;

    public EmergencyLabBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<RCDComponent>(Owner, out var rcd) ||
            !EntMan.TryGetComponent<EmergencyLabAssemblerComponent>(Owner, out _))
        {
            Close();
            return;
        }

        _menu = this.CreateWindowCenteredLeft<EmergencyLabMenu>();
        _menu.OnPrototypeSelected += protoId =>
        {
            SendMessage(new RCDSystemMessage(protoId));
        };

        _menu.Populate(rcd.AvailablePrototypes, rcd.ProtoId);
    }
}

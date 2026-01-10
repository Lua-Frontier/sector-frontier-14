using Content.Shared._Lua.StationRecords;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.StationRecords;
using Robust.Client.UserInterface;

namespace Content.Client.StationRecords;

public sealed class GeneralStationRecordConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private GeneralStationRecordConsoleWindow? _window = default!;

    public GeneralStationRecordConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GeneralStationRecordConsoleWindow>();
        _window.OnKeySelected += key =>
            SendMessage(new SelectStationRecord(key));
        _window.OnFiltersChanged += (type, filterValue) =>
            SendMessage(new SetStationRecordFilter(type, filterValue));
        _window.OnCaptainIdPressed += () => SendMessage(new ItemSlotButtonPressedEvent(ShipCrewManagement.CaptainIdSlotId));
        _window.OnTargetIdPressed += () => SendMessage(new ItemSlotButtonPressedEvent(ShipCrewManagement.TargetIdSlotId));
        _window.OnAssignShipRole += role => SendMessage(new AssignShipCrewRoleMsg(role));
        _window.OnFireSelectedRecord += recordId => SendMessage(new FireShipCrewByRecordMsg(recordId));
        _window.OnSetShipRoleByName += (name, role) => SendMessage(new SetShipCrewRoleByNameMsg(name, role));
        _window.OnFireShipCrewByName += name => SendMessage(new FireShipCrewByNameMsg(name));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not GeneralStationRecordConsoleState cast)
            return;

        _window?.UpdateState(cast);
    }
}

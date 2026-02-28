// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Content.Shared._Lua.Stargate;
using Content.Shared._Lua.Stargate.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server._Lua.Stargate.Systems;

public sealed class StargateAddressEditorSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StargateAddressEditorConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<StargateAddressEditorConsoleComponent, StargateAddressEditorInputSymbolMessage>(OnInputSymbol);
        SubscribeLocalEvent<StargateAddressEditorConsoleComponent, StargateAddressEditorClearInputMessage>(OnClearInput);
        SubscribeLocalEvent<StargateAddressEditorConsoleComponent, StargateAddressEditorSaveToLeftMessage>(OnSaveToLeft);
        SubscribeLocalEvent<StargateAddressEditorConsoleComponent, StargateAddressEditorSaveToRightMessage>(OnSaveToRight);
        SubscribeLocalEvent<StargateAddressEditorConsoleComponent, StargateAddressEditorDeleteFromLeftMessage>(OnDeleteFromLeft);
        SubscribeLocalEvent<StargateAddressEditorConsoleComponent, StargateAddressEditorDeleteFromRightMessage>(OnDeleteFromRight);
        SubscribeLocalEvent<StargateAddressEditorConsoleComponent, StargateAddressEditorMoveLeftToRightMessage>(OnMoveLeftToRight);
        SubscribeLocalEvent<StargateAddressEditorConsoleComponent, StargateAddressEditorMoveRightToLeftMessage>(OnMoveRightToLeft);
        SubscribeLocalEvent<StargateAddressEditorConsoleComponent, StargateAddressEditorCopyLeftToRightMessage>(OnCopyLeftToRight);
        SubscribeLocalEvent<StargateAddressEditorConsoleComponent, StargateAddressEditorCopyRightToLeftMessage>(OnCopyRightToLeft);
        SubscribeLocalEvent<StargateAddressEditorConsoleComponent, StargateAddressEditorCloneLeftToRightMessage>(OnCloneLeftToRight);
        SubscribeLocalEvent<StargateAddressEditorConsoleComponent, StargateAddressEditorCloneRightToLeftMessage>(OnCloneRightToLeft);
        SubscribeLocalEvent<StargateAddressEditorConsoleComponent, EntInsertedIntoContainerMessage>(OnDiskInserted);
        SubscribeLocalEvent<StargateAddressEditorConsoleComponent, EntRemovedFromContainerMessage>(OnDiskRemoved);
    }

    private void OnUiOpened(EntityUid uid, StargateAddressEditorConsoleComponent comp, BoundUIOpenedEvent args)
    {
        UpdateUi(uid, comp);
    }

    private void OnInputSymbol(EntityUid uid, StargateAddressEditorConsoleComponent comp, StargateAddressEditorInputSymbolMessage args)
    {
        var requiredLen = GetRequiredLength(comp.CurrentInput, args.Symbol);
        if (comp.CurrentInput.Count >= requiredLen)
            return;

        if (args.Symbol < 1 || args.Symbol > 40)
            return;

        if (comp.CurrentInput.Contains(args.Symbol))
            return;

        comp.CurrentInput.Add(args.Symbol);
        UpdateUi(uid, comp);
    }

    private void OnClearInput(EntityUid uid, StargateAddressEditorConsoleComponent comp, StargateAddressEditorClearInputMessage args)
    {
        comp.CurrentInput.Clear();
        UpdateUi(uid, comp);
    }

    private static int GetRequiredLength(List<byte> currentInput, byte nextSymbol)
    {
        if (currentInput.Count == 0 && nextSymbol == 1)
            return 7;
        if (currentInput.Count > 0 && currentInput[0] == 1)
            return 7;
        return 6;
    }

    private static bool ValidateAddress(byte[] addr)
    {
        if (addr.Length != 6 && addr.Length != 7)
            return false;
        var seen = new HashSet<byte>();
        foreach (var s in addr)
        {
            if (s < 1 || s > 40)
                return false;
            if (!seen.Add(s))
                return false;
        }
        return true;
    }

    private void OnSaveToLeft(EntityUid uid, StargateAddressEditorConsoleComponent comp, StargateAddressEditorSaveToLeftMessage args)
    {
        var disk = GetLeftDisk(uid);
        if (disk == null || !TryComp<StargateAddressDiskComponent>(disk, out var diskComp))
            return;

        var addr = comp.CurrentInput.ToArray();
        if (!ValidateAddress(addr))
            return;

        if (AddressExists(diskComp, addr))
            return;

        diskComp.Addresses.Add(new List<byte>(addr));
        Dirty(disk.Value, diskComp);
        UpdateUi(uid, comp);
    }

    private void OnSaveToRight(EntityUid uid, StargateAddressEditorConsoleComponent comp, StargateAddressEditorSaveToRightMessage args)
    {
        var disk = GetRightDisk(uid);
        if (disk == null || !TryComp<StargateAddressDiskComponent>(disk, out var diskComp))
            return;

        var addr = comp.CurrentInput.ToArray();
        if (!ValidateAddress(addr))
            return;

        if (AddressExists(diskComp, addr))
            return;

        diskComp.Addresses.Add(new List<byte>(addr));
        Dirty(disk.Value, diskComp);
        UpdateUi(uid, comp);
    }

    private static bool AddressExists(StargateAddressDiskComponent disk, IList<byte> addr)
    {
        foreach (var existing in disk.Addresses)
        {
            if (existing.Count != addr.Count)
                continue;
            var match = true;
            for (var i = 0; i < existing.Count; i++)
            {
                if (existing[i] != addr[i])
                {
                    match = false;
                    break;
                }
            }
            if (match)
                return true;
        }
        return false;
    }

    private void OnDeleteFromLeft(EntityUid uid, StargateAddressEditorConsoleComponent comp, StargateAddressEditorDeleteFromLeftMessage args)
    {
        var disk = GetLeftDisk(uid);
        if (disk == null || !TryComp<StargateAddressDiskComponent>(disk, out var diskComp))
            return;
        if (args.Index < 0 || args.Index >= diskComp.Addresses.Count)
            return;
        diskComp.Addresses.RemoveAt(args.Index);
        Dirty(disk.Value, diskComp);
        UpdateUi(uid, comp);
    }

    private void OnDeleteFromRight(EntityUid uid, StargateAddressEditorConsoleComponent comp, StargateAddressEditorDeleteFromRightMessage args)
    {
        var disk = GetRightDisk(uid);
        if (disk == null || !TryComp<StargateAddressDiskComponent>(disk, out var diskComp))
            return;
        if (args.Index < 0 || args.Index >= diskComp.Addresses.Count)
            return;
        diskComp.Addresses.RemoveAt(args.Index);
        Dirty(disk.Value, diskComp);
        UpdateUi(uid, comp);
    }

    private void OnMoveLeftToRight(EntityUid uid, StargateAddressEditorConsoleComponent comp, StargateAddressEditorMoveLeftToRightMessage args)
    {
        var left = GetLeftDisk(uid);
        var right = GetRightDisk(uid);
        if (left == null || right == null || !TryComp<StargateAddressDiskComponent>(left, out var leftComp) || !TryComp<StargateAddressDiskComponent>(right, out var rightComp))
            return;
        if (args.Index < 0 || args.Index >= leftComp.Addresses.Count)
            return;

        var addr = leftComp.Addresses[args.Index];
        if (AddressExists(rightComp, addr))
            return;
        leftComp.Addresses.RemoveAt(args.Index);
        rightComp.Addresses.Add(new List<byte>(addr));
        Dirty(left.Value, leftComp);
        Dirty(right.Value, rightComp);
        UpdateUi(uid, comp);
    }

    private void OnMoveRightToLeft(EntityUid uid, StargateAddressEditorConsoleComponent comp, StargateAddressEditorMoveRightToLeftMessage args)
    {
        var left = GetLeftDisk(uid);
        var right = GetRightDisk(uid);
        if (left == null || right == null || !TryComp<StargateAddressDiskComponent>(left, out var leftComp) || !TryComp<StargateAddressDiskComponent>(right, out var rightComp))
            return;
        if (args.Index < 0 || args.Index >= rightComp.Addresses.Count)
            return;

        var addr = rightComp.Addresses[args.Index];
        if (AddressExists(leftComp, addr))
            return;
        rightComp.Addresses.RemoveAt(args.Index);
        leftComp.Addresses.Add(new List<byte>(addr));
        Dirty(left.Value, leftComp);
        Dirty(right.Value, rightComp);
        UpdateUi(uid, comp);
    }

    private void OnCopyLeftToRight(EntityUid uid, StargateAddressEditorConsoleComponent comp, StargateAddressEditorCopyLeftToRightMessage args)
    {
        var left = GetLeftDisk(uid);
        var right = GetRightDisk(uid);
        if (left == null || right == null || !TryComp<StargateAddressDiskComponent>(left, out var leftComp) || !TryComp<StargateAddressDiskComponent>(right, out var rightComp))
            return;
        if (args.Index < 0 || args.Index >= leftComp.Addresses.Count)
            return;

        var addr = leftComp.Addresses[args.Index];
        if (AddressExists(rightComp, addr))
            return;
        rightComp.Addresses.Add(new List<byte>(addr));
        Dirty(right.Value, rightComp);
        UpdateUi(uid, comp);
    }

    private void OnCopyRightToLeft(EntityUid uid, StargateAddressEditorConsoleComponent comp, StargateAddressEditorCopyRightToLeftMessage args)
    {
        var left = GetLeftDisk(uid);
        var right = GetRightDisk(uid);
        if (left == null || right == null || !TryComp<StargateAddressDiskComponent>(left, out var leftComp) || !TryComp<StargateAddressDiskComponent>(right, out var rightComp))
            return;
        if (args.Index < 0 || args.Index >= rightComp.Addresses.Count)
            return;

        var addr = rightComp.Addresses[args.Index];
        if (AddressExists(leftComp, addr))
            return;
        leftComp.Addresses.Add(new List<byte>(addr));
        Dirty(left.Value, leftComp);
        UpdateUi(uid, comp);
    }

    private void OnCloneLeftToRight(EntityUid uid, StargateAddressEditorConsoleComponent comp, StargateAddressEditorCloneLeftToRightMessage args)
    {
        var left = GetLeftDisk(uid);
        var right = GetRightDisk(uid);
        if (left == null || right == null || !TryComp<StargateAddressDiskComponent>(left, out var leftComp) || !TryComp<StargateAddressDiskComponent>(right, out var rightComp))
            return;

        foreach (var addr in leftComp.Addresses)
        {
            if (!AddressExists(rightComp, addr))
                rightComp.Addresses.Add(new List<byte>(addr));
        }

        Dirty(right.Value, rightComp);
        UpdateUi(uid, comp);
    }

    private void OnCloneRightToLeft(EntityUid uid, StargateAddressEditorConsoleComponent comp, StargateAddressEditorCloneRightToLeftMessage args)
    {
        var left = GetLeftDisk(uid);
        var right = GetRightDisk(uid);
        if (left == null || right == null || !TryComp<StargateAddressDiskComponent>(left, out var leftComp) || !TryComp<StargateAddressDiskComponent>(right, out var rightComp))
            return;

        foreach (var addr in rightComp.Addresses)
        {
            if (!AddressExists(leftComp, addr))
                leftComp.Addresses.Add(new List<byte>(addr));
        }

        Dirty(left.Value, leftComp);
        UpdateUi(uid, comp);
    }

    private void OnDiskInserted(EntityUid uid, StargateAddressEditorConsoleComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == "left_disk_slot" || args.Container.ID == "right_disk_slot")
            UpdateUi(uid, comp);
    }

    private void OnDiskRemoved(EntityUid uid, StargateAddressEditorConsoleComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == "left_disk_slot" || args.Container.ID == "right_disk_slot")
            UpdateUi(uid, comp);
    }

    private EntityUid? GetLeftDisk(EntityUid uid)
    {
        if (!_itemSlots.TryGetSlot(uid, "left_disk_slot", out var slot))
            return null;
        return slot.Item;
    }

    private EntityUid? GetRightDisk(EntityUid uid)
    {
        if (!_itemSlots.TryGetSlot(uid, "right_disk_slot", out var slot))
            return null;
        return slot.Item;
    }

    public void UpdateUi(EntityUid uid, StargateAddressEditorConsoleComponent comp)
    {
        byte[][]? leftAddresses = null;
        var leftDisk = GetLeftDisk(uid);
        if (leftDisk != null && TryComp<StargateAddressDiskComponent>(leftDisk, out var leftDiskComp))
        {
            leftAddresses = new byte[leftDiskComp.Addresses.Count][];
            for (var i = 0; i < leftDiskComp.Addresses.Count; i++)
                leftAddresses[i] = leftDiskComp.Addresses[i].ToArray();
        }

        byte[][]? rightAddresses = null;
        var rightDisk = GetRightDisk(uid);
        if (rightDisk != null && TryComp<StargateAddressDiskComponent>(rightDisk, out var rightDiskComp))
        {
            rightAddresses = new byte[rightDiskComp.Addresses.Count][];
            for (var i = 0; i < rightDiskComp.Addresses.Count; i++)
                rightAddresses[i] = rightDiskComp.Addresses[i].ToArray();
        }

        var state = new StargateAddressEditorUiState(
            comp.CurrentInput.ToArray(),
            40,
            leftAddresses,
            rightAddresses
        );

        _ui.SetUiState(uid, StargateAddressEditorUiKey.Key, state);
    }
}

// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp
// See AGPLv3.txt for details.

using Robust.Shared.Serialization;

namespace Content.Shared._Lua.Stargate;
public static class StargateGlyphs
{
    public const string Charmap = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmn";
    public const int Count = 40;

    public static char GetChar(byte symbol)
    {
        var idx = symbol - 1;
        if (idx < 0 || idx >= Charmap.Length)
            return '?';
        return Charmap[idx];
    }
    public static byte GetByte(char c)
    {
        var idx = Charmap.IndexOf(c);
        if (idx < 0)
            return 0;
        return (byte)(idx + 1);
    }
    public static byte[]? ParseAddress(string glyphs)
    {
        var result = new byte[glyphs.Length];
        for (var i = 0; i < glyphs.Length; i++)
        {
            var b = GetByte(glyphs[i]);
            if (b == 0)
                return null;
            result[i] = b;
        }
        return result;
    }
    public static string ToGlyphString(byte[] address)
    {
        var chars = new char[address.Length];
        for (var i = 0; i < address.Length; i++)
            chars[i] = GetChar(address[i]);
        return new string(chars);
    }
}

[Serializable, NetSerializable]
public enum StargateConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum StargateVisuals : byte
{
    State,
    IrisState
}

[Serializable, NetSerializable]
public enum StargateVisualState : byte
{
    Off,
    Starting,
    Opening,
    Idle,
    Closing
}

[Serializable, NetSerializable]
public enum StargateIrisVisualState : byte
{
    Open,
    Opening,
    Closing,
    Closed
}

[Serializable, NetSerializable]
public enum StargateVisualLayers : byte
{
    Portal,
    Iris
}

[Serializable, NetSerializable]
public sealed class StargateConsoleUiState : BoundUserInterfaceState
{
    public byte[] CurrentInput;
    public bool PortalOpen;
    public byte MaxSymbols;
    public byte[] GateAddress;
    public bool Dialing;
    public bool AutoDialing;
    public byte[][]? DiskAddresses;
    public bool HasControllable;
    public bool IrisOpen;

    public StargateConsoleUiState(byte[] currentInput, bool portalOpen, byte maxSymbols, byte[] gateAddress, bool dialing, bool autoDialing, byte[][]? diskAddresses, bool hasControllable, bool irisOpen)
    {
        CurrentInput = currentInput;
        PortalOpen = portalOpen;
        MaxSymbols = maxSymbols;
        GateAddress = gateAddress;
        Dialing = dialing;
        AutoDialing = autoDialing;
        DiskAddresses = diskAddresses;
        HasControllable = hasControllable;
        IrisOpen = irisOpen;
    }
}

[Serializable, NetSerializable]
public sealed class StargateDialMessage : BoundUserInterfaceMessage
{
    public byte[] Symbols;

    public StargateDialMessage(byte[] symbols)
    {
        Symbols = symbols;
    }
}

[Serializable, NetSerializable]
public sealed class StargateInputSymbolMessage : BoundUserInterfaceMessage
{
    public byte Symbol;

    public StargateInputSymbolMessage(byte symbol)
    {
        Symbol = symbol;
    }
}

[Serializable, NetSerializable]
public sealed class StargateClearInputMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class StargateClosePortalMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class StargateToggleIrisMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class StargateSaveDiskAddressMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class StargateDeleteDiskAddressMessage : BoundUserInterfaceMessage
{
    public int Index;

    public StargateDeleteDiskAddressMessage(int index)
    {
        Index = index;
    }
}

[Serializable, NetSerializable]
public sealed class StargateAutoDialFromDiskMessage : BoundUserInterfaceMessage
{
    public byte[] Address;

    public StargateAutoDialFromDiskMessage(byte[] address)
    {
        Address = address;
    }
}

[Serializable, NetSerializable]
public enum StargateAddressEditorUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class StargateAddressEditorUiState : BoundUserInterfaceState
{
    public byte[] CurrentInput;
    public byte MaxSymbols;
    public byte[][]? LeftDiskAddresses;
    public byte[][]? RightDiskAddresses;

    public StargateAddressEditorUiState(byte[] currentInput, byte maxSymbols, byte[][]? leftDiskAddresses, byte[][]? rightDiskAddresses)
    {
        CurrentInput = currentInput;
        MaxSymbols = maxSymbols;
        LeftDiskAddresses = leftDiskAddresses;
        RightDiskAddresses = rightDiskAddresses;
    }
}

[Serializable, NetSerializable]
public sealed class StargateAddressEditorInputSymbolMessage : BoundUserInterfaceMessage
{
    public byte Symbol;
    public StargateAddressEditorInputSymbolMessage(byte symbol) { Symbol = symbol; }
}

[Serializable, NetSerializable]
public sealed class StargateAddressEditorClearInputMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class StargateAddressEditorSaveToLeftMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class StargateAddressEditorSaveToRightMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class StargateAddressEditorDeleteFromLeftMessage : BoundUserInterfaceMessage
{
    public int Index;
    public StargateAddressEditorDeleteFromLeftMessage(int index) { Index = index; }
}

[Serializable, NetSerializable]
public sealed class StargateAddressEditorDeleteFromRightMessage : BoundUserInterfaceMessage
{
    public int Index;
    public StargateAddressEditorDeleteFromRightMessage(int index) { Index = index; }
}

[Serializable, NetSerializable]
public sealed class StargateAddressEditorMoveLeftToRightMessage : BoundUserInterfaceMessage
{
    public int Index;
    public StargateAddressEditorMoveLeftToRightMessage(int index) { Index = index; }
}

[Serializable, NetSerializable]
public sealed class StargateAddressEditorMoveRightToLeftMessage : BoundUserInterfaceMessage
{
    public int Index;
    public StargateAddressEditorMoveRightToLeftMessage(int index) { Index = index; }
}

[Serializable, NetSerializable]
public sealed class StargateAddressEditorCopyLeftToRightMessage : BoundUserInterfaceMessage
{
    public int Index;
    public StargateAddressEditorCopyLeftToRightMessage(int index) { Index = index; }
}

[Serializable, NetSerializable]
public sealed class StargateAddressEditorCopyRightToLeftMessage : BoundUserInterfaceMessage
{
    public int Index;
    public StargateAddressEditorCopyRightToLeftMessage(int index) { Index = index; }
}

[Serializable, NetSerializable]
public sealed class StargateAddressEditorCloneLeftToRightMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class StargateAddressEditorCloneRightToLeftMessage : BoundUserInterfaceMessage { }

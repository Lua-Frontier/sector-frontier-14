using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Content.Shared.UserInterface;

namespace Content.Shared.DeadSpace.Polaroid;

public static class PolaroidSharedConstants
{
    public const int MaxPhotoSignatureLength = 26;
}

[Serializable, NetSerializable]
public enum PolaroidCameraUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class PolaroidCameraUiState : BoundUserInterfaceState
{
    public readonly NetEntity? PreviewCamera;
    public readonly GameTick Tick;
    public readonly int CurrentCharges;
    public readonly int MaxCharges;
    public readonly bool HasLastCapture;
    public readonly int ViewportPixelSize;

    public PolaroidCameraUiState(
        NetEntity? previewCamera,
        GameTick tick,
        int currentCharges,
        int maxCharges,
        bool hasLastCapture,
        int viewportPixelSize)
    {
        PreviewCamera = previewCamera;
        Tick = tick;
        CurrentCharges = currentCharges;
        MaxCharges = maxCharges;
        HasLastCapture = hasLastCapture;
        ViewportPixelSize = viewportPixelSize;
    }
}

[Serializable, NetSerializable]
public sealed class PolaroidCaptureMessage : BoundUserInterfaceMessage
{
    public readonly byte[] Png;

    public PolaroidCaptureMessage(byte[] png)
    {
        Png = png;
    }
}

[Serializable, NetSerializable]
public sealed class PolaroidPrintLastMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public enum PolaroidPhotoUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class PolaroidPhotoUiState : BoundUserInterfaceState
{
    public readonly byte[] Png;
    public readonly string? Photographer;
    public readonly string? TakenAt;
    public readonly string? Signature;

    public PolaroidPhotoUiState(byte[] png, string? photographer, string? takenAt, string? signature)
    {
        Png = png;
        Photographer = photographer;
        TakenAt = takenAt;
        Signature = signature;
    }
}

[Serializable, NetSerializable]
public sealed class PolaroidPhotoSetSignatureMessage : BoundUserInterfaceMessage
{
    public readonly string Signature;

    public PolaroidPhotoSetSignatureMessage(string signature)
    {
        Signature = signature;
    }
}

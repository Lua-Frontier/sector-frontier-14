using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Polaroid;

[RegisterComponent]
public sealed partial class PolaroidCameraComponent : Component
{
    [DataField(required: true)]
    public ItemSlot CartridgeSlot = new();

    [DataField]
    public EntProtoId PreviewCameraPrototype = "PolaroidPreviewCamera";

    [DataField]
    public EntProtoId PhotoPrototype = "PolaroidPhoto";

    [DataField]
    public int ViewportPixelSize = 160;

    [DataField]
    public int MaxPayloadBytes = 256 * 1024;

    [DataField]
    public int MaxCaptureDimension = 512;

    [DataField]
    public SoundSpecifier ShutterSound = new SoundPathSpecifier("/Audio/Machines/shutter.ogg");

    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    [ViewVariables]
    public EntityUid? CurrentViewer;

    [ViewVariables]
    public EntityUid? PreviewCamera;

    [ViewVariables]
    public byte[] LastCapture = Array.Empty<byte>();

    [ViewVariables]
    public bool HasLastCapture;

    [ViewVariables]
    public string? LastCapturePhotographer;

    [ViewVariables]
    public DateTime? LastCaptureTakenAt;
}

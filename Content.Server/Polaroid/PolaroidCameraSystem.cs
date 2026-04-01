using System.IO;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Coordinates;
using Content.Shared.DeadSpace.Polaroid;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Server.DeadSpace.Polaroid;

public sealed class PolaroidCameraSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const string CartridgeSlotId = "cartridge";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PolaroidCameraComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<PolaroidCameraComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<PolaroidCameraComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<PolaroidCameraComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PolaroidCameraComponent, EntInsertedIntoContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<PolaroidCameraComponent, EntRemovedFromContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<PolaroidCameraComponent, AfterActivatableUIOpenEvent>(OnUiOpened);
        SubscribeLocalEvent<PolaroidCameraComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<PolaroidCameraComponent, GotUnequippedHandEvent>(OnUnequippedHand);
        SubscribeLocalEvent<PolaroidCameraComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<PolaroidCameraComponent, PolaroidCaptureMessage>(OnCaptureMessage);
        SubscribeLocalEvent<PolaroidCameraComponent, PolaroidPrintLastMessage>(OnPrintLastMessage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PolaroidCameraComponent>();
        while (query.MoveNext(out _, out var component))
        {
            if (component.CurrentViewer is not { } viewer ||
                component.PreviewCamera is not { } preview)
            {
                continue;
            }

            var viewerCoords = Transform(viewer).Coordinates;
            if (Transform(preview).Coordinates != viewerCoords)
                _transform.SetCoordinates(preview, viewerCoords);
        }
    }

    private void OnComponentInit(EntityUid uid, PolaroidCameraComponent component, ComponentInit args)
    {
        _itemSlots.AddItemSlot(uid, CartridgeSlotId, component.CartridgeSlot);
    }

    private void OnComponentRemove(EntityUid uid, PolaroidCameraComponent component, ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(uid, component.CartridgeSlot);
    }

    private void OnComponentShutdown(EntityUid uid, PolaroidCameraComponent component, ComponentShutdown args)
    {
        CleanupPreview(uid, component);
    }

    private void OnMapInit(EntityUid uid, PolaroidCameraComponent component, MapInitEvent args)
    {
        UpdateUi(uid, component);
    }

    private void OnSlotChanged(EntityUid uid, PolaroidCameraComponent component, ContainerModifiedMessage args)
    {
        if (args.Container.ID != component.CartridgeSlot.ID)
            return;

        UpdateUi(uid, component);
    }

    private void OnUiOpened(EntityUid uid, PolaroidCameraComponent component, AfterActivatableUIOpenEvent args)
    {
        CleanupPreview(uid, component);

        if (!TryComp(args.Actor, out ActorComponent? actor))
        {
            UpdateUi(uid, component);
            return;
        }

        component.PreviewCamera = SpawnAttachedTo(component.PreviewCameraPrototype, args.Actor.ToCoordinates());
        component.CurrentViewer = args.Actor;
        _viewSubscriber.AddViewSubscriber(component.PreviewCamera.Value, actor.PlayerSession);

        UpdateUi(uid, component);
    }

    private void OnUiClosed(EntityUid uid, PolaroidCameraComponent component, BoundUIClosedEvent args)
    {
        CleanupPreview(uid, component);
        UpdateUi(uid, component);
    }

    private void OnUnequippedHand(EntityUid uid, PolaroidCameraComponent component, GotUnequippedHandEvent args)
    {
        CleanupPreview(uid, component);
    }

    private void OnDropped(EntityUid uid, PolaroidCameraComponent component, DroppedEvent args)
    {
        CleanupPreview(uid, component);
    }

    private void OnCaptureMessage(EntityUid uid, PolaroidCameraComponent component, PolaroidCaptureMessage args)
    {
        if (!CanUseCamera(uid, component, args.Actor))
            return;

        if (args.Png.Length == 0 || args.Png.Length > component.MaxPayloadBytes)
        {
            _popup.PopupEntity(Loc.GetString("polaroid-camera-popup-invalid-capture"), uid, args.Actor);
            return;
        }

        if (!TryGetLoadedCartridge(uid, component, out var cartridgeUid, out var cartridge) || cartridge.CurrentAmount <= 0)
        {
            PopupChargeFailure(uid, args.Actor, cartridgeUid);
            return;
        }

        if (!ValidateCapture(args.Png, component))
        {
            _popup.PopupEntity(Loc.GetString("polaroid-camera-popup-invalid-capture"), uid, args.Actor);
            return;
        }

        cartridge.CurrentAmount--;

        component.LastCapture = args.Png;
        component.HasLastCapture = true;
        component.LastCapturePhotographer = Identity.Name(args.Actor, EntityManager);
        component.LastCaptureTakenAt = DateTime.UtcNow;

        SpawnPhoto(uid, args.Actor, component.LastCapture, component.LastCapturePhotographer, component.LastCaptureTakenAt);
        _audio.PlayPvs(component.ShutterSound, uid);
        _audio.PlayPvs(component.PrintSound, uid);

        UpdateUi(uid, component);
    }

    private void OnPrintLastMessage(EntityUid uid, PolaroidCameraComponent component, PolaroidPrintLastMessage args)
    {
        if (!CanUseCamera(uid, component, args.Actor))
            return;

        if (!component.HasLastCapture || component.LastCapture.Length == 0)
        {
            _popup.PopupEntity(Loc.GetString("polaroid-camera-popup-no-last-capture"), uid, args.Actor);
            return;
        }

        if (!TryGetLoadedCartridge(uid, component, out var cartridgeUid, out var cartridge) || cartridge.CurrentAmount <= 0)
        {
            PopupChargeFailure(uid, args.Actor, cartridgeUid);
            return;
        }

        cartridge.CurrentAmount--;
        SpawnPhoto(uid, args.Actor, component.LastCapture, component.LastCapturePhotographer, component.LastCaptureTakenAt);
        _audio.PlayPvs(component.PrintSound, uid);

        UpdateUi(uid, component);
    }

    private bool CanUseCamera(EntityUid uid, PolaroidCameraComponent component, EntityUid actor)
    {
        if (component.CurrentViewer != actor)
            return false;

        if (!_ui.IsUiOpen(uid, PolaroidCameraUiKey.Key, actor))
            return false;

        if (!TryComp(actor, out HandsComponent? hands))
            return false;

        if (!_hands.IsHolding((actor, hands), uid, out var hand))
            return false;

        return hands.ActiveHandId == hand;
    }

    private bool TryGetLoadedCartridge(
        EntityUid uid,
        PolaroidCameraComponent component,
        out EntityUid? cartridgeUid,
        out PolaroidCartridgeComponent cartridge)
    {
        cartridgeUid = null;
        cartridge = default!;

        if (component.CartridgeSlot.Item is not { } loadedCartridge)
            return false;

        if (!TryComp<PolaroidCartridgeComponent>(loadedCartridge, out var cartridgeComp))
            return false;

        cartridgeUid = loadedCartridge;
        cartridge = cartridgeComp;
        return true;
    }

    private void PopupChargeFailure(EntityUid uid, EntityUid user, EntityUid? cartridge)
    {
        var message = cartridge == null
            ? Loc.GetString("polaroid-camera-popup-no-cartridge")
            : Loc.GetString("polaroid-camera-popup-cartridge-empty");

        _popup.PopupEntity(message, uid, user);
    }

    private bool ValidateCapture(byte[] png, PolaroidCameraComponent component)
    {
        try
        {
            using var stream = new MemoryStream(png, writable: false);
            using var image = Image.Load<Rgba32>(stream);

            if (image.Width <= 0 || image.Height <= 0)
                return false;

            if (image.Width != image.Height)
                return false;

            if (image.Width > component.MaxCaptureDimension || image.Height > component.MaxCaptureDimension)
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SpawnPhoto(
        EntityUid camera,
        EntityUid user,
        byte[] png,
        string? photographer,
        DateTime? takenAt,
        PolaroidCameraComponent? component = null)
    {
        if (!Resolve(camera, ref component))
            return;

        var photo = Spawn(component.PhotoPrototype, Transform(user).Coordinates);
        var photoComp = EnsureComp<PolaroidPhotoComponent>(photo);

        photoComp.PngData = (byte[]) png.Clone();
        photoComp.Photographer = photographer;
        photoComp.TakenAt = takenAt ?? DateTime.UtcNow;

        _hands.PickupOrDrop(user, photo, checkActionBlocker: false);
    }

    private void CleanupPreview(EntityUid uid, PolaroidCameraComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.PreviewCamera is { } preview)
        {
            if (component.CurrentViewer is { } viewer &&
                TryComp<ActorComponent>(viewer, out var actor))
            {
                _viewSubscriber.RemoveViewSubscriber(preview, actor.PlayerSession);
            }

            Del(preview);
        }

        component.PreviewCamera = null;
        component.CurrentViewer = null;
    }

    private void UpdateUi(EntityUid uid, PolaroidCameraComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var charges = 0;
        var maxCharges = 0;

        if (TryGetLoadedCartridge(uid, component, out _, out var cartridge))
        {
            charges = cartridge.CurrentAmount;
            maxCharges = cartridge.MaxAmount;
        }

        var state = new PolaroidCameraUiState(
            GetNetEntity(component.PreviewCamera),
            _timing.CurTick,
            charges,
            maxCharges,
            component.HasLastCapture,
            component.ViewportPixelSize);

        _ui.SetUiState(uid, PolaroidCameraUiKey.Key, state);
    }
}

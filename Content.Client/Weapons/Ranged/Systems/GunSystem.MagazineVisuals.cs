using Content.Client.Weapons.Ranged.Components;
using Content.Shared.Hands;
using Content.Shared.Item;
using Content.Shared.Rounding;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.GameObjects;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    [Dependency] private readonly TagSystem _tagSystem = default!;

    private void InitializeMagazineVisuals()
    {
        SubscribeLocalEvent<MagazineVisualsComponent, ComponentInit>(OnMagazineVisualsInit);
        SubscribeLocalEvent<MagazineVisualsComponent, AppearanceChangeEvent>(OnMagazineVisualsChange);
        //SubscribeLocalEvent<MagazineVisualsComponent, GetInhandVisualsEvent>(OnGetHeldVisuals); //huh?!
    }

    private void OnMagazineVisualsInit(EntityUid uid, MagazineVisualsComponent component, ComponentInit args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite)) return;

        if (_tagSystem.HasTag(uid, "BatteryWeaponFireModesSprites"))
        {
            if (TryComp<BatteryWeaponFireModesComponent>(uid, out var batteryFireModes))
            {
                if (_sprite.LayerMapTryGet((uid, sprite), GunVisualLayers.Mag, out _, false))
                {
                    _sprite.LayerSetRsiState((uid, sprite), GunVisualLayers.Mag, $"{component.MagState}-{component.MagSteps - 1}-{batteryFireModes.CurrentFireMode}");
                    _sprite.LayerSetVisible((uid, sprite), GunVisualLayers.Mag, false);
                }

                if (_sprite.LayerMapTryGet((uid, sprite), GunVisualLayers.MagUnshaded, out _, false))
                {
                    _sprite.LayerSetRsiState((uid, sprite), GunVisualLayers.MagUnshaded, $"{component.MagState}-unshaded-{component.MagSteps - 1}-{batteryFireModes.CurrentFireMode}");
                    _sprite.LayerSetVisible((uid, sprite), GunVisualLayers.MagUnshaded, false);
                }
            }
        }
        else
        {
            if (_sprite.LayerMapTryGet((uid, sprite), GunVisualLayers.Mag, out _, false))
            {
                _sprite.LayerSetRsiState((uid, sprite), GunVisualLayers.Mag, $"{component.MagState}-{component.MagSteps - 1}");
                _sprite.LayerSetVisible((uid, sprite), GunVisualLayers.Mag, false);
            }

            if (_sprite.LayerMapTryGet((uid, sprite), GunVisualLayers.MagUnshaded, out _, false))
            {
                _sprite.LayerSetRsiState((uid, sprite), GunVisualLayers.MagUnshaded, $"{component.MagState}-unshaded-{component.MagSteps - 1}");
                _sprite.LayerSetVisible((uid, sprite), GunVisualLayers.MagUnshaded, false);
            }
        }
    }

    private void OnMagazineVisualsChange(EntityUid uid, MagazineVisualsComponent component, ref AppearanceChangeEvent args)
    {
        // tl;dr
        // 1.If no mag then hide it OR
        // 2. If step 0 isn't visible then hide it (mag or unshaded)
        // 3. Otherwise just do mag / unshaded as is
        var sprite = args.Sprite;

        if (sprite == null) return;

        if (!args.AppearanceData.TryGetValue(AmmoVisuals.MagLoaded, out var magloaded) ||
            magloaded is true)
        {
            if (!args.AppearanceData.TryGetValue(AmmoVisuals.AmmoMax, out var capacity))
            {
                capacity = component.MagSteps;
            }

            if (!args.AppearanceData.TryGetValue(AmmoVisuals.AmmoCount, out var current))
            {
                current = component.MagSteps;
            }

            var step = ContentHelpers.RoundToLevels((int)current, (int)capacity, component.MagSteps);

            if (step == 0 && !component.ZeroVisible)
            {
                if (_sprite.LayerMapTryGet((uid, sprite), GunVisualLayers.Mag, out _, false))
                {
                    _sprite.LayerSetVisible((uid, sprite), GunVisualLayers.Mag, false);
                }

                if (_sprite.LayerMapTryGet((uid, sprite), GunVisualLayers.MagUnshaded, out _, false))
                {
                    _sprite.LayerSetVisible((uid, sprite), GunVisualLayers.MagUnshaded, false);
                }

                return;
            }
            if (_tagSystem.HasTag(uid, "BatteryWeaponFireModesSprites"))
            {
                if (TryComp<BatteryWeaponFireModesComponent>(uid, out var batteryFireModes))
                {
                    if (_sprite.LayerMapTryGet((uid, sprite), GunVisualLayers.Mag, out _, false))
                    {
                        _sprite.LayerSetVisible((uid, sprite), GunVisualLayers.Mag, true);
                        _sprite.LayerSetRsiState((uid, sprite), GunVisualLayers.Mag, $"{component.MagState}-{step}-{batteryFireModes.CurrentFireMode}");
                    }

                    if (_sprite.LayerMapTryGet((uid, sprite), GunVisualLayers.MagUnshaded, out _, false))
                    {
                        _sprite.LayerSetVisible((uid, sprite), GunVisualLayers.MagUnshaded, true);
                        _sprite.LayerSetRsiState((uid, sprite), GunVisualLayers.MagUnshaded, $"{component.MagState}-unshaded-{step}-{batteryFireModes.CurrentFireMode}");
                    }
                }
            }
            else
            {
            if (_sprite.LayerMapTryGet((uid, sprite), GunVisualLayers.Mag, out _, false))
            {
                _sprite.LayerSetVisible((uid, sprite), GunVisualLayers.Mag, true);
                _sprite.LayerSetRsiState((uid, sprite), GunVisualLayers.Mag, $"{component.MagState}-{step}");
            }

            if (_sprite.LayerMapTryGet((uid, sprite), GunVisualLayers.MagUnshaded, out _, false))
            {
                _sprite.LayerSetVisible((uid, sprite), GunVisualLayers.MagUnshaded, true);
                _sprite.LayerSetRsiState((uid, sprite), GunVisualLayers.MagUnshaded, $"{component.MagState}-unshaded-{step}");
                }
            }
        }
        else
        {
            if (_sprite.LayerMapTryGet((uid, sprite), GunVisualLayers.Mag, out _, false))
            {
                _sprite.LayerSetVisible((uid, sprite), GunVisualLayers.Mag, false);
            }

            if (_sprite.LayerMapTryGet((uid, sprite), GunVisualLayers.MagUnshaded, out _, false))
            {
                _sprite.LayerSetVisible((uid, sprite), GunVisualLayers.MagUnshaded, false);
            }
        }
    }

    //private void OnGetHeldVisuals(EntityUid uid, MagazineVisualsComponent component, GetInhandVisualsEvent args)
    //{
    //    if (!TryComp(uid, out AppearanceComponent? _))
    //        return;

    //    if (!TryComp(uid, out ItemComponent? _))
    //        return;

    //    var layer = new PrototypeLayerData();
    //    var key = $"inhand-{args.Location.ToString().ToLowerInvariant()}";
    //    layer.State = key;
    //    args.Layers.Add((key, layer));
    //}
}

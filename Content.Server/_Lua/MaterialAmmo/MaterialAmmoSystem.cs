// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2025 LuaWorld
// See AGPLv3.txt for details.

using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Stacks;
using Content.Server.Stack;
using Content.Server.Power.EntitySystems;

namespace Content.Server._Lua.MaterialAmmo
{
    public sealed class MaterialChargerSystem : EntitySystem
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly BatterySystem _powerSystem = default!;
        [Dependency] private readonly StackSystem _stackHandler = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MaterialAmmoComponent, InteractUsingEvent>(HandleMaterialInsert);
        }

        private void HandleMaterialInsert(EntityUid uid, MaterialAmmoComponent component, InteractUsingEvent args)
        {
            if (component.AllowedMaterials == null)
                return;

            _entityManager.TryGetComponent<PhysicalCompositionComponent>(args.Used, out var composition);
            if (composition == null)
                return;
            _entityManager.TryGetComponent<StackComponent>(args.Used, out var stack);
            if (stack == null)
                return;

            foreach (var materialType in component.AllowedMaterials)
            {
                if (composition.MaterialComposition.ContainsKey(materialType)) ProcessBatteryRecharge(uid, args.Used, composition.MaterialComposition[materialType], stack.Count);
            }
        }

        private void ProcessBatteryRecharge(EntityUid charger, EntityUid materialEntity, int unitsPerSheet, int stackSize)
        {
            var availableCapacity = _powerSystem.GetChargeDifference(charger);
            if (availableCapacity == 0)
                return;
            var totalMaterial = unitsPerSheet * stackSize;
            var remainingMaterial = totalMaterial - availableCapacity;
            var chargeAmount = 0;

            if (remainingMaterial == 0)
            {
                chargeAmount = totalMaterial;
            }
            else if (remainingMaterial > 0)
            {
                chargeAmount = (totalMaterial - remainingMaterial);
            }
            else
            {
                chargeAmount = Math.Abs(Math.Abs(remainingMaterial) - availableCapacity);
            }

            _powerSystem.AddCharge(charger, chargeAmount);
            var removedEntity = _stackHandler.Split(materialEntity, chargeAmount / unitsPerSheet, Transform(materialEntity).Coordinates);
            QueueDel(removedEntity);
        }
    }
}

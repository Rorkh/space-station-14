using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.DisplacementMap;
using Content.Client.Inventory;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Timing;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client.DeadSpace.Containers.ItemSlot;

public sealed class ClientItemSlotsSystem : ItemSlotsSystem
{
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly DisplacementMapSystem _displacement = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemSlotsComponent, ItemSlotInsertEvent>(OnInsert);
    }

    private void RenderItemSlot(EntityUid equipee, Shared.Containers.ItemSlots.ItemSlot slot, string slotId, EntityUid item, InventoryComponent? inventory = null, SpriteComponent? sprite = null, ClothingComponent? clothingComponent = null,
        InventorySlotsComponent? inventorySlots = null)
    {
        if (!Resolve(equipee, ref inventory, ref sprite, ref inventorySlots) ||
           !Resolve(item, ref clothingComponent, false))
        {
            return;
        }

        if (slot.RenderSlot == null)
            return;

        if (!_inventorySystem.TryGetSlot(equipee, slot.RenderSlot, out var slotDef, inventory))
            return;

        if (inventorySlots.VisualLayerKeys.TryGetValue($"item-slot-{slotId}", out var revealedLayers))
        {
            foreach (var key in revealedLayers)
            {
                sprite.RemoveLayer(key);
            }
            revealedLayers.Clear();
        }
        else
        {
            revealedLayers = new();
        }

        var layers = GetItemSlotLayers(slot, slotId, clothingComponent);

        if (layers.Count == 0)
            return;

        bool slotLayerExists = sprite.LayerMapTryGet(slot.RenderSlot, out var index);

        var displacementData = inventory.Displacements.GetValueOrDefault(slot.RenderSlot);

        var equipeeSex = CompOrNull<HumanoidAppearanceComponent>(equipee)?.Sex;
        if (equipeeSex != null)
        {
            switch (equipeeSex)
            {
                case Sex.Male:
                    if (inventory.MaleDisplacements.Count > 0)
                        displacementData = inventory.MaleDisplacements.GetValueOrDefault(slot.RenderSlot);
                    break;
                case Sex.Female:
                    if (inventory.FemaleDisplacements.Count > 0)
                        displacementData = inventory.FemaleDisplacements.GetValueOrDefault(slot.RenderSlot);
                    break;
            }
        }

        foreach (var (key, layerData) in layers)
        {
            if (!revealedLayers.Add(key))
            {
                Log.Warning($"Duplicate key for clothing visuals: {key}. Are multiple components attempting to modify the same layer? Equipment: {ToPrettyString(slot.Item)}");
                continue;
            }

            if (slotLayerExists)
            {
                index++;
                // note that every insertion requires reshuffling & remapping all the existing layers.
                sprite.AddBlankLayer(index);
                sprite.LayerMapSet(key, index);

                if (layerData.Color != null)
                    sprite.LayerSetColor(key, layerData.Color.Value);
                if (layerData.Scale != null)
                    sprite.LayerSetScale(key, layerData.Scale.Value);
            }
            else
                index = sprite.LayerMapReserveBlank(key);

            if (sprite[index] is not Layer layer)
                continue;

            // In case no RSI is given, use the item's base RSI as a default. This cuts down on a lot of unnecessary yaml entries.
            /*if (layerData.RsiPath == null
                && layerData.TexturePath == null
                && layer.RSI == null
                && TryComp(equipment, out SpriteComponent? clothingSprite))
            {
                layer.SetRsi(clothingSprite.BaseRSI);
            }*/

            sprite.LayerSetData(index, layerData);
            layer.Offset += slotDef.Offset;

            if (displacementData is not null)
            {
                //Checking that the state is not tied to the current race. In this case we don't need to use the displacement maps.
                if (layerData.State is not null && inventory.SpeciesId is not null && layerData.State.EndsWith(inventory.SpeciesId))
                    continue;

                if (_displacement.TryAddDisplacement(displacementData, sprite, index, key, revealedLayers))
                    index++;
            }
        }
    }

    private void OnInsert(EntityUid uid, ItemSlotsComponent component, ref ItemSlotInsertEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        foreach (var (slotId, slot) in component.Slots)
        {
            Logger.Debug($"iterating slot {slotId} in OnInsert");
            if (slot.RenderSlot == null)
                continue;
            Logger.Debug("Should be rendered");
            if (slot.Item == null || args.User == null)
                continue;

            // slotId???
            RenderItemSlot((EntityUid) args.User, slot, slotId, (EntityUid) slot.Item);
        }
    }

    private List<(string, PrototypeLayerData)> GetItemSlotLayers(Shared.Containers.ItemSlots.ItemSlot slot, string slotId, ClothingComponent clothing)
    {
        // Ugly bastard. Remove later
        List<(string, PrototypeLayerData)> result = new();

        List<PrototypeLayerData> layers = new();

        // Slot.Item check???
        if (!clothing.ClothingVisuals.TryGetValue(slotId, out var slotLayers) && slot.Item != null)
        {
            TryGetDefaultVisuals((EntityUid) slot.Item, clothing, slotId, out slotLayers);
        }

        // hmm... get rid of slotLayers???
        if (slotLayers != null)
        {
            layers = layers.Concat(slotLayers).ToList();
        }

        var i = 0;
        foreach (var layer in layers)
        {
            // Я это нихуя не понимаю!
            var key = layer.MapKeys?.FirstOrDefault();
            if (key == null)
            {
                key = $"item-slot-{slotId}-{i}";
                i++;
            }
            // Конец того, что я нихуя не понимаю
            result.Add((key, layer));
        }

        return result;
    }

    private bool TryGetDefaultVisuals(EntityUid uid, ClothingComponent clothing, string itemSlot,
        [NotNullWhen(true)] out List<PrototypeLayerData>? layers)
    {
        layers = null;

        RSI? rsi = null;

        if (clothing.RsiPath != null)
            rsi = _cache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / clothing.RsiPath).RSI;
        else if (TryComp(uid, out SpriteComponent? sprite))
            rsi = sprite.BaseRSI;

        if (rsi == null)
            return false;

        var state = $"item-slot-{itemSlot}";

        if (!rsi.TryGetState(state, out _))
            return false;

        var layer = new PrototypeLayerData();
        layer.RsiPath = rsi.Path.ToString();
        layer.State = state;
        layers = new() { layer };

        return true;
    }
}

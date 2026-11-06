using System.Text.Json;
using DevilGate.Scripts.Data;
using DevilGate.Scripts.Definitions;
using DevilGate.Scripts.Systems;
using DevilGate.Scripts.World;
using Godot;

namespace DevilGate.Scripts.Systems;

/// <summary>
/// One manual save slot under user://saves/. Writes go to a temp file first and
/// are then moved over the previous save, so a failed write preserves both the
/// crystal and the prior valid save.
/// </summary>
public static class SaveService
{
    public const string SaveFileName = "devilgate_save.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>Test hook: when set, save files go here instead of user://saves.</summary>
    public static string? DirectoryOverride { get; set; }

    public static string SaveDirectory =>
        DirectoryOverride ?? Path.Combine(ProjectSettings.GlobalizePath("user://"), "saves");

    public static string SavePath => Path.Combine(SaveDirectory, SaveFileName);
    public static string TempPath => SavePath + ".tmp";

    public sealed record SaveResult(bool Success, string? Error = null)
    {
        public static SaveResult Ok() => new(true);
        public static SaveResult Fail(string error) => new(false, error);
    }

    public static bool SaveExists() => File.Exists(SavePath);

    /// <summary>Serialize and atomically replace the save file.</summary>
    public static SaveResult WriteSave(SaveData data)
    {
        try
        {
            Directory.CreateDirectory(SaveDirectory);
            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(TempPath, json);
            File.Move(TempPath, SavePath, overwrite: true);
            return SaveResult.Ok();
        }
        catch (Exception ex)
        {
            GD.PushError($"SaveService: write failed: {ex.Message}");
            TryDelete(TempPath);
            return SaveResult.Fail($"Could not write save file: {ex.Message}");
        }
    }

    /// <summary>Delete the save slot (used by tests / reset flows).</summary>
    public static void DeleteSave()
    {
        TryDelete(SavePath);
        TryDelete(TempPath);
    }

    /// <summary>
    /// Read and fully validate the save file. On any problem the current
    /// session is left untouched: returns false with a player-facing error.
    /// </summary>
    public static bool TryLoadSave(out SaveData? data, out string error)
    {
        data = null;
        error = "";

        if (!SaveExists())
        {
            error = "No save file found.";
            return false;
        }

        string json;
        try
        {
            json = File.ReadAllText(SavePath);
        }
        catch (Exception ex)
        {
            error = $"Could not read save file: {ex.Message}";
            return false;
        }

        SaveData? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<SaveData>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            error = $"Save file is corrupt: {ex.Message}";
            return false;
        }

        if (parsed == null)
        {
            error = "Save file is empty or corrupt.";
            return false;
        }

        var validation = Validate(parsed, GameCatalog.Instance);
        if (validation != null)
        {
            error = validation;
            return false;
        }

        // Recover invalid positions to the safehouse spawn point.
        if (!(parsed.LocationId != "safehouse" ? CourtyardRenderer.Bounds : SafehouseLayout.PlayableBounds).HasPoint(new Vector2(parsed.PositionX, parsed.PositionY)))
        {
            parsed.PositionX = (parsed.LocationId != "safehouse" ? CourtyardRenderer.Spawn : SafehouseLayout.SpawnPoint).X;
            parsed.PositionY = (parsed.LocationId != "safehouse" ? CourtyardRenderer.Spawn : SafehouseLayout.SpawnPoint).Y;
        }

        data = parsed;
        return true;
    }

    /// <summary>Returns null when valid, otherwise a player-facing reason.</summary>
    private static string? Validate(SaveData data, GameCatalog catalog)
    {
        if (!WorldRoute.Valid(WorldRoute.Normalize(data.LocationId))) return "Unknown saved location.";
        if (data.InductionStage < 0 || data.InductionStage > 3) return "Invalid induction mission stage.";
        var battleError=WoodlandBattle.Validate(data.WoodlandBattle);
        if(battleError!=null)return battleError;
        if(data.WoodlandBattle.Stage is 1 or 3 && data.LocationId!="woodland") return "Active woodland battle must be saved on the woodland path.";
        var guardError=WoodlandBattle.Validate(data.FilterBattle,24);
        if(guardError!=null)return guardError;
        var echoError=EchoMission.Validate(data.Echo,data.FilterBattle,data.WoodlandBattle.Stage,data.InductionStage);
        if(echoError!=null)return echoError;
        if(data.FilterBattle.Stage is 1 or 3 && data.LocationId!="grove")return "Active relay battle must be saved in Willow grove.";
        var magicError = StarterTraining.Validate(data.ArchetypeId,data.MagicAwakened,data.LearnedSpellIds);
        if(magicError != null) return magicError;
        if(data.MagicAwakened && (!StarterTraining.IsSupported(data.ArchetypeId) || data.CreationStage != 2 || data.InductionStage != 3)) return "Invalid magic training progression.";
        if (data.CreationStage < 0 || data.CreationStage > 2) return "Invalid character setup stage.";
        if (data.Version != SaveData.CurrentVersion)
        {
            return $"Unsupported save version {data.Version} (expected {SaveData.CurrentVersion}).";
        }
        if (string.IsNullOrWhiteSpace(data.RunId) || string.IsNullOrWhiteSpace(data.Name))
        {
            return "Save file is missing character identity.";
        }
        if (!catalog.TryGetArchetype(data.ArchetypeId, out _))
        {
            return $"Save file references unknown archetype '{data.ArchetypeId}'.";
        }
        if (data.GenderId is not (Ids.GenderMale or Ids.GenderFemale))
        {
            return $"Save file references unknown gender '{data.GenderId}'.";
        }

        var attrs = data.Attributes;
        if (attrs == null)
        {
            return "Save file is missing attributes.";
        }
        foreach (var kind in AttributeSet.All)
        {
            var v = attrs[kind];
            if (v < CharacterRules.AttributeMin || v > CharacterRules.AttributeMax)
            {
                return $"Save file has out-of-range attribute {CharacterRules.DisplayName(kind)}={v}.";
            }
        }
        if (attrs.Total != CharacterRules.RequiredTotal)
        {
            return $"Save file attribute total {attrs.Total} is invalid.";
        }

        var maxHp = CharacterRules.MaxHealth(attrs,data.Level);
        var maxMana = CharacterRules.MaxMana(attrs,data.Level);
        if (data.Hp < 0 || data.Hp > maxHp) return $"Save file health {data.Hp} is out of range (max {maxHp}).";
        if (data.Mana < 0 || data.Mana > maxMana) return $"Save file mana {data.Mana} is out of range (max {maxMana}).";
        if (data.Level < 1) return "Save file has invalid level.";
        if (data.Xp < 0) return "Save file has invalid XP.";
        if (data.CarriedGold < 0 || data.ChestGold < 0) return "Save file has invalid gold.";

        if (!ValidateSlots(data.BackpackSlots, catalog, "backpack")) return $"Save file backpack is invalid: {LastSlotError}";
        if (!ValidateSlots(data.ChestSlots, catalog, "chest")) return $"Save file chest is invalid: {LastSlotError}";

        if (data.Weapon != null && !ValidateStack(data.Weapon, catalog)) return $"Save file weapon is invalid: {LastSlotError}";
        if (data.Armour != null && !ValidateStack(data.Armour, catalog)) return $"Save file armour is invalid: {LastSlotError}";

        return null;
    }

    private static string LastSlotError = "";

    private static bool ValidateSlots(ItemStack?[]? slots, GameCatalog catalog, string where)
    {
        LastSlotError = "";
        if (slots == null || slots.Length != InventoryState.SlotCount)
        {
            LastSlotError = $"{where} must have {InventoryState.SlotCount} slots.";
            return false;
        }
        foreach (var stack in slots)
        {
            if (stack == null) continue;
            if (!ValidateStack(stack, catalog)) return false;
        }
        return true;
    }

    private static bool ValidateStack(ItemStack stack, GameCatalog catalog)
    {
        if (string.IsNullOrEmpty(stack.ItemId) || !catalog.TryGetItem(stack.ItemId, out var item) || item == null)
        {
            LastSlotError = $"unknown item '{stack.ItemId}'.";
            return false;
        }
        if (stack.Quantity < 1 || stack.Quantity > item.StackLimit)
        {
            LastSlotError = $"{item.DisplayName} quantity {stack.Quantity} out of range.";
            return false;
        }
        return true;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"SaveService: could not delete {path}: {ex.Message}");
        }
    }
}

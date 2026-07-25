using System.Text.Json;

namespace BlobForge.World;

public enum BasinVolumeUnit : byte
{
    Gallons,
    Liters
}

public readonly record struct WeaponCatalogEntry(
    string Code,
    string Name,
    decimal Cost,
    bool StartingUnlocked = false);

public readonly record struct DayPayout(
    int AbsoluteDay,
    int Year,
    int DayOfYear,
    float BloodGallons,
    float BloodLiters,
    decimal BloodRatePerGallon,
    decimal BloodPayout,
    int ProcessedBlobs,
    decimal ProcessedRate,
    decimal ProcessedPayout,
    decimal TotalPayout,
    decimal CurrencyAfterSale);

/// <summary>
/// Persistent progression and economy state. Physical simulation quantities stay
/// in world-volume units; payout conversion happens through BloodBasin's authored
/// real-world tank calibration so changing the player's display unit cannot alter
/// earnings.
/// </summary>
public sealed class GameProgression
{
    public const int DaysPerYear = 365;
    public const decimal BaseBloodRatePerGallon = 2.75m;
    public const decimal BaseProcessedBlobRate = 18m;

    public static readonly IReadOnlyList<WeaponCatalogEntry> WeaponCatalog =
    [
        new("CLEAVER", "BUTCHER CLEAVER", 0m, true),
        new("LIGHTSABER", "LIGHTSABER", 8_500m),
        new("NAIL_GUN", "NAIL GUN", 1_200m),
        new("SHOTGUN", "SHOTGUN", 2_400m),
        new("MAGNUM", "MAGNUM", 3_100m),
        new("SMG", "SMG", 4_200m),
        new("BLADE_SHOOTER", "BLADE SHOOTER", 5_200m),
        new("CHIPPER_VAC", "CHIPPER VAC", 5_800m),
        new("SLEDGEHAMMER", "SLEDGEHAMMER", 1_700m),
        new("SLINGSHOT", "BLOB SLINGSHOT", 2_100m),
        new("WALL_PIKE", "WALL PIKE", 1_400m),
        new("BOXING_GLOVE", "BOXING GLOVE", 1_600m),
        new("GRENADES", "GRENADES", 3_600m),
        new("WHIRLWIND_AXE", "WHIRLWIND AXE", 4_800m),
        new("BLACK_HOLE", "BLACK HOLE", 12_500m),
        new("RAT_GUN", "RAT GUN", 6_600m),
        new("ENLARGER", "ENLARGER", 8_800m),
        new("FLAMETHROWER", "FLAMETHROWER", 7_200m),
        new("FREEZE_RAY", "FREEZE RAY", 7_600m),
        new("LIGHTNING_COIL", "LIGHTNING COIL", 8_200m),
        new("ACID_LOBBER", "ACID LOBBER", 6_900m),
        new("WATER_DOLL", "WATER DOLL", 5_400m),
        new("BAT_BALL", "BAT + BALL", 2_800m)
    ];

    private readonly string? _path;
    private readonly HashSet<string> _unlockedWeapons =
        new(StringComparer.OrdinalIgnoreCase);

    private GameProgression(string? path)
    {
        _path = path;
        _unlockedWeapons.Add("CLEAVER");
    }

    public int AbsoluteDay { get; private set; } = 1;
    public decimal Currency { get; private set; }
    public BasinVolumeUnit VolumeUnit { get; private set; } = BasinVolumeUnit.Gallons;
    public decimal BloodRatePerGallon { get; private set; } = BaseBloodRatePerGallon;
    public decimal ProcessedBlobRate { get; private set; } = BaseProcessedBlobRate;
    public int Year => (Math.Max(1, AbsoluteDay) - 1) / DaysPerYear + 1;
    public int DayOfYear => (Math.Max(1, AbsoluteDay) - 1) % DaysPerYear + 1;
    public IReadOnlySet<string> UnlockedWeapons => _unlockedWeapons;

    public static GameProgression Load()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BlobForge");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "progression.json");
        var progression = new GameProgression(path);
        try
        {
            if (!File.Exists(path)) return progression;
            var data = JsonSerializer.Deserialize<ProgressionData>(File.ReadAllText(path));
            if (data is null) return progression;
            progression.AbsoluteDay = Math.Max(1, data.AbsoluteDay);
            progression.Currency = Math.Max(0m, data.Currency);
            progression.VolumeUnit = Enum.IsDefined(data.VolumeUnit)
                ? data.VolumeUnit
                : BasinVolumeUnit.Gallons;
            progression.BloodRatePerGallon = data.BloodRatePerGallon > 0m
                ? data.BloodRatePerGallon
                : BaseBloodRatePerGallon;
            progression.ProcessedBlobRate = data.ProcessedBlobRate > 0m
                ? data.ProcessedBlobRate
                : BaseProcessedBlobRate;
            if (data.UnlockedWeapons is not null)
                foreach (var code in data.UnlockedWeapons)
                    if (WeaponCatalog.Any(item =>
                            item.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
                        progression._unlockedWeapons.Add(code);
        }
        catch
        {
            // Invalid or unavailable persistence falls back to a playable new game.
        }
        return progression;
    }

    internal static GameProgression CreateTransient() => new(null);

    public bool IsWeaponUnlocked(string code) => _unlockedWeapons.Contains(code);

    public bool TryUnlockWeapon(string code)
    {
        var item = WeaponCatalog.FirstOrDefault(candidate =>
            candidate.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(item.Code) || _unlockedWeapons.Contains(item.Code) ||
            Currency < item.Cost)
            return false;
        Currency -= item.Cost;
        _unlockedWeapons.Add(item.Code);
        Save();
        return true;
    }

    public void ToggleVolumeUnit()
    {
        VolumeUnit = VolumeUnit == BasinVolumeUnit.Gallons
            ? BasinVolumeUnit.Liters
            : BasinVolumeUnit.Gallons;
        Save();
    }

    public DayPayout CompleteDay(BloodBasin basin, int processedBlobs)
    {
        var gallons = basin.EstimatedStoredGallons;
        var liters = basin.EstimatedStoredLiters;
        var payout = CompleteDay(gallons, liters, processedBlobs);
        basin.SellAllStoredBlood();
        return payout;
    }

    public DayPayout CompleteDay(float gallons, float liters, int processedBlobs)
    {
        gallons = MathF.Max(0f, gallons);
        liters = MathF.Max(0f, liters);
        var bloodPayout = decimal.Round(
            (decimal)gallons * BloodRatePerGallon,
            2,
            MidpointRounding.AwayFromZero);
        processedBlobs = Math.Max(0, processedBlobs);
        var processedPayout = processedBlobs * ProcessedBlobRate;
        var total = bloodPayout + processedPayout;
        Currency += total;
        var payout = new DayPayout(
            AbsoluteDay,
            Year,
            DayOfYear,
            gallons,
            liters,
            BloodRatePerGallon,
            bloodPayout,
            processedBlobs,
            ProcessedBlobRate,
            processedPayout,
            total,
            Currency);
        Save();
        return payout;
    }

    public void AdvanceDay()
    {
        AbsoluteDay = Math.Max(1, AbsoluteDay + 1);
        Save();
    }

    public string DayLabel(bool includeYearWhenNeeded = true)
    {
        if (includeYearWhenNeeded && Year > 1) return $"YEAR {Year}  •  DAY {DayOfYear}";
        return $"DAY {DayOfYear}";
    }

    public void Save()
    {
        if (string.IsNullOrEmpty(_path)) return;
        try
        {
            var data = new ProgressionData
            {
                AbsoluteDay = AbsoluteDay,
                Currency = Currency,
                VolumeUnit = VolumeUnit,
                BloodRatePerGallon = BloodRatePerGallon,
                ProcessedBlobRate = ProcessedBlobRate,
                UnlockedWeapons = _unlockedWeapons.OrderBy(code => code).ToArray()
            };
            File.WriteAllText(_path,
                JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // A save failure must not interrupt the active factory session.
        }
    }

    private sealed class ProgressionData
    {
        public int AbsoluteDay { get; set; } = 1;
        public decimal Currency { get; set; }
        public BasinVolumeUnit VolumeUnit { get; set; }
        public decimal BloodRatePerGallon { get; set; } = BaseBloodRatePerGallon;
        public decimal ProcessedBlobRate { get; set; } = BaseProcessedBlobRate;
        public string[]? UnlockedWeapons { get; set; }
    }
}

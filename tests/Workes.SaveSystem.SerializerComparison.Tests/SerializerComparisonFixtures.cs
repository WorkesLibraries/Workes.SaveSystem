using Workes.SaveSystem;

namespace Workes.SaveSystem.SerializerComparison.Tests;

internal static class SerializerComparisonFixtures
{
    public const string SaveIdentity = "slot";
    public const string ProviderSaveKey = "comparison";

    public static IReadOnlyList<ComparisonScenario> CreateScenarios()
    {
        return new[]
        {
            new ComparisonScenario("small", CreateSmallState()),
            new ComparisonScenario("medium-typical", CreateMediumTypicalState()),
            new ComparisonScenario("large-repetitive", CreateLargeRepetitiveState()),
            new ComparisonScenario("large-varied", CreateLargeVariedState())
        };
    }

    public static IReadOnlyList<SerializerCase> CreateSerializerCases()
    {
        return new[]
        {
            new SerializerCase("pretty-json", "Pretty JSON", () => new JsonSaveSerializer()),
            new SerializerCase("compact-json", "Compact JSON", () => new JsonSaveSerializer(JsonSaveFormatting.Compact)),
            new SerializerCase("compressed-json", "Compressed compact JSON", () => new CompressedSaveSerializer(new JsonSaveSerializer(JsonSaveFormatting.Compact))),
            new SerializerCase("messagepack", "MessagePack", () => new MessagePackSaveSerializer()),
            new SerializerCase("compressed-messagepack", "Compressed MessagePack", () => new CompressedSaveSerializer(new MessagePackSaveSerializer()))
        };
    }

    public static SaveManager<string> CreateManager(string saveRoot, ISaveSerializer serializer)
    {
        return new SaveManager<string>(
            SaveSystemOptions.Create(
                saveRootPath: saveRoot,
                serializer: serializer));
    }

    public static ComparisonState CloneState(ComparisonState state)
    {
        return new ComparisonState
        {
            ProfileId = state.ProfileId,
            DisplayName = state.DisplayName,
            Level = state.Level,
            Experience = state.Experience,
            LastSavedUtc = state.LastSavedUtc,
            Settings = new ComparisonSettings
            {
                Difficulty = state.Settings.Difficulty,
                MusicVolume = state.Settings.MusicVolume,
                Fullscreen = state.Settings.Fullscreen
            },
            Inventory = state.Inventory
                .Select(item => new ComparisonItem
                {
                    Id = item.Id,
                    Category = item.Category,
                    Count = item.Count,
                    Quality = item.Quality,
                    Notes = item.Notes
                })
                .ToList(),
            Quests = state.Quests
                .Select(quest => new ComparisonQuest
                {
                    Id = quest.Id,
                    Title = quest.Title,
                    Completed = quest.Completed,
                    Progress = quest.Progress,
                    Flags = new Dictionary<string, int>(quest.Flags, StringComparer.Ordinal)
                })
                .ToList(),
            DiscoveredLocations = state.DiscoveredLocations
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => new ComparisonLocation
                    {
                        Name = kvp.Value.Name,
                        Visits = kvp.Value.Visits,
                        X = kvp.Value.X,
                        Y = kvp.Value.Y
                    },
                    StringComparer.Ordinal)
        };
    }

    public static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
    }

    private static ComparisonState CreateSmallState()
    {
        return new ComparisonState
        {
            ProfileId = "rook",
            DisplayName = "Rook",
            Level = 4,
            Experience = 1250,
            LastSavedUtc = new DateTime(2026, 6, 24, 12, 0, 0, DateTimeKind.Utc),
            Settings = new ComparisonSettings { Difficulty = "Normal", MusicVolume = 0.8f, Fullscreen = true },
            Inventory = new List<ComparisonItem>
            {
                new ComparisonItem { Id = "starter-sword", Category = "weapon", Count = 1, Quality = 0.55f, Notes = "starter" },
                new ComparisonItem { Id = "small-potion", Category = "consumable", Count = 3, Quality = 0.2f, Notes = "healing" }
            },
            Quests = new List<ComparisonQuest>
            {
                new ComparisonQuest { Id = "intro", Title = "Arrival", Completed = true, Progress = 1.0f, Flags = new Dictionary<string, int> { ["talked"] = 1 } }
            },
            DiscoveredLocations = new Dictionary<string, ComparisonLocation>
            {
                ["harbor"] = new ComparisonLocation { Name = "Harbor", Visits = 2, X = 12.5, Y = -4.0 }
            }
        };
    }

    private static ComparisonState CreateMediumTypicalState()
    {
        var state = CreateSmallState();
        state.ProfileId = "medium-profile";
        state.DisplayName = "Mira Vale";
        state.Level = 28;
        state.Experience = 184500;
        state.Inventory = Enumerable.Range(0, 45)
            .Select(i => new ComparisonItem
            {
                Id = "item-" + i.ToString("D3", System.Globalization.CultureInfo.InvariantCulture),
                Category = i % 5 == 0 ? "weapon" : i % 3 == 0 ? "material" : "consumable",
                Count = i % 7 + 1,
                Quality = (i % 11) / 10f,
                Notes = "slot-" + (i % 6).ToString(System.Globalization.CultureInfo.InvariantCulture)
            })
            .ToList();
        state.Quests = Enumerable.Range(0, 18)
            .Select(i => new ComparisonQuest
            {
                Id = "quest-" + i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture),
                Title = "Chapter " + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Completed = i < 11,
                Progress = i < 11 ? 1f : (i % 5) / 5f,
                Flags = new Dictionary<string, int>
                {
                    ["stage"] = i % 6,
                    ["choices"] = i * 3,
                    ["visited"] = i % 2
                }
            })
            .ToList();
        state.DiscoveredLocations = Enumerable.Range(0, 30)
            .ToDictionary(
                i => "location-" + i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture),
                i => new ComparisonLocation
                {
                    Name = "Location " + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Visits = i % 9,
                    X = i * 7.25,
                    Y = i * -3.5
                },
                StringComparer.Ordinal);
        return state;
    }

    private static ComparisonState CreateLargeRepetitiveState()
    {
        var state = CreateMediumTypicalState();
        state.ProfileId = "large-repetitive";
        state.Inventory = Enumerable.Range(0, 900)
            .Select(_ => new ComparisonItem
            {
                Id = "iron-ore",
                Category = "material",
                Count = 99,
                Quality = 0.25f,
                Notes = "stacked-resource"
            })
            .ToList();
        state.Quests = Enumerable.Range(0, 240)
            .Select(i => new ComparisonQuest
            {
                Id = "repeatable-task",
                Title = "Repeatable Task",
                Completed = i % 2 == 0,
                Progress = 0.5f,
                Flags = new Dictionary<string, int>
                {
                    ["stage"] = 2,
                    ["region"] = 4,
                    ["reward"] = 10
                }
            })
            .ToList();
        return state;
    }

    private static ComparisonState CreateLargeVariedState()
    {
        var random = new Random(1729);
        var state = CreateMediumTypicalState();
        state.ProfileId = "large-varied";
        state.Inventory = Enumerable.Range(0, 900)
            .Select(i => new ComparisonItem
            {
                Id = "item-" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + "-" + Token(random),
                Category = "category-" + random.Next(0, 19).ToString(System.Globalization.CultureInfo.InvariantCulture),
                Count = random.Next(1, 100),
                Quality = (float)Math.Round(random.NextDouble(), 3),
                Notes = Token(random) + "-" + Token(random)
            })
            .ToList();
        state.Quests = Enumerable.Range(0, 240)
            .Select(i => new ComparisonQuest
            {
                Id = "quest-" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + "-" + Token(random),
                Title = "Quest " + Token(random),
                Completed = random.Next(0, 2) == 0,
                Progress = (float)Math.Round(random.NextDouble(), 3),
                Flags = new Dictionary<string, int>
                {
                    ["stage-" + Token(random)] = random.Next(0, 20),
                    ["choice-" + Token(random)] = random.Next(0, 1000),
                    ["seed-" + Token(random)] = random.Next(0, 1000)
                }
            })
            .ToList();
        state.DiscoveredLocations = Enumerable.Range(0, 240)
            .ToDictionary(
                i => "location-" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + "-" + Token(random),
                _ => new ComparisonLocation
                {
                    Name = "Place " + Token(random),
                    Visits = random.Next(0, 40),
                    X = Math.Round(random.NextDouble() * 2000 - 1000, 3),
                    Y = Math.Round(random.NextDouble() * 2000 - 1000, 3)
                },
                StringComparer.Ordinal);
        return state;
    }

    private static string Token(Random random)
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
        var chars = new char[8];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[random.Next(alphabet.Length)];
        return new string(chars);
    }
}

internal sealed record ComparisonScenario(string Id, ComparisonState State);

internal sealed record SerializerCase(string Id, string DisplayName, Func<ISaveSerializer> CreateSerializer);

internal sealed class ComparisonProvider : ISaveProvider<ComparisonState>
{
    public ComparisonProvider(ComparisonState current)
    {
        Current = current;
    }

    public string SaveKey => SerializerComparisonFixtures.ProviderSaveKey;
    public int SchemaVersion => 1;
    public int LoadPriority => 0;
    public ComparisonState Current { get; set; }
    public ComparisonState CaptureState() => Current;
    public void RestoreState(ComparisonState state) => Current = state;
}

public sealed class ComparisonState
{
    public string ProfileId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Level { get; set; }
    public long Experience { get; set; }
    public DateTime LastSavedUtc { get; set; }
    public ComparisonSettings Settings { get; set; } = new ComparisonSettings();
    public List<ComparisonItem> Inventory { get; set; } = new List<ComparisonItem>();
    public List<ComparisonQuest> Quests { get; set; } = new List<ComparisonQuest>();
    public Dictionary<string, ComparisonLocation> DiscoveredLocations { get; set; } = new Dictionary<string, ComparisonLocation>(StringComparer.Ordinal);
}

public sealed class ComparisonSettings
{
    public string Difficulty { get; set; } = string.Empty;
    public float MusicVolume { get; set; }
    public bool Fullscreen { get; set; }
}

public sealed class ComparisonItem
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
    public float Quality { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class ComparisonQuest
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool Completed { get; set; }
    public float Progress { get; set; }
    public Dictionary<string, int> Flags { get; set; } = new Dictionary<string, int>(StringComparer.Ordinal);
}

public sealed class ComparisonLocation
{
    public string Name { get; set; } = string.Empty;
    public int Visits { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}

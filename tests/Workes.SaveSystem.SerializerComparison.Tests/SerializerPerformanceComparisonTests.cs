using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Workes.SaveSystem.SerializerComparison.Tests;

public sealed class SerializerPerformanceComparisonTests
{
    private const int WarmupRuns = 1;
    private const int MeasuredRuns = 5;

    [Test]
    public void GenerateSerializerPerformanceComparisonExamples()
    {
        var outputRoot = Path.Combine(SerializerComparisonFixtures.GetRepositoryRoot(), "tests", "obj", "SerializerPerformanceComparison");
        if (Directory.Exists(outputRoot))
            Directory.Delete(outputRoot, recursive: true);
        Directory.CreateDirectory(outputRoot);

        var scenarios = SerializerComparisonFixtures.CreateScenarios();
        var serializerCases = SerializerComparisonFixtures.CreateSerializerCases();

        var results = new List<PerformanceResult>();
        foreach (var scenario in scenarios)
        {
            foreach (var serializerCase in serializerCases)
            {
                for (var i = 0; i < WarmupRuns; i++)
                    MeasureOnce(scenario, serializerCase, Path.Combine(outputRoot, "_warmup", scenario.Id, serializerCase.Id, i.ToString(CultureInfo.InvariantCulture)));

                var runResults = new List<OperationTimings>();
                for (var i = 0; i < MeasuredRuns; i++)
                    runResults.Add(MeasureOnce(scenario, serializerCase, Path.Combine(outputRoot, scenario.Id, serializerCase.Id, "run-" + i.ToString(CultureInfo.InvariantCulture))));

                results.AddRange(CreateResults(scenario, serializerCase, runResults));
            }
        }

        WriteSummary(outputRoot, scenarios, serializerCases, results);

        Assert.That(File.Exists(Path.Combine(outputRoot, "README.md")), Is.True);
        Assert.That(File.Exists(Path.Combine(outputRoot, "summary.csv")), Is.True);
        foreach (var scenario in scenarios)
        {
            foreach (var serializerCase in serializerCases)
            {
                var rows = results
                    .Where(result => result.ScenarioId == scenario.Id && result.SerializerId == serializerCase.Id)
                    .ToArray();

                Assert.That(rows.Select(result => result.Operation), Is.EquivalentTo(new[] { "ValidateRegistrations", "SaveToDisk", "ValidateSave", "LoadFromDisk" }));
                Assert.That(rows.All(result => result.Runs == MeasuredRuns), Is.True);
                Assert.That(rows.All(result => result.MedianMilliseconds >= 0), Is.True);
                Assert.That(rows.Any(result => result.MedianMilliseconds > 0), Is.True);
            }
        }
    }

    private static OperationTimings MeasureOnce(ComparisonScenario scenario, SerializerCase serializerCase, string saveRoot)
    {
        var serializer = serializerCase.CreateSerializer();
        var provider = new ComparisonProvider(SerializerComparisonFixtures.CloneState(scenario.State));
        var manager = SerializerComparisonFixtures.CreateManager(saveRoot, serializer);

        manager.RegisterProvider(provider);

        var validateRegistrations = Measure(() => manager.ValidateRegistrations());
        var saveToDisk = Measure(() => manager.SaveToDisk(SerializerComparisonFixtures.SaveIdentity));
        var validateSave = Measure(() =>
        {
            var validation = manager.ValidateSave(SerializerComparisonFixtures.SaveIdentity);
            Assert.That(validation.IsValid, Is.True);
        });

        provider.Current = new ComparisonState { ProfileId = "changed" };
        var loadFromDisk = Measure(() =>
        {
            var loaded = manager.LoadFromDisk(SerializerComparisonFixtures.SaveIdentity);
            Assert.That(loaded, Is.True);
        });

        Assert.That(provider.Current.ProfileId, Is.EqualTo(scenario.State.ProfileId));

        return new OperationTimings(validateRegistrations, saveToDisk, validateSave, loadFromDisk);
    }

    private static double Measure(Action action)
    {
        var stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static IEnumerable<PerformanceResult> CreateResults(
        ComparisonScenario scenario,
        SerializerCase serializerCase,
        IReadOnlyList<OperationTimings> runResults)
    {
        yield return CreateResult(scenario, serializerCase, "ValidateRegistrations", runResults.Select(result => result.ValidateRegistrationsMilliseconds));
        yield return CreateResult(scenario, serializerCase, "SaveToDisk", runResults.Select(result => result.SaveToDiskMilliseconds));
        yield return CreateResult(scenario, serializerCase, "ValidateSave", runResults.Select(result => result.ValidateSaveMilliseconds));
        yield return CreateResult(scenario, serializerCase, "LoadFromDisk", runResults.Select(result => result.LoadFromDiskMilliseconds));
    }

    private static PerformanceResult CreateResult(
        ComparisonScenario scenario,
        SerializerCase serializerCase,
        string operation,
        IEnumerable<double> measurements)
    {
        var values = measurements.OrderBy(value => value).ToArray();
        return new PerformanceResult(
            scenario.Id,
            serializerCase.Id,
            serializerCase.DisplayName,
            operation,
            Median(values),
            values.Length);
    }

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
            return 0;

        var middle = values.Count / 2;
        if (values.Count % 2 == 1)
            return values[middle];

        return (values[middle - 1] + values[middle]) / 2;
    }

    private static void WriteSummary(
        string outputRoot,
        IReadOnlyList<ComparisonScenario> scenarios,
        IReadOnlyList<SerializerCase> serializerCases,
        IReadOnlyList<PerformanceResult> results)
    {
        var readme = new StringBuilder();
        readme.AppendLine("# Serializer Performance Comparison");
        readme.AppendLine();
        readme.AppendLine("Generated by `Workes.SaveSystem.SerializerComparison.Tests`. Results are diagnostic and machine-dependent, not benchmark-grade. Each value is the median of five measured runs after one warmup run.");
        readme.AppendLine();

        var csv = new StringBuilder();
        csv.AppendLine("scenario,serializer,operation,median_ms,runs");

        foreach (var scenario in scenarios)
        {
            readme.AppendLine("## " + scenario.Id);
            readme.AppendLine();
            readme.AppendLine("| Serializer | ValidateRegistrations ms | SaveToDisk ms | ValidateSave ms | LoadFromDisk ms |");
            readme.AppendLine("|---|---:|---:|---:|---:|");

            foreach (var serializerCase in serializerCases)
            {
                var rows = results
                    .Where(result => result.ScenarioId == scenario.Id && result.SerializerId == serializerCase.Id)
                    .ToDictionary(result => result.Operation, StringComparer.Ordinal);

                readme.AppendLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "| {0} | {1:0.###} | {2:0.###} | {3:0.###} | {4:0.###} |",
                        serializerCase.DisplayName,
                        rows["ValidateRegistrations"].MedianMilliseconds,
                        rows["SaveToDisk"].MedianMilliseconds,
                        rows["ValidateSave"].MedianMilliseconds,
                        rows["LoadFromDisk"].MedianMilliseconds));
            }

            readme.AppendLine();
        }

        foreach (var result in results)
        {
            csv.AppendLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3:0.###},{4}",
                    result.ScenarioId,
                    result.SerializerId,
                    result.Operation,
                    result.MedianMilliseconds,
                    result.Runs));
        }

        File.WriteAllText(Path.Combine(outputRoot, "README.md"), readme.ToString());
        File.WriteAllText(Path.Combine(outputRoot, "summary.csv"), csv.ToString());
    }

    private sealed record OperationTimings(
        double ValidateRegistrationsMilliseconds,
        double SaveToDiskMilliseconds,
        double ValidateSaveMilliseconds,
        double LoadFromDiskMilliseconds);

    private sealed record PerformanceResult(
        string ScenarioId,
        string SerializerId,
        string SerializerDisplayName,
        string Operation,
        double MedianMilliseconds,
        int Runs);
}

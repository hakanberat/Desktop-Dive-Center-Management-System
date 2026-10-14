using System.IO;

namespace DiveCenterManager.Helpers;

public sealed record AppOptions(bool SelfTest, bool SampleData, bool VerifyPersistence, string? Directory)
{
    public bool Phase2Test { get; init; }
    public bool Phase3Test { get; init; }
    public bool Phase3Persistence { get; init; }
    public bool Phase6Test { get; init; }
    public bool Phase6Persistence { get; init; }
    public bool Phase7Test { get; init; }
    public bool Phase7Persistence { get; init; }
    public bool Phase8Test { get; init; }
    public bool Phase8Persistence { get; init; }
    public bool StartupSmoke { get; init; }
    public bool IsDiagnostic => SelfTest || VerifyPersistence || Phase2Test || Phase3Test || Phase3Persistence || Phase4Test || Phase4Persistence || Phase5Test || Phase5Persistence || Phase6Test || Phase6Persistence || Phase7Test || Phase7Persistence || Phase8Test || Phase8Persistence || StartupSmoke;
    public bool Phase5Test { get; init; }
    public bool Phase5Persistence { get; init; }
    public bool Phase4Test { get; init; }
    public bool Phase4Persistence { get; init; }
    public static AppOptions Parse(string[] args)
    {
        string? directory = null;
        var known = new HashSet<string> { "--sample-data", "--self-test", "--verify-persistence", "--verify-phase2", "--verify-startup" };
        for (var phase = 3; phase <= 8; phase++) { known.Add($"--verify-phase{phase}"); known.Add($"--verify-phase{phase}-persistence"); }
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--data-dir")
            {
                if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i]) || args[i].StartsWith("--", StringComparison.Ordinal))
                    throw new System.ComponentModel.DataAnnotations.ValidationException("--data-dir requires a database folder.");
                directory = AppPaths.CanonicalPath(args[i]);
            }
            else if (!known.Contains(args[i])) throw new System.ComponentModel.DataAnnotations.ValidationException("Unknown application option: " + args[i]);
        }
        var test = args.Contains("--self-test"); var verify = args.Contains("--verify-persistence"); var sample = args.Contains("--sample-data");
        var phase2 = args.Contains("--verify-phase2");
        var phase3 = args.Contains("--verify-phase3");
        var phase3Persistence = args.Contains("--verify-phase3-persistence");
        var phase6 = args.Contains("--verify-phase6");
        var phase6Persistence = args.Contains("--verify-phase6-persistence");
        var phase7 = args.Contains("--verify-phase7");
        var phase7Persistence = args.Contains("--verify-phase7-persistence");
        var phase8 = args.Contains("--verify-phase8");
        var phase8Persistence = args.Contains("--verify-phase8-persistence");
        var startupSmoke = args.Contains("--verify-startup");
        var phase5 = args.Contains("--verify-phase5");
        var phase5Persistence = args.Contains("--verify-phase5-persistence");
        var phase4 = args.Contains("--verify-phase4");
        var phase4Persistence = args.Contains("--verify-phase4-persistence");
        var diagnosticCount = new[] { test, verify, phase2, phase3, phase3Persistence, phase4, phase4Persistence, phase5, phase5Persistence, phase6, phase6Persistence, phase7, phase7Persistence, phase8, phase8Persistence, startupSmoke }.Count(x => x);
        if (diagnosticCount > 1 || diagnosticCount > 0 && sample) throw new System.ComponentModel.DataAnnotations.ValidationException("Choose one verification mode at a time; sample data is a separate startup mode.");
        if (diagnosticCount > 0 && (directory == null || string.Equals(directory, AppPaths.CanonicalPath(AppPaths.DefaultDataDirectory), StringComparison.OrdinalIgnoreCase)))
            throw new System.ComponentModel.DataAnnotations.ValidationException("Automated checks require an explicit isolated --data-dir outside the live workspace.");
        if (sample && directory == null) directory = Path.Combine(AppPaths.DefaultDataDirectory, "Sample");
        if (sample && string.Equals(AppPaths.CanonicalPath(directory!), AppPaths.CanonicalPath(AppPaths.DefaultDataDirectory), StringComparison.OrdinalIgnoreCase))
            throw new System.ComponentModel.DataAnnotations.ValidationException("Sample data must use a separate data folder, not the live workspace.");
        return new AppOptions(test, sample, verify, directory) { Phase2Test = phase2, Phase3Test = phase3, Phase3Persistence = phase3Persistence, Phase4Test = phase4, Phase4Persistence = phase4Persistence, Phase5Test = phase5, Phase5Persistence = phase5Persistence, Phase6Test = phase6, Phase6Persistence = phase6Persistence, Phase7Test = phase7, Phase7Persistence = phase7Persistence, Phase8Test = phase8, Phase8Persistence = phase8Persistence, StartupSmoke = startupSmoke };
    }
}

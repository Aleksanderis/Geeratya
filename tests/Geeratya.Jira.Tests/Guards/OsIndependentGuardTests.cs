using System.Text.RegularExpressions;

namespace Geeratya.Jira.Tests.Guards;

/// <summary>
/// Fails when a test class marked <c>[Trait("Os", "Independent")]</c> uses an API whose behaviour differs per OS
/// (the Windows CI leg runs only unmarked tests, so a marked class must be pure).
/// </summary>
public class OsIndependentGuardTests
{
    private const string FileSystemPattern =
        @"\b(File|Directory|FileInfo|DirectoryInfo|FileStream|StreamReader|StreamWriter)\.|" +
        @"\bnew\s+(FileStream|StreamReader|StreamWriter|FileInfo|DirectoryInfo)\b";

    private const string EnvironmentPattern =
        @"Environment\.(GetEnvironmentVariable|SetEnvironmentVariable|GetFolderPath|OSVersion|UserName|" +
        @"MachineName|NewLine|ProcessPath|CurrentDirectory)";

    private static readonly Regex _markerPattern = Pattern(@"\[\s*Trait\(\s*""Os""\s*,\s*""Independent""\s*\)\s*\]");

    private static readonly Dictionary<string, Regex> _forbiddenApis = new()
    {
        ["file system"] = Pattern(FileSystemPattern),
        ["temp path"] = Pattern(@"GetTempPath|GetTempFileName"),
        ["path"] = Pattern(@"\bPath\.|AppContext\.BaseDirectory"),
        ["environment"] = Pattern(EnvironmentPattern),
        ["process"] = Pattern(@"\bProcess\.|ProcessStartInfo|\bnew\s+Process\b"),
        ["sockets"] = Pattern(@"\bSocket\b|TcpListener|TcpClient|UdpClient|NetworkStream|HttpListener|SocketsHttpHandler"),
        ["terminal"] = Pattern(@"\bConsole\.|\\u001b|\\x1b|\\e\["),
        ["line ending"] = Pattern(@"\\r\\n"),
        ["OS check"] = Pattern(@"OperatingSystem\.Is|RuntimeInformation\.IsOSPlatform"),
    };

    [Fact]
    public void GivenTestSources_WhenScanningMarkedClasses_ThenNoneUsesAnOsDependentApi()
    {
        var violations = new List<string>();

        foreach (var file in TestSourceFiles())
        {
            violations.AddRange(FindViolations(file, File.ReadAllText(file)));
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    [Fact]
    public void GivenMarkedClassUsingTempPath_WhenScanning_ThenItIsReported()
    {
        const string source = """
            [Trait("Os", "Independent")]
            public class Wrong { void A() => Path.GetTempPath(); }
            """;

        var violations = FindViolations("Wrong.cs", source);

        Assert.NotEmpty(violations);
    }

    [Fact]
    public void GivenUnmarkedClassUsingTempPath_WhenScanning_ThenItIsIgnored()
    {
        const string source = "public class Fine { void A() => Path.GetTempPath(); }";

        Assert.Empty(FindViolations("Fine.cs", source));
    }

    [Fact]
    public void GivenRepository_WhenScanning_ThenMarkedClassesAreFound()
    {
        var marked = TestSourceFiles().Count(file => _markerPattern.IsMatch(File.ReadAllText(file)));

        Assert.True(marked > 0, "The guard found no Os=Independent class; the scan is broken.");
    }

    private static Regex Pattern(string pattern) => new(pattern, RegexOptions.Compiled);

    private static List<string> FindViolations(string file, string source)
    {
        var violations = new List<string>();

        if (!_markerPattern.IsMatch(source))
        {
            return violations;
        }

        var lines = source.Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            foreach (var (kind, pattern) in _forbiddenApis)
            {
                if (pattern.IsMatch(lines[index]))
                {
                    violations.Add($"{file}:{index + 1}: Os=Independent class uses a {kind} API: {lines[index].Trim()}");
                }
            }
        }

        return violations;
    }

    private static IEnumerable<string> TestSourceFiles()
    {
        var guardFile = $"{nameof(OsIndependentGuardTests)}.cs";

        return Directory.EnumerateFiles(FindTestsDirectory(), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file) && Path.GetFileName(file) != guardFile);
    }

    private static bool IsBuildOutput(string file)
    {
        var segments = file.Split(Path.DirectorySeparatorChar);

        return segments.Contains("bin") || segments.Contains("obj");
    }

    private static string FindTestsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tests");

            if (File.Exists(Path.Combine(candidate, "Directory.Build.props")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The tests directory was not found above the test output.");
    }
}

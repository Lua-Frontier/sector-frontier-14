#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Content.IntegrationTests.Tests._Lua;

[TestFixture]
public sealed class SPDXHunter
{
    private static readonly string[] ExcludedDirectories =
    {
        "obj",
        "bin",
        ".git",
        ".vs",
        "node_modules",
        "RobustToolbox"
    };

    private static readonly string[] ExcludedFiles =
    {
        "AssemblyInfo.cs",
        "GlobalUsings.cs",
        "Designer.cs",
        ".Designer.cs",
        "SPDXHunter.cs"
    };

    [Test]
    public void NoSpdxInCode()
    {
        var solutionRoot = FindSolutionRoot();
        Assert.That(solutionRoot, Is.Not.Null, "Не удалось найти корневую директорию решения");
        var csFiles = Directory.GetFiles(solutionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !ExcludedDirectories.Any(dir => file.Contains(Path.DirectorySeparatorChar + dir + Path.DirectorySeparatorChar) || file.Contains(Path.AltDirectorySeparatorChar + dir + Path.AltDirectorySeparatorChar)))
            .Where(file => !ExcludedFiles.Any(excluded => file.EndsWith(excluded)))
            .ToList();
        Assert.That(csFiles, Is.Not.Empty, "Не найдено C# файлов для проверки");
        var matches = new List<string>();
        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(solutionRoot, file);
            if (content.Contains("SPDX-", StringComparison.Ordinal)) matches.Add(relativePath);
        }
        if (matches.Count > 0)
        { Assert.Fail($"SPDX теги не разрешены в коде, но были найдены в {matches.Count} файле(ах):\n" + $"{string.Join("\n", matches.Take(200))}" + (matches.Count > 200 ? $"\n... и ещё {matches.Count - 200}" : "")); }
    }

    private static string? FindSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);
        while (dir != null)
        {
            if (dir.GetFiles("*.sln").Any() || dir.GetFiles("Content.Shared.csproj", SearchOption.TopDirectoryOnly).Any())
            { return dir.FullName; }
            dir = dir.Parent;
        }
        return null;
    }
}


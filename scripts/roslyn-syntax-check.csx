using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

if (Args.Count != 1)
{
    Console.Error.WriteLine("Usage: csi roslyn-syntax-check.csx <repository-root>");
    Environment.Exit(2);
}

var root = Path.GetFullPath(Args[0]);
var files = Directory
    .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
    .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
    .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
    .ToArray();
var errorCount = 0;

foreach (var file in files)
{
    var tree = CSharpSyntaxTree.ParseText(
        File.ReadAllText(file),
        new CSharpParseOptions(LanguageVersion.Preview),
        file);
    foreach (var diagnostic in tree.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error))
    {
        Console.Error.WriteLine(diagnostic);
        errorCount++;
    }
}

if (errorCount > 0)
{
    Console.Error.WriteLine($"C# syntax check failed with {errorCount} error(s).");
    Environment.Exit(1);
}

Console.WriteLine($"Roslyn syntax check passed for {files.Length} C# files.");

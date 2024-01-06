using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Scripting;
using StandaloneBannedApiAnalyzers;

public class Csx
{
    public static Script CreateScript(string code)
    {
        static IEnumerable<string> GetSystemAssemblyPaths()
        {
            var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location)
                               ?? throw new InvalidOperationException("Could not find the assembly for object.");
            yield return typeof(object).Assembly.Location;
            yield return Path.Combine(assemblyPath, "mscorlib.dll");
            yield return Path.Combine(assemblyPath, "System.dll");
            yield return Path.Combine(assemblyPath, "System.Core.dll");
            yield return Path.Combine(assemblyPath, "System.Console.dll");
            yield return Path.Combine(assemblyPath, "System.Runtime.dll");
            yield return Path.Combine(assemblyPath, "System.Private.CoreLib.dll");
            yield return Path.Combine(assemblyPath, "System.Runtime.Extensions.dll");
            yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "N.dll");
        }
        
        var references = GetSystemAssemblyPaths()
            .Select(path => MetadataReference.CreateFromFile(path)).ToList();
        
        var options = ScriptOptions.Default
            .WithImports("System", "System.IO", "System.Text")
            .WithEmitDebugInformation(true)
            .WithReferences(references)
            .WithAllowUnsafe(false);
        
        return CSharpScript.Create(code, globalsType: typeof(object), options: options);
    }

    public static async Task<ImmutableArray<Diagnostic>> CompileCodeAsync(Script script, BannedSymbolsAdditionalText bannedSymbols)
    {
        var compilation = script.GetCompilation();
        
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new StandaloneCSharpSymbolIsBannedAnalyzer()
        );

        var compilationWithAnalyzers = new CompilationWithAnalyzers(
            compilation,
            analyzers,
            new AnalyzerOptions([(AdditionalText)bannedSymbols]));

        return await compilationWithAnalyzers.GetAllDiagnosticsAsync();
    }
    public static async Task<ImmutableArray<Diagnostic>> CompileCodeAsync(string script, BannedSymbolsAdditionalText bannedSymbols) {
        return await CompileCodeAsync(CreateScript(script), bannedSymbols);
    }
}
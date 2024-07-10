using System.Text;
using Dotnet.Script.Core;
using Dotnet.Script.DependencyModel;
using Dotnet.Script.DependencyModel.Context;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace OpenTelemetry.Template.OtelEnabler.Helpers;

public static class ScriptExecutioner
{
    public static T ExecuteCsxFromPath<T>(string path, params string[] args)
    {   
        using var scriptStream = File.OpenRead(path);
        var compiler = new ScriptCompiler(LogFactory, true);
        var runner = new ScriptRunner(compiler, LogFactory, ScriptConsole.Default);
        var sourceText = SourceText.From(scriptStream);
        var context = new ScriptContext(sourceText, Directory.GetCurrentDirectory(), args, null, OptimizationLevel.Release, ScriptMode.Eval);

        var result = runner.Execute<T>(context).GetAwaiter().GetResult();
        return result;

        Dotnet.Script.DependencyModel.Logging.Logger LogFactory(Type type)
        {
            return (level, message, exception) =>
            {
                Console.WriteLine($"{level} {message} {exception}");
            };
        }
    }

    public static T ExecuteCsxFromString<T>(string script, params string[] args)
    {   
        var compiler = new ScriptCompiler(LogFactory, true);
        var runner = new ScriptRunner(compiler, LogFactory, ScriptConsole.Default);
        var sourceText = SourceText.From(script, Encoding.UTF8);
        var context = new ScriptContext(sourceText, Directory.GetCurrentDirectory(), args, null, OptimizationLevel.Release, ScriptMode.Eval);

        var result = runner.Execute<T>(context).GetAwaiter().GetResult();
        return result;

        Dotnet.Script.DependencyModel.Logging.Logger LogFactory(Type type)
        {
            return (level, message, exception) =>
            {
                Console.WriteLine($"{level} {message} {exception}");
            };
        }
    }
}
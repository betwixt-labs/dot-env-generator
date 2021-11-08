using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DotEnvGenerator;

[Generator]
public class DotEnvSourceGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        var envFile = context.AdditionalFiles.FirstOrDefault();
        if (envFile is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(NoDotEnv, Location.None, context.Compilation.AssemblyName));
            return;
        }

        var entries = ParseEnvFile(context, envFile);

        var className = $"{Path.GetFileNameWithoutExtension(envFile.Path).ToPascalCase()}Environment";

        var builder = new StringBuilder();

        builder.AppendLine("using System;");
        builder.AppendLine("namespace DotEnv.Generated");
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>");
        builder.AppendLine($"   /// An auto-generated class which holds constants derived from '{Path.GetFileName(envFile.Path)}'");
        builder.AppendLine("    /// </summary>");
        builder.AppendLine($"    public static class {className}");
        builder.AppendLine("    {");
        foreach (var entry in entries)
        {
            var value = FormatValue(entry);
            if (string.IsNullOrWhiteSpace(value))
            {
                context.ReportDiagnostic(Diagnostic.Create(EnvironmentVariableNotFound, Location.None, entry.Name));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.Documentation))
            {
                builder.Append("       /// <summary> ");
                builder.Append(entry.Documentation);
                builder.Append(" </summary>").AppendLine();
            }

            if (!entry.Type.IsArray)
            {
                if (entry.Type.CanBeConsts())
                {
                    builder.AppendLine($"       public const {entry.Type} {entry.Name.ToPascalCase()} = {value};");
                }
                else
                {
                    builder.AppendLine($"       public static readonly {entry.Type} {entry.Name.ToPascalCase()} = {value};");
                }
            }
            else
            {
                // https://vcsjones.dev/csharp-readonly-span-bytes-static/
                builder.AppendLine(entry.Type == typeof(byte[])
                    ? $"       public static ReadOnlySpan<byte> {entry.Name.ToPascalCase()} => {value};"
                    : $"       public static readonly IReadOnlyList<{entry.Type.GetElementType()}> {entry.Name.ToPascalCase()} = {value};");
            }
        }
        builder.AppendLine("    }");
        builder.AppendLine("}");

        context.AddSource("dotenv", SourceText.From(builder.ToString(), Encoding.UTF8));
    }


    public void Initialize(GeneratorInitializationContext context)
    {
    }

    #region Formatters 

    private static string FormatValue(EnvEntry entry) => entry switch
    {
        _ when entry.Type.IsNumericType() => ValueToNumber(entry),
        _ when entry.Type.IsArray => ValueToArray(entry),
        _ when entry.Type == typeof(string) => ValueToLiteral(entry),
        _ when entry.Type == typeof(Guid) => ValueToGuid(entry),
        _ when entry.Type == typeof(DateTime) => ValueToDateTime(entry),
        _ => throw new ArgumentOutOfRangeException(entry.Type.ToString())
    };

    private static string ValueToDateTime(EnvEntry entry)
    {
        var value = entry.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return "DateTime.MinValue";
        }
        return $"DateTime.Parse(\"{value}\")";
    }

    private static string ValueToGuid(EnvEntry entry)
    {
        var value = entry.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Guid.Empty";
        }
        return $"Guid.Parse(\"{value}\")";
    }

    /// <summary>
    ///     Converts an environment variable to a ImmutableArray.
    /// </summary>
    private static string ValueToArray(EnvEntry entry)
    {
        var value = entry.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            return $"Array.Empty<{entry.Type.GetElementType()}>()";
        }

        if (entry.Type == typeof(byte[]) && value.IsBase64String())
        {
            return $"new byte[] {{ {Convert.FromBase64String(value).ToHex()} }}";
        }
        if (entry.Type == typeof(string[]))
        {
            return $"new string[] {{ {string.Join(", ", value.CommaSplit().Select(x => x.QuoteString()))} }}";
        }

        return $"new {entry.Type.GetElementType()}[] {{ {string.Join(", ", value.Split(',').Select(x => x.Trim()))} }}";
    }


    /// <summary>
    ///     Converts an environment variable to a string literal.
    /// </summary>
    private static string ValueToNumber(EnvEntry entry)
    {
        var value = entry.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            value = "default";
        }
        return value;
    }



    /// <summary>
    ///     Converts an environment variable to a string literal.
    /// </summary>
    private static string ValueToLiteral(EnvEntry entry) => entry.Value.QuoteString();

    #endregion

    #region Parser
    private static List<EnvEntry> ParseEnvFile(GeneratorExecutionContext context, AdditionalText envFile)
    {
        var entries = new List<EnvEntry>();
        var text = envFile.GetText();
        if (text is null)
        {
            return entries;
        }
        string? documentation = null;
        foreach (var textLine in text.Lines)
        {
            var line = textLine.ToString();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            line = line.Trim();
            if (line[0] is '#')
            {
                if (line.Length > 1)
                {
                    documentation = line.Substring(1);
                }
                continue;
            }
            try
            {
                if (!line.Contains('='))
                {
                    continue;
                }

                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length < 2)
                {
                    context.ReportDiagnostic(Diagnostic.Create(SyntaxError, Location.None, envFile.Path));
                    continue;
                }

                var name = parts[0].Trim();
                if (entries.Any(e => e.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DuplicateEnvironmentVariable, Location.None, name, envFile.Path));
                    continue;
                }
                var defaultValue = parts[1].Trim();

                var value = GetEnvironmentVariable(name, defaultValue);

                if (string.IsNullOrWhiteSpace(value))
                {
                    context.ReportDiagnostic(Diagnostic.Create(EnvironmentVariableNotFound, Location.None, name));
                    continue;
                }
                entries.Add(new EnvEntry(name, value, documentation?.Trim()));

            }
            finally
            {
                documentation = null;
            }
        }
        return entries;
    }

    /// <summary>
    ///     Searches for an environment variable checking all available stores.
    /// </summary>
    private static string GetEnvironmentVariable(string name, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
        if (string.IsNullOrWhiteSpace(value))
        {
            value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
        }
        if (string.IsNullOrWhiteSpace(value))
        {
            value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }
        if (string.IsNullOrWhiteSpace(value))
        {
            value = defaultValue;
        }
        return value;
    }

    #endregion
    #region Errors

    private static readonly DiagnosticDescriptor NoDotEnv = new("DOTENVGEN001",
        "No .env file found",
        "Unable to find .env file in project '{0}'.",
        "DotEnvGenerator",
        DiagnosticSeverity.Warning,
        true);


    private static readonly DiagnosticDescriptor DefaultEnvironmentVariable = new("DOTENVGEN004",
        "Default environment variable was used",
        "No environment variable could be found for '{0}' so its default value was used.",
        "DotEnvGenerator",
        DiagnosticSeverity.Warning,
        true);

    private static readonly DiagnosticDescriptor EnvironmentVariableNotFound = new("DOTENVGEN005",
        "Environment variable not found",
        "No environment variable or default value could be found for '{0}'.",
        "DotEnvGenerator",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor DuplicateEnvironmentVariable = new("DOTENVGEN006",
        "Duplicate environment variable",
        "The environment variable '{0}' is defined twice in '{1}'.",
        "DotEnvGenerator",
        DiagnosticSeverity.Error,
        true);

    private static readonly DiagnosticDescriptor SyntaxError = new("DOTENVGEN003",
        "Not enough pipes.",
        "Invalid .env entry: '{0}'.",
        "DotEnvGenerator",
        DiagnosticSeverity.Error,
        true);

    #endregion
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json.Linq;
using RevitPlugin.Attributes;
using RevitPlugin.Core;

namespace RevitPlugin.Commands
{
    [McpTool("ExecuteCSharp", @"Generates and executes dynamic C# code using the Revit API.
Available variables: 'app' (UIApplication) and 'doc' (Document).
Default using directives: System, System.Linq, System.Collections.Generic, Autodesk.Revit.DB, Autodesk.Revit.UI.
Transactions: NOT started automatically. You MUST wrap any modifying code inside a Transaction block.
Return Values: Return primitive types, strings, or anonymous objects. DO NOT return complex Revit API Elements or it will fail to serialize json.", requiresTransaction: false)]
    public class ExecuteCSharpCmd : IRevitCommand
    {
        public class Args
        {
            [McpArg(@"The C# code snippet to execute inside the Run method.
Rules:
1. Access `doc` and `app` directly.
2. If modifying data, wrap in: using (var t = new Transaction(doc, ""ExecuteCSharp"")) { t.Start(); ... t.Commit(); }
3. Return simple types or anonymous objects (e.g. `return new { Name = e.Name };`), DO NOT return `Element`s.", required: true)]
            public string Code { get; set; } = string.Empty;
        }

        private static List<MetadataReference>? _cachedReferences;
        private static readonly object _referenceLock = new object();

        public McpResponse Execute(UIApplication app, JObject jsonArgs)
        {
            var args = jsonArgs.ToObject<Args>();
            if (string.IsNullOrWhiteSpace(args?.Code))
            {
                return new McpResponse { Success = false, Message = "Code argument cannot be empty." };
            }

            string sourceCode = $@"
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.ApplicationServices;

namespace DynamicApp
{{
    public class Executor
    {{
        public static object? Run(UIApplication app, Document doc)
        {{
            {args.Code}
#pragma warning disable CS0162 // Unreachable code detected
            return null;
#pragma warning restore CS0162
        }}
    }}
}}";

            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

            if (_cachedReferences == null)
            {
                lock (_referenceLock)
                {
                    if (_cachedReferences == null)
                    {
                        var refs = new List<MetadataReference>();
                        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            if (!assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
                            {
                                try
                                {
                                    refs.Add(MetadataReference.CreateFromFile(assembly.Location));
                                }
                                catch
                                {
                                    // Ignore loading issues for specific assemblies
                                }
                            }
                        }
                        _cachedReferences = refs;
                    }
                }
            }
            var references = _cachedReferences;

            var compilation = CSharpCompilation.Create(
                $"DynamicRevitScript_{Guid.NewGuid()}",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);

            if (!result.Success)
            {
                var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                                               .Select(d => d.GetMessage());
                return new McpResponse { Success = false, Message = "Compilation failed:\n" + string.Join("\n", errors) };
            }

            ms.Seek(0, SeekOrigin.Begin);

            var loadContext = new AssemblyLoadContext($"DynamicContext_{Guid.NewGuid()}", isCollectible: true);
            try
            {
                var assembly = loadContext.LoadFromStream(ms);
                var type = assembly.GetType("DynamicApp.Executor");
                if (type == null)
                    return new McpResponse { Success = false, Message = "Failed to find Executor class in dynamically generated assembly." };

                var method = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                    return new McpResponse { Success = false, Message = "Failed to find Run method in dynamically generated assembly." };

                var returnValue = method.Invoke(null, new object[] { app, app.ActiveUIDocument.Document });

                return new McpResponse
                {
                    Success = true,
                    Message = "Execution completed successfully.",
                    Data = SerializeResult(returnValue)
                };
            }
            catch (TargetInvocationException ex)
            {
                return new McpResponse
                {
                    Success = false,
                    Message = $"Runtime Error: {ex.InnerException?.Message ?? ex.Message}\nStackTrace: {ex.InnerException?.StackTrace}"
                };
            }
            catch (Exception ex)
            {
                return new McpResponse
                {
                    Success = false,
                    Message = $"Execution Error: {ex.Message}\nStackTrace: {ex.StackTrace}"
                };
            }
            finally
            {
                // Unload dynamically loaded assembly context
                loadContext.Unload();
            }
        }

        private object? SerializeResult(object? result)
        {
            if (result == null) return null;

            // Prevent trying to serialize complex Revit Elements and crashing MCP Newtonsoft
            if (result is Element element)
            {
                return new { Type = result.GetType().Name, Id = element.Id.Value, Name = element.Name };
            }
            
            if (result is ElementId elementId)
            {
                return new { Type = "ElementId", Id = elementId.Value };
            }
            
            var type = result.GetType();
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
            {
                return result;
            }

            if (result is System.Collections.IEnumerable enumerable)
            {
                var list = new List<object?>();
                foreach (var item in enumerable)
                {
                    list.Add(SerializeResult(item));
                }
                return list;
            }

            return result.ToString(); // Safe fallback for unhandled complex types
        }
    }
}

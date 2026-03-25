using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using RevitPlugin.Attributes;

namespace RevitPlugin.Core
{
    public static class SchemaGenerator
    {
        public static void GenerateManifest()
        {
            var tools = new List<object>();
            var assembly = Assembly.GetExecutingAssembly();

            foreach (var type in assembly.GetTypes())
            {
                var toolAttr = type.GetCustomAttribute<McpToolAttribute>();
                if (toolAttr == null) continue;

                var argsType = type.GetNestedType("Args") ?? type.GetNestedType("Arguments");
                var parameters = new Dictionary<string, object>();
                var required = new List<string>();

                if (argsType != null)
                {
                    foreach (var prop in argsType.GetProperties())
                    {
                        var argAttr = prop.GetCustomAttribute<McpArgAttribute>();
                        if (argAttr == null) continue;

                        var paramSchema = new Dictionary<string, object>
                        {
                            { "type", GetJsonType(prop.PropertyType) },
                            { "description", argAttr.Description }
                        };

                        if (prop.PropertyType.IsEnum)
                        {
                            paramSchema["enum"] = Enum.GetNames(prop.PropertyType);
                        }

                        parameters[prop.Name] = paramSchema;

                        if (argAttr.Required)
                        {
                            required.Add(prop.Name);
                        }
                    }
                }

                tools.Add(new
                {
                    name = toolAttr.Name,
                    description = toolAttr.Description,
                    inputSchema = new
                    {
                        type = "object",
                        properties = parameters,
                        required = required
                    }
                });
            }

            var manifest = new { tools };
            var json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
            
             // Use a common path writable by both Revit and Node.js
            var folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RevitMcp");
            Directory.CreateDirectory(folderPath);
            File.WriteAllText(Path.Combine(folderPath, "manifest.json"), json);
        }

        private static string GetJsonType(Type type)
        {
            if (type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(float)) return "number";
            if (type == typeof(bool)) return "boolean";
            return "string";
        }
    }
}

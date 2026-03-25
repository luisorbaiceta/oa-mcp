using System;

namespace RevitPlugin.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class McpToolAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }
        public bool RequiresTransaction { get; }

        public McpToolAttribute(string name, string description, bool requiresTransaction = false)
        {
            Name = name;
            Description = description;
            RequiresTransaction = requiresTransaction;
        }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    public class McpArgAttribute : Attribute
    {
        public string Description { get; }
        public bool Required { get; }

        public McpArgAttribute(string description, bool required = true)
        {
            Description = description;
            Required = required;
        }
    }
}

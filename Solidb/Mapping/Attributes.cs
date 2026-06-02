using System;

namespace Solidb.Mapping
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TableAttribute : Attribute
    {
        public string Name { get; }
        public TableAttribute(string name) => Name = name;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class KeyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ColumnAttribute : Attribute
    {
        public string Name { get; }
        public ColumnAttribute(string name) => Name = name;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ForeignKeyAttribute : Attribute
    {
        public string Name { get; }
        public ForeignKeyAttribute(string name) => Name = name;
    }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class NotMappedAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class GeneratedAttribute : Attribute { }
}

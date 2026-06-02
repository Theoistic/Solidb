using System;

namespace Solidb.Mapping
{
    public enum RelationKind
    {
        OneToOne,
        OneToMany,
        ManyToOne,
        ManyToMany
    }

    public sealed class RelationMap
    {
        public string NavigationName { get; }
        public Type PrincipalType { get; }
        public Type DependentType { get; }
        public string ForeignKeyName { get; }
        public RelationKind Kind { get; }

        public RelationMap(
            string navigationName,
            Type principalType,
            Type dependentType,
            string foreignKeyName,
            RelationKind kind)
        {
            NavigationName = navigationName;
            PrincipalType = principalType;
            DependentType = dependentType;
            ForeignKeyName = foreignKeyName;
            Kind = kind;
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore.ModelBuilding
{
    public partial class RelationalModelBuilderTest
    {
        public abstract class RelationalJsonTestBase : ModelBuilderTestBase
        {
            protected class JsonEntity
            {
                public int Id { get; set; }
                public string Name { get; set; }

                public OwnedEntity OwnedReference1 { get; set; }
                public OwnedEntity OwnedReference2 { get; set; }

                public List<OwnedEntity> OwnedCollection1 { get; set; }
                public List<OwnedEntity> OwnedCollection2 { get; set; }
            }

            protected class OwnedEntity
            {
                public DateTime Date { get; set; }
                public double Fraction { get; set; }
                public MyJsonEnum Enum { get; set; }
            }

            protected enum MyJsonEnum
            {
                One,
                Two,
                Three,
            }

            protected class JsonEntityInheritanceBase
            {
                public int Id { get; set; }
                public OwnedEntity OwnedReferenceOnBase { get; set; }
                public List<OwnedEntity> OwnedCollectionOnBase { get; set; }
            }

            protected class JsonEntityInheritanceDerived : JsonEntityInheritanceBase
            {
                public string Name { get; set; }
                public OwnedEntity OwnedReferenceOnDerived { get; set; }
                public List<OwnedEntity> OwnedCollectionOnDerived { get; set; }
            }

            protected class OwnedEntityExtraLevel
            {
                public DateTime Date { get; set; }
                public double Fraction { get; set; }
                public MyJsonEnum Enum { get; set; }

                public OwnedEntity Reference1 { get; set; }
                public OwnedEntity Reference2 { get; set; }
                public List<OwnedEntity> Collection1 { get; set; }
                public List<OwnedEntity> Collection2 { get; set; }
            }

            protected class JsonEntityWithNesting
            {
                public int Id { get; set; }
                public string Name { get; set; }

                public OwnedEntityExtraLevel OwnedReference1 { get; set; }
                public OwnedEntityExtraLevel OwnedReference2 { get; set; }
                public List<OwnedEntityExtraLevel> OwnedCollection1 { get; set; }
                public List<OwnedEntityExtraLevel> OwnedCollection2 { get; set; }
            }
        }
    }
}

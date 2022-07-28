// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore.ModelBuilding
{
    public partial class RelationalModelBuilderTest
    {
        public abstract class RelationalJsonTestBase : ModelBuilderTestBase
        {
            [ConditionalFact]
            public virtual void Json_entity_and_normal_owned_can_exist_side_to_side_on_same_entity()
            {
                var modelBuilder = CreateModelBuilder();

                modelBuilder.Entity<JsonEntity>(b =>
                {
                    b.OwnsOne(x => x.OwnedReference1);
                    b.OwnsOne(x => x.OwnedReference2, bb => bb.ToJson("reference"));
                    b.OwnsMany(x => x.OwnedCollection1);
                    b.OwnsMany(x => x.OwnedCollection2, bb => bb.ToJson("collection"));
                });

                var model = modelBuilder.FinalizeModel();

                var ownedEntities = model.FindEntityTypes(typeof(OwnedEntity));
                Assert.Equal(4, ownedEntities.Count());
                Assert.Equal(2, ownedEntities.Where(e => e.IsMappedToJson()).Count());
                Assert.Equal(2, ownedEntities.Where(e => e.IsOwned() && !e.IsMappedToJson()).Count());
            }

            [ConditionalFact]
            public virtual void Json_entity_with_tph_inheritance()
            {
                var modelBuilder = CreateModelBuilder();

                modelBuilder.Entity<JsonEntityInheritanceBase>(b =>
                {
                    b.OwnsOne(x => x.OwnedReferenceOnBase, bb => bb.ToJson("reference_on_base"));
                    b.OwnsMany(x => x.OwnedCollectionOnBase, bb => bb.ToJson("collection_on_base"));
                });

                modelBuilder.Entity<JsonEntityInheritanceDerived>(b =>
                {
                    b.HasBaseType<JsonEntityInheritanceBase>();
                    b.OwnsOne(x => x.OwnedReferenceOnDerived, bb => bb.ToJson("reference_on_derived"));
                    b.OwnsMany(x => x.OwnedCollectionOnDerived, bb => bb.ToJson("collection_on_derived"));
                });

                var model = modelBuilder.FinalizeModel();
                var ownedEntities = model.FindEntityTypes(typeof(OwnedEntity));
                Assert.Equal(4, ownedEntities.Count());
            }

            [ConditionalFact]
            public virtual void Json_entity_with_nested_structure_same_property_names_()
            {
                var modelBuilder = CreateModelBuilder();
                modelBuilder.Entity<JsonEntityWithNesting>(b =>
                {
                    b.OwnsOne(x => x.OwnedReference1, bb =>
                    {
                        bb.ToJson("ref1");
                        bb.OwnsOne(x => x.Reference1);
                        bb.OwnsOne(x => x.Reference2);
                        bb.OwnsMany(x => x.Collection1);
                        bb.OwnsMany(x => x.Collection2);
                    });

                    b.OwnsOne(x => x.OwnedReference2, bb =>
                    {
                        bb.ToJson("ref2");
                        bb.OwnsOne(x => x.Reference1);
                        bb.OwnsOne(x => x.Reference2);
                        bb.OwnsMany(x => x.Collection1);
                        bb.OwnsMany(x => x.Collection2);
                    });

                    b.OwnsMany(x => x.OwnedCollection1, bb =>
                    {
                        bb.ToJson("col1");
                        bb.OwnsOne(x => x.Reference1);
                        bb.OwnsOne(x => x.Reference2);
                        bb.OwnsMany(x => x.Collection1);
                        bb.OwnsMany(x => x.Collection2);
                    });

                    b.OwnsMany(x => x.OwnedCollection2, bb =>
                    {
                        bb.ToJson("col2");
                        bb.OwnsOne(x => x.Reference1);
                        bb.OwnsOne(x => x.Reference2);
                        bb.OwnsMany(x => x.Collection1);
                        bb.OwnsMany(x => x.Collection2);
                    });
                });

                var model = modelBuilder.FinalizeModel();
                var outerOwnedEntities = model.FindEntityTypes(typeof(OwnedEntityExtraLevel));
                Assert.Equal(4, outerOwnedEntities.Count());

                var ownedEntities = model.FindEntityTypes(typeof(OwnedEntity));
                Assert.Equal(16, ownedEntities.Count());
            }

            private class JsonEntity
            {
                public int Id { get; set; }
                public string Name { get; set; }

                public OwnedEntity OwnedReference1 { get; set; }
                public OwnedEntity OwnedReference2 { get; set; }

                public List<OwnedEntity> OwnedCollection1 { get; set; }
                public List<OwnedEntity> OwnedCollection2 { get; set; }
            }

            public class OwnedEntity
            {
                public DateTime Date { get; set; }
                public double Fraction { get; set; }
            }

            public class JsonEntityInheritanceBase
            {
                public int Id { get; set; }
                public OwnedEntity OwnedReferenceOnBase { get; set; }
                public List<OwnedEntity> OwnedCollectionOnBase { get; set; }
            }

            public class JsonEntityInheritanceDerived : JsonEntityInheritanceBase
            {
                public string Name { get; set; }
                public OwnedEntity OwnedReferenceOnDerived { get; set; }
                public List<OwnedEntity> OwnedCollectionOnDerived { get; set; }
            }

            public class OwnedEntityExtraLevel
            {
                public DateTime Date { get; set; }
                public double Fraction { get; set; }

                public OwnedEntity Reference1 { get; set; }
                public OwnedEntity Reference2 { get; set; }
                public List<OwnedEntity> Collection1 { get; set; }
                public List<OwnedEntity> Collection2 { get; set; }
            }

            public class JsonEntityWithNesting
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

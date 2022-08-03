﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore.Infrastructure
{
    public partial class RelationalModelValidatorTest
    {
        [ConditionalFact]
        public void Throw_when_non_json_entity_is_the_owner_of_json_entity_ref_ref()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityBasic>(b =>
            {
                b.OwnsOne(x => x.OwnedReference, bb =>
                {
                    bb.Ignore(x => x.NestedCollection);
                    bb.OwnsOne(x => x.NestedReference, bbb => bbb.ToJson("reference_reference"));
                });
                b.Ignore(x => x.OwnedCollection);
            });

            VerifyError(
                "Owned entity type 'ValidatorJsonOwnedRoot' is mapped to table 'ValidatorJsonEntityBasic' and contains JSON columns. This is currently not supported. All owned types containing a JSON column must be mapped to a JSON column themselves.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Throw_when_non_json_entity_is_the_owner_of_json_entity_ref_col()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityBasic>(b =>
            {
                b.OwnsOne(x => x.OwnedReference, bb =>
                {
                    bb.OwnsMany(x => x.NestedCollection, bbb => bbb.ToJson("reference_collection"));
                    bb.Ignore(x => x.NestedReference);
                });
                b.Ignore(x => x.OwnedCollection);
            });

            VerifyError(
                "Owned entity type 'ValidatorJsonOwnedRoot' is mapped to table 'ValidatorJsonEntityBasic' and contains JSON columns. This is currently not supported. All owned types containing a JSON column must be mapped to a JSON column themselves.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Throw_when_non_json_entity_is_the_owner_of_json_entity_col_ref()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityBasic>(b =>
            {
                b.OwnsMany(x => x.OwnedCollection, bb =>
                {
                    bb.Ignore(x => x.NestedCollection);
                    bb.OwnsOne(x => x.NestedReference, bbb => bbb.ToJson("collection_reference"));
                });
                b.Ignore(x => x.OwnedReference);
            });

            VerifyError(
                "Owned entity type 'ValidatorJsonOwnedRoot' is mapped to table 'ValidatorJsonOwnedRoot' and contains JSON columns. This is currently not supported. All owned types containing a JSON column must be mapped to a JSON column themselves.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Throw_when_non_json_entity_is_the_owner_of_json_entity_col_col()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityBasic>(b =>
            {
                b.OwnsMany(x => x.OwnedCollection, bb =>
                {
                    bb.Ignore(x => x.NestedReference);
                    bb.OwnsMany(x => x.NestedCollection, bbb => bbb.ToJson("collection_collection"));
                });
                b.Ignore(x => x.OwnedReference);
            });

            VerifyError(
                "Owned entity type 'ValidatorJsonOwnedRoot' is mapped to table 'ValidatorJsonOwnedRoot' and contains JSON columns. This is currently not supported. All owned types containing a JSON column must be mapped to a JSON column themselves.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Throw_when_json_entity_references_another_non_json_entity_via_reference()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityReferencedEntity>();
            modelBuilder.Entity<ValidatorJsonEntityJsonReferencingRegularEntity>(b =>
            {
                b.OwnsOne(x => x.Owned, bb =>
                {
                    bb.ToJson("reference");
                    bb.HasOne(x => x.Reference).WithOne().HasForeignKey<ValidatorJsonOwnedReferencingRegularEntity>(x => x.Fk);
                });
            });

            VerifyError(
                "Entity type 'ValidatorJsonOwnedReferencingRegularEntity' is mapped to JSON and has navigation to a regular entity which is not the owner.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Tpt_not_supported_for_owner_of_json_entity_on_base()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityInheritanceBase>(b =>
            {
                b.ToTable("Table1");
                b.OwnsOne(x => x.ReferenceOnBase, bb =>
                {
                    bb.ToJson("reference");
                });
            });

            modelBuilder.Entity<ValidatorJsonEntityInheritanceDerived>(b =>
            {
                b.HasBaseType<ValidatorJsonEntityInheritanceBase>();
                b.ToTable("Table2");
                b.Ignore(x => x.ReferenceOnDerived);
                b.Ignore(x => x.CollectionOnDerived);
            });

            VerifyError(
                "Entity type 'ValidatorJsonEntityInheritanceBase' references entities mapped to JSON. Only 'TPH' inheritance is supported for those entities.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Tpt_not_supported_for_owner_of_json_entity_on_derived()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityInheritanceBase>(b =>
            {
                b.ToTable("Table1");
                b.Ignore(x => x.ReferenceOnBase);
            });

            modelBuilder.Entity<ValidatorJsonEntityInheritanceDerived>(b =>
            {
                b.ToTable("Table2");
                b.OwnsOne(x => x.ReferenceOnDerived, bb => bb.ToJson("reference"));
                b.Ignore(x => x.CollectionOnDerived);
            });

            VerifyError(
                "Entity type 'ValidatorJsonEntityInheritanceBase' references entities mapped to JSON. Only 'TPH' inheritance is supported for those entities.",
                modelBuilder);
        }


        [ConditionalFact]
        public void Tpt_not_supported_for_owner_of_json_entity_mapping_strategy_explicitly_defined()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityInheritanceBase>(b =>
            {
                b.UseTptMappingStrategy();
                b.OwnsOne(x => x.ReferenceOnBase, bb =>
                {
                    bb.ToJson("reference");
                });
            });

            modelBuilder.Entity<ValidatorJsonEntityInheritanceDerived>(b =>
            {
                b.HasBaseType<ValidatorJsonEntityInheritanceBase>();
                b.Ignore(x => x.ReferenceOnDerived);
                b.Ignore(x => x.CollectionOnDerived);
            });

            VerifyError(
                "Entity type 'ValidatorJsonEntityInheritanceBase' references entities mapped to JSON. Only 'TPH' inheritance is supported for those entities.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Tpt_not_supported_for_owner_of_json_entity_same_table_names_different_schemas()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityInheritanceBase>(b =>
            {
                b.ToTable("Table", "mySchema1");
                b.Ignore(x => x.ReferenceOnBase);
            });

            modelBuilder.Entity<ValidatorJsonEntityInheritanceDerived>(b =>
            {
                b.ToTable("Table", "mySchema2");
                b.OwnsOne(x => x.ReferenceOnDerived, bb => bb.ToJson("reference"));
                b.Ignore(x => x.CollectionOnDerived);
            });

            VerifyError(
                "Entity type 'ValidatorJsonEntityInheritanceBase' references entities mapped to JSON. Only 'TPH' inheritance is supported for those entities.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Tpc_not_supported_for_owner_of_json_entity()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityInheritanceBase>().UseTpcMappingStrategy();
            modelBuilder.Entity<ValidatorJsonEntityInheritanceAbstract>();
            modelBuilder.Entity<ValidatorJsonEntityInheritanceBase>(b => b.Ignore(x => x.ReferenceOnBase));

            modelBuilder.Entity<ValidatorJsonEntityInheritanceDerived>(b =>
            {
                b.OwnsOne(x => x.ReferenceOnDerived, bb => bb.ToJson("reference"));
                b.Ignore(x => x.CollectionOnDerived);
            });

            VerifyError(
                "Entity type 'ValidatorJsonEntityInheritanceBase' references entities mapped to JSON. Only 'TPH' inheritance is supported for those entities.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Json_entity_not_mapped_to_table_or_a_view_is_not_supported()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityBasic>(b =>
            {
                b.ToTable((string)null);
                b.OwnsOne(x => x.OwnedReference, bb =>
                {
                    bb.ToJson("reference");
                    bb.Ignore(x => x.NestedReference);
                    bb.Ignore(x => x.NestedCollection);
                });
                b.Ignore(x => x.OwnedCollection);
            });

            VerifyError(
                "Entity type 'ValidatorJsonEntityBasic' references entities mapped to JSON but is not itself mapped to a table or a view. This is not supported.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Json_multiple_json_entities_mapped_to_the_same_column()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntitySideBySide>(b =>
            {
                b.OwnsOne(x => x.Reference1, bb => bb.ToJson("json"));
                b.OwnsOne(x => x.Reference2, bb => bb.ToJson("json"));
                b.Ignore(x => x.Collection1);
                b.Ignore(x => x.Collection2);
            });

            VerifyError(
                "Multiple aggregates are mapped to the same JSON column 'json' in table 'ValidatorJsonEntitySideBySide'. Each aggreagate must map to a different column.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Json_entity_with_defalt_value_on_a_property()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityBasic>(b =>
            {
                b.Ignore(x => x.OwnedCollection);
                b.OwnsOne(x => x.OwnedReference, bb =>
                {
                    bb.Ignore(x => x.NestedReference);
                    bb.Ignore(x => x.NestedCollection);
                    bb.ToJson("json");
                    bb.Property(x => x.Name).HasDefaultValue("myDefault");
                });
            });

            VerifyError(
                "Setting default value on properties of an entity mapped to JSON is not supported. Entity: 'ValidatorJsonOwnedRoot', property: 'Name'.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Json_entity_with_table_splitting_throws()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityBasic>(b =>
            {
                b.ToTable("SharedTable");
                b.OwnsOne(x => x.OwnedReference, bb =>
                {
                    bb.ToJson("json");
                    bb.OwnsOne(x => x.NestedReference);
                    bb.OwnsMany(x => x.NestedCollection);
                });
                b.Ignore(x => x.OwnedCollection);
            });

            modelBuilder.Entity<ValidatorJsonEntityTableSplitting>(b =>
            {
                b.ToTable("SharedTable");
                b.HasOne(x => x.Link).WithOne().HasForeignKey<ValidatorJsonEntityBasic>(x => x.Id);
            });

            VerifyError(
                "Table splitting is not supported for entities containing entities mapped to JSON.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Json_entity_with_explicit_ordinal_key_on_collection_throws()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityExplicitOrdinal>(b =>
            {
                b.OwnsMany(x => x.OwnedCollection, bb =>
                {
                    bb.ToJson("json");
                    bb.HasKey(x => x.Ordinal);
                });
            });

            VerifyError(
                "Entity type 'ValidatorJsonOwnedExplicitOrdinal' is part of collection mapped to JSON and has it's ordinal key defined explicitly. Only implicitly defined ordinal keys are supported.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Json_entity_with_key_having_json_property_name_configured_explicitly_throws()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityExplicitOrdinal>(b =>
            {
                b.OwnsMany(x => x.OwnedCollection, bb =>
                {
                    bb.ToJson("json");
                    bb.HasKey(x => x.Ordinal);
                    bb.Property(x => x.Ordinal).HasJsonPropertyName("Foo");
                });
            });

            VerifyError(
                "Key property 'Ordinal' on JSON-mapped entity 'ValidatorJsonOwnedExplicitOrdinal' should not have JSON property name configured explicitly.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Json_entity_with_multiple_properties_mapped_to_same_json_name()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityBasic>(b =>
            {
                b.OwnsOne(x => x.OwnedReference, bb =>
                {
                    bb.Property(x => x.Name).HasJsonPropertyName("Foo");
                    bb.Property(x => x.Number).HasJsonPropertyName("Foo");
                    bb.ToJson("reference");
                    bb.Ignore(x => x.NestedReference);
                    bb.Ignore(x => x.NestedCollection);
                });
                b.Ignore(x => x.OwnedCollection);
            });

            VerifyError(
                "Entity 'ValidatorJsonOwnedRoot' is mapped to JSON and it contains multiple properties or navigations which are mapped to the same JSON property 'Foo'. Each property should map to a unique JSON property.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Json_entity_with_property_and_navigation_mapped_to_same_json_name()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityBasic>(b =>
            {
                b.OwnsOne(x => x.OwnedReference, bb =>
                {
                    bb.Property(x => x.Name);
                    bb.Property(x => x.Number);
                    bb.ToJson("reference");
                    bb.OwnsOne(x => x.NestedReference);
                    bb.Navigation(x => x.NestedReference).HasJsonPropertyName("Name");
                    bb.Ignore(x => x.NestedCollection);
                });
b.Ignore(x => x.OwnedCollection);
});

            VerifyError(
                "Entity 'ValidatorJsonOwnedRoot' is mapped to JSON and it contains multiple properties or navigations which are mapped to the same JSON property 'Name'. Each property should map to a unique JSON property.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Json_on_base_and_derived_mapped_to_same_column_throws()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityInheritanceBase>().OwnsOne(x => x.ReferenceOnBase, b => b.ToJson("jsonColumn"));
            modelBuilder.Entity<ValidatorJsonEntityInheritanceDerived>(b =>
            {
                b.HasBaseType<ValidatorJsonEntityInheritanceBase>();
                b.OwnsOne(x => x.ReferenceOnDerived, bb => bb.ToJson("jsonColumn"));
                b.Ignore(x => x.CollectionOnDerived);
            });

            VerifyError(
                "Multiple aggregates are mapped to the same JSON column 'jsonColumn' in table 'ValidatorJsonEntityInheritanceBase'. Each aggreagate must map to a different column.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Json_entity_mapped_to_different_view_than_its_root_aggregate()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityBasic>(b =>
            {
                b.ToView("MyView");
                b.OwnsOne(x => x.OwnedReference, bb =>
                {
                    bb.ToJson();
                    bb.ToView("MyOtherView");
                    bb.Ignore(x => x.NestedReference);
                    bb.Ignore(x => x.NestedCollection);
                });
                b.Ignore(x => x.OwnedCollection);
            });

            VerifyError(
                "Entity 'ValidatorJsonOwnedRoot' is mapped to JSON and also mapped to a view 'MyOtherView', however it's owner 'ValidatorJsonEntityBasic' is mapped to a different view 'MyView'. Every entity mapped to JSON must also map to the same view as it's owner.",
                modelBuilder);
        }

        [ConditionalFact]
        public void Json_entity_mapped_to_different_view_than_its_parent()
        {
            var modelBuilder = CreateConventionModelBuilder();
            modelBuilder.Entity<ValidatorJsonEntityBasic>(b =>
            {
                b.ToView("MyView");
                b.OwnsOne(x => x.OwnedReference, bb =>
                {
                    bb.ToJson();
                    bb.ToView("MyView");
                    bb.OwnsMany(x => x.NestedCollection, bbb => bbb.ToView("MyOtherView"));
                    bb.Ignore(x => x.NestedReference);
                });
                b.Ignore(x => x.OwnedCollection);
            });

            VerifyError(
                "Entity 'ValidatorJsonOwnedBranch' is mapped to JSON and also mapped to a view 'MyOtherView', however it's owner 'ValidatorJsonOwnedRoot' is mapped to a different view 'MyView'. Every entity mapped to JSON must also map to the same view as it's owner.",
                modelBuilder);
        }

        private class ValidatorJsonEntityBasic
        {
            public int Id { get; set; }
            public ValidatorJsonOwnedRoot OwnedReference { get; set; }
            public List<ValidatorJsonOwnedRoot> OwnedCollection { get; set; }
        }

        private abstract class ValidatorJsonEntityInheritanceAbstract : ValidatorJsonEntityInheritanceBase
        {
            public Guid Guid { get; set; }
        }

        private class ValidatorJsonEntityInheritanceBase
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public ValidatorJsonOwnedBranch ReferenceOnBase { get; set; }

        }

        private class ValidatorJsonEntityInheritanceDerived : ValidatorJsonEntityInheritanceAbstract
        {
            public bool Switch { get; set; }

            public ValidatorJsonOwnedBranch ReferenceOnDerived { get; set; }

            public List<ValidatorJsonOwnedBranch> CollectionOnDerived { get; set; }
        }

        private class ValidatorJsonOwnedRoot
        {
            public string Name { get; set; }
            public int Number { get; set; }

            public ValidatorJsonOwnedBranch NestedReference { get; set; }
            public List<ValidatorJsonOwnedBranch> NestedCollection { get; set; }
        }

        private class ValidatorJsonOwnedBranch
        {
            public double Number { get; set; }
        }

        private class ValidatorJsonEntityExplicitOrdinal
        {
            public int Id { get; set; }

            public string Name { get; set; }

            public List<ValidatorJsonOwnedExplicitOrdinal> OwnedCollection { get; set; }
        }

        private class ValidatorJsonOwnedExplicitOrdinal
        {
            public int Ordinal { get; set; }
            public DateTime Date { get; set; }
        }

        private class ValidatorJsonEntityJsonReferencingRegularEntity
        {
            public int Id { get; set; }
            public ValidatorJsonOwnedReferencingRegularEntity Owned { get; set; }
        }

        private class ValidatorJsonOwnedReferencingRegularEntity
        {
            public string Foo { get; set; }

            public int? Fk { get; set; }
            public ValidatorJsonEntityReferencedEntity Reference { get; set; }
        }

        private class ValidatorJsonEntityReferencedEntity
        {
            public int Id { get; set; }
            public DateTime Date { get; set; }
        }

        private class ValidatorJsonEntitySideBySide
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public ValidatorJsonOwnedBranch Reference1 { get; set; }
            public ValidatorJsonOwnedBranch Reference2 { get; set; }
            public List<ValidatorJsonOwnedBranch> Collection1 { get; set; }
            public List<ValidatorJsonOwnedBranch> Collection2 { get; set; }
        }

        private class ValidatorJsonEntityTableSplitting
        {
            public int Id { get; set; }
            public ValidatorJsonEntityBasic Link { get; set; }
        }
    }
}

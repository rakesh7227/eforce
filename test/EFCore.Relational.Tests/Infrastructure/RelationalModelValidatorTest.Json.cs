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
                "Json mapped type can't be owned by a non-json owned type. Only regular entity types or json mapped types are allowed.",
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
                "Json mapped type can't be owned by a non-json owned type. Only regular entity types or json mapped types are allowed.",
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
                "Json mapped type can't be owned by a non-json owned type. Only regular entity types or json mapped types are allowed.",
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
                "Json mapped type can't be owned by a non-json owned type. Only regular entity types or json mapped types are allowed.",
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
                "Entity type 'ValidatorJsonOwnedReferencingRegularEntity' is mapped to json and has navigation to a regular entity which is not the owner.",
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
                "Entity type 'ValidatorJsonEntityInheritanceBase' references entities mapped to json. Only 'TPH' inheritance is supported for those entities.",
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
                "Entity type 'ValidatorJsonEntityInheritanceDerived' references entities mapped to json. Only 'TPH' inheritance is supported for those entities.",
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
                "Entity type 'ValidatorJsonEntityInheritanceBase' references entities mapped to json. Only 'TPH' inheritance is supported for those entities.",
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
                "Entity type 'ValidatorJsonEntityInheritanceDerived' references entities mapped to json. Only 'TPH' inheritance is supported for those entities.",
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
                "Entity type 'ValidatorJsonEntityInheritanceDerived' references entities mapped to json. Only 'TPH' inheritance is supported for those entities.",
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
                "Entity type 'ValidatorJsonEntityBasic' references entities mapped to json but is not itself mapped to a table or a view. This is not supported.",
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

        public class ValidatorJsonOwnedRoot
        {
            public string Name { get; set; }

            public ValidatorJsonOwnedBranch NestedReference { get; set; }
            public List<ValidatorJsonOwnedBranch> NestedCollection { get; set; }
        }

        public class ValidatorJsonOwnedBranch
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

        public class ValidatorJsonOwnedReferencingRegularEntity
        {
            public string Foo { get; set; }

            public int? Fk { get; set; }
            public ValidatorJsonEntityReferencedEntity Reference { get; set; }
        }

        public class ValidatorJsonEntityReferencedEntity
        {
            public int Id { get; set; }
            public DateTime Date { get; set; }
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

/// <summary>
///     <para>
///         A service on the EF internal service provider that creates the <see cref="ConventionSet" />
///         for the current relational database provider. This is combined with <see cref="IConventionSetPlugin" />
///         instances to produce the full convention set exposed by the <see cref="IConventionSetBuilder" />
///         service.
///     </para>
///     <para>
///         This type is typically used by database providers (and other extensions). It is generally
///         not used in application code.
///     </para>
/// </summary>
/// <remarks>
///     <para>
///         Database providers should implement this service by inheriting from either
///         this class (for relational providers) or <see cref="ProviderConventionSetBuilder" /> (for non-relational providers).
///     </para>
///     <para>
///         The service lifetime is <see cref="ServiceLifetime.Scoped" />. This means that each
///         <see cref="DbContext" /> instance will use its own instance of this service.
///         The implementation may depend on other services registered with any lifetime.
///         The implementation does not need to be thread-safe.
///     </para>
///     <para>
///         See <see href="https://aka.ms/efcore-docs-conventions">Model building conventions</see> for more information and examples.
///     </para>
/// </remarks>
public abstract class RelationalConventionSetBuilder : ProviderConventionSetBuilder
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="RelationalConventionSetBuilder" /> class.
    /// </summary>
    /// <param name="dependencies">Parameter object containing dependencies for this service.</param>
    /// <param name="relationalDependencies">Parameter object containing relational dependencies for this service.</param>
    protected RelationalConventionSetBuilder(
        ProviderConventionSetBuilderDependencies dependencies,
        RelationalConventionSetBuilderDependencies relationalDependencies)
        : base(dependencies)
    {
        RelationalDependencies = relationalDependencies;
    }

    /// <summary>
    ///     Relational provider-specific dependencies for this service.
    /// </summary>
    protected virtual RelationalConventionSetBuilderDependencies RelationalDependencies { get; }

    /// <summary>
    ///     Builds and returns the convention set for the current database provider.
    /// </summary>
    /// <returns>The convention set for the current database provider.</returns>
    public override ConventionSet CreateConventionSet()
    {
        var conventionSet = base.CreateConventionSet();

        var relationalColumnAttributeConvention = new RelationalColumnAttributeConvention(Dependencies, RelationalDependencies);
        var relationalCommentAttributeConvention = new RelationalColumnCommentAttributeConvention(Dependencies, RelationalDependencies);
        var relationalJsonPropertyNameAttributeConvention = new RelationalJsonPropertyNameAttributeConvention(Dependencies, RelationalDependencies);

        conventionSet.PropertyAddedConventions.Add(relationalColumnAttributeConvention);
        conventionSet.PropertyAddedConventions.Add(relationalCommentAttributeConvention);
        conventionSet.PropertyAddedConventions.Add(relationalJsonPropertyNameAttributeConvention);

        var relationalNavigationJsonPropertyNameAttributeConvention = new RelationalNavigationJsonPropertyNameAttributeConvention(Dependencies, RelationalDependencies);
        conventionSet.NavigationAddedConventions.Add(relationalNavigationJsonPropertyNameAttributeConvention);

        var tableNameFromDbSetConvention = new TableNameFromDbSetConvention(Dependencies, RelationalDependencies);
        var entitySplittingConvention = new EntitySplittingConvention(Dependencies, RelationalDependencies);
        var checkConstraintConvention = new CheckConstraintConvention(Dependencies, RelationalDependencies);
        var triggerConvention = new TriggerConvention(Dependencies, RelationalDependencies);
        conventionSet.EntityTypeAddedConventions.Add(new RelationalTableAttributeConvention(Dependencies, RelationalDependencies));
        conventionSet.EntityTypeAddedConventions.Add(
            new RelationalTableCommentAttributeConvention(Dependencies, RelationalDependencies));
        conventionSet.EntityTypeAddedConventions.Add(tableNameFromDbSetConvention);
        conventionSet.EntityTypeAddedConventions.Add(entitySplittingConvention);
        conventionSet.EntityTypeAddedConventions.Add(checkConstraintConvention);
        conventionSet.EntityTypeAddedConventions.Add(triggerConvention);

        ValueGenerationConvention valueGenerationConvention =
            new RelationalValueGenerationConvention(Dependencies, RelationalDependencies);
        ReplaceConvention(conventionSet.EntityTypeBaseTypeChangedConventions, valueGenerationConvention);
        conventionSet.EntityTypeBaseTypeChangedConventions.Add(tableNameFromDbSetConvention);
        conventionSet.EntityTypeBaseTypeChangedConventions.Add(checkConstraintConvention);
        conventionSet.EntityTypeBaseTypeChangedConventions.Add(triggerConvention);

        var mapToJsonConvention = new RelationalMapToJsonConvention(Dependencies, RelationalDependencies);
        conventionSet.EntityTypeAnnotationChangedConventions.Add(mapToJsonConvention);
        conventionSet.NavigationAddedConventions.Add(mapToJsonConvention);

        ReplaceConvention(conventionSet.ForeignKeyPropertiesChangedConventions, valueGenerationConvention);

        ReplaceConvention(conventionSet.ForeignKeyOwnershipChangedConventions, valueGenerationConvention);

        conventionSet.EntityTypeAnnotationChangedConventions.Add((RelationalValueGenerationConvention)valueGenerationConvention);

        ReplaceConvention(conventionSet.EntityTypePrimaryKeyChangedConventions, valueGenerationConvention);

        ReplaceConvention(conventionSet.ForeignKeyAddedConventions, valueGenerationConvention);

        ReplaceConvention(conventionSet.ForeignKeyRemovedConventions, valueGenerationConvention);

        conventionSet.PropertyAddedConventions.Add(new PropertyOverridesConvention(Dependencies, RelationalDependencies));

        conventionSet.PropertyFieldChangedConventions.Add(relationalColumnAttributeConvention);
        conventionSet.PropertyFieldChangedConventions.Add(relationalCommentAttributeConvention);

        var storeGenerationConvention = new StoreGenerationConvention(Dependencies, RelationalDependencies);
        conventionSet.PropertyAnnotationChangedConventions.Add(storeGenerationConvention);
        conventionSet.PropertyAnnotationChangedConventions.Add((RelationalValueGenerationConvention)valueGenerationConvention);

        var dbFunctionAttributeConvention = new RelationalDbFunctionAttributeConvention(Dependencies, RelationalDependencies);
        conventionSet.ModelInitializedConventions.Add(dbFunctionAttributeConvention);

        // ModelCleanupConvention would remove the entity types added by TableValuedDbFunctionConvention #15898
        ConventionSet.AddAfter(
            conventionSet.ModelFinalizingConventions,
            new TableValuedDbFunctionConvention(Dependencies, RelationalDependencies),
            typeof(ModelCleanupConvention));
        conventionSet.ModelFinalizingConventions.Add(new TableSharingConcurrencyTokenConvention(Dependencies, RelationalDependencies));
        conventionSet.ModelFinalizingConventions.Add(dbFunctionAttributeConvention);
        conventionSet.ModelFinalizingConventions.Add(tableNameFromDbSetConvention);
        conventionSet.ModelFinalizingConventions.Add(storeGenerationConvention);
        conventionSet.ModelFinalizingConventions.Add(entitySplittingConvention);
        conventionSet.ModelFinalizingConventions.Add(new EntityTypeHierarchyMappingConvention(Dependencies, RelationalDependencies));
        conventionSet.ModelFinalizingConventions.Add(new SequenceUniquificationConvention(Dependencies, RelationalDependencies));
        conventionSet.ModelFinalizingConventions.Add(new SharedTableConvention(Dependencies, RelationalDependencies));
        ReplaceConvention(
            conventionSet.ModelFinalizingConventions,
            (QueryFilterRewritingConvention)new RelationalQueryFilterRewritingConvention(
                Dependencies, RelationalDependencies));

        ReplaceConvention(
            conventionSet.ModelFinalizedConventions,
            (RuntimeModelConvention)new RelationalRuntimeModelConvention(Dependencies, RelationalDependencies));

        return conventionSet;
    }
}

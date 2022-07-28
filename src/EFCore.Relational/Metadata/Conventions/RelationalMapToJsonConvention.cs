// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Microsoft.EntityFrameworkCore.Metadata.Conventions
{
    /// <summary>
    /// TODO
    /// </summary>
    public class RelationalMapToJsonConvention : IEntityTypeAnnotationChangedConvention, INavigationAddedConvention
    {
        /// <summary>
        ///     Creates a new instance of <see cref="RelationalMapToJsonConvention" />.
        /// </summary>
        /// <param name="dependencies">Parameter object containing dependencies for this convention.</param>
        /// <param name="relationalDependencies">Parameter object containing relational dependencies for this convention.</param>
        public RelationalMapToJsonConvention(
            ProviderConventionSetBuilderDependencies dependencies,
            RelationalConventionSetBuilderDependencies relationalDependencies)
        {
            Dependencies = dependencies;
            RelationalDependencies = relationalDependencies;
        }

        /// <summary>
        ///     Dependencies for this service.
        /// </summary>
        protected virtual ProviderConventionSetBuilderDependencies Dependencies { get; }

        /// <summary>
        ///     Relational provider-specific dependencies for this service.
        /// </summary>
        protected virtual RelationalConventionSetBuilderDependencies RelationalDependencies { get; }

        /// <summary>
        /// TODO
        /// </summary>
        public virtual void ProcessEntityTypeAnnotationChanged(
            IConventionEntityTypeBuilder entityTypeBuilder,
            string name,
            IConventionAnnotation? annotation,
            IConventionAnnotation? oldAnnotation,
            IConventionContext<IConventionAnnotation> context)
        {
            if (name != RelationalAnnotationNames.JsonColumnName)
            {
                return;
            }

            var jsonColumnName = annotation?.Value as string;
            if (!string.IsNullOrEmpty(jsonColumnName))
            {
                var jsonColumnTypeMapping = ((IRelationalTypeMappingSource)Dependencies.TypeMappingSource).FindMapping(
                    typeof(JsonElement))!;

                entityTypeBuilder.Metadata.SetJsonColumnTypeMapping(jsonColumnTypeMapping);

                //foreach (var navigation in entityTypeBuilder.Metadata.GetDeclaredNavigations()
                //    .Where(n => n.ForeignKey.IsOwnership
                //        && n.DeclaringEntityType == entityTypeBuilder.Metadata
                //        && n.TargetEntityType.IsOwned()))
                //{
                //    var currentJsonColumnName = navigation.TargetEntityType.JsonColumnName();
                //    if (currentJsonColumnName == null || currentJsonColumnName != jsonColumnName)
                //    {
                //        navigation.TargetEntityType.SetJsonColumnName(jsonColumnName);
                //    }
                //}

                // by default store enums as strings - values should be human-readable
                SetEnumStringConversion(entityTypeBuilder.Metadata);

                //foreach (var enumProperty in entityTypeBuilder.Metadata.GetDeclaredProperties().Where(p => p.ClrType.IsEnum))
                //{
                //    enumProperty.Builder.HasConversion(typeof(string));
                //}

                // TODO: go thru everything recursively and set the enums to string


                //foreach (var navigation in entityTypeBuilder.Metadata.GetDeclaredNavigations()
                //    .Where(n => n.ForeignKey.IsOwnership
                //        && n.DeclaringEntityType == entityTypeBuilder.Metadata
                //        && n.TargetEntityType.IsOwned()))
                //{
                //    var currentJsonColumnName = navigation.TargetEntityType.JsonColumnName();
                //    if (currentJsonColumnName == null || currentJsonColumnName != jsonColumnName)
                //    {
                //        navigation.TargetEntityType.SetJsonColumnName(jsonColumnName);
                //    }
                //}
            }
            else
            {
                // TODO: unwind everything
            }
        }

        private void SetEnumStringConversion(IConventionEntityType entityType)
        {
            // by default store enums as strings - values should be human-readable
            foreach (var enumProperty in entityType.GetDeclaredProperties().Where(p => p.ClrType.IsEnum))
            {
                enumProperty.Builder.HasConversion(typeof(string));
            }

            foreach (var navigation in entityType.GetDeclaredNavigations()
                .Where(n => n.ForeignKey.IsOwnership
                    && n.DeclaringEntityType == entityType
                    && n.TargetEntityType.IsOwned()))
            {
                SetEnumStringConversion(navigation.TargetEntityType);
            }
        }

        /// <summary>
        /// TODO
        /// </summary>
        public virtual void ProcessNavigationAdded(
            IConventionNavigationBuilder navigationBuilder,
            IConventionContext<IConventionNavigationBuilder> context)
        {
            if (navigationBuilder.Metadata.ForeignKey.IsOwnership)
            {
                if (navigationBuilder.Metadata.DeclaringEntityType.IsMappedToJson())
                {
                    SetEnumStringConversion(navigationBuilder.Metadata.TargetEntityType);
                }

                //if (navigationBuilder.Metadata.DeclaringEntityType.JsonColumnName() is string jsonColumnName)
                //{
                //    navigationBuilder.Metadata.TargetEntityType.SetJsonColumnName(jsonColumnName);
                //}
            }
        }
    }
}

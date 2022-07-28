// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore
{
    /// <summary>
    ///     Relational database specific extension methods for <see cref="OwnedNavigationBuilder" />.
    /// </summary>
    /// <remarks>
    ///     See <see href="https://aka.ms/efcore-docs-modeling">Modeling entity types and relationships</see> for more information and examples.
    /// </remarks>
    public static class RelationalOwnedNavigationBuilderExtensions
    {
        /// <summary>
        /// TODO
        /// </summary>
        public static OwnedNavigationBuilder ToJson(this OwnedNavigationBuilder builder)
        {
            var navigationName = builder.Metadata.GetNavigation(pointsToPrincipal: false)!.Name;
            builder.ToJson(navigationName);

            return builder;
        }

        /// <summary>
        /// TODO
        /// </summary>
        public static OwnedNavigationBuilder<TOwnerEntity, TDependentEntity> ToJson<TOwnerEntity, TDependentEntity>(
            this OwnedNavigationBuilder<TOwnerEntity, TDependentEntity> builder)
            where TOwnerEntity : class
            where TDependentEntity : class
        {
            var navigationName = builder.Metadata.GetNavigation(pointsToPrincipal: false)!.Name;
            builder.ToJson(navigationName);

            return builder;
        }

        /// <summary>
        /// TODO
        /// </summary>
        public static OwnedNavigationBuilder<TOwnerEntity, TDependentEntity> ToJson<TOwnerEntity, TDependentEntity>(
            this OwnedNavigationBuilder<TOwnerEntity, TDependentEntity> builder,
            string jsonColumnName)
            where TOwnerEntity : class
            where TDependentEntity : class
        {
            builder.OwnedEntityType.SetJsonColumnName(jsonColumnName);

            return builder;
        }

        /// <summary>
        /// TODO
        /// </summary>
        public static OwnedNavigationBuilder ToJson(
            this OwnedNavigationBuilder builder,
            string? jsonColumnName)
        {
            builder.OwnedEntityType.SetJsonColumnName(jsonColumnName);

            return builder;
        }
    }
}

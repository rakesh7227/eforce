// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.EntityFrameworkCore.Metadata.Conventions;

/// <summary>
///     A convention that configures json property name and type for a property based on the applied <see cref="JsonPropertyNameAttribute" />.
/// </summary>
/// <remarks>
///     See <see href="https://aka.ms/efcore-docs-conventions">Model building conventions</see> for more information and examples.
/// </remarks>
public class RelationalJsonPropertyNameAttributeConvention : PropertyAttributeConventionBase<JsonPropertyNameAttribute>
{
    /// <summary>
    ///     Creates a new instance of <see cref="RelationalJsonPropertyNameAttributeConvention" />.
    /// </summary>
    /// <param name="dependencies">Parameter object containing dependencies for this convention.</param>
    /// <param name="relationalDependencies"> Parameter object containing relational dependencies for this convention.</param>
    public RelationalJsonPropertyNameAttributeConvention(
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
    ///     Called after a property is added to the entity type with an attribute on the associated CLR property or field.
    /// </summary>
    /// <param name="propertyBuilder">The builder for the property.</param>
    /// <param name="attribute">The attribute.</param>
    /// <param name="clrMember">The member that has the attribute.</param>
    /// <param name="context">Additional information associated with convention execution.</param>
    protected override void ProcessPropertyAdded(
        IConventionPropertyBuilder propertyBuilder,
        JsonPropertyNameAttribute attribute,
        MemberInfo clrMember,
        IConventionContext context)
    {
        if (!string.IsNullOrWhiteSpace(attribute.Name))
        {
            propertyBuilder.HasJsonPropertyName(attribute.Name, fromDataAnnotation: true);
        }
    }
}

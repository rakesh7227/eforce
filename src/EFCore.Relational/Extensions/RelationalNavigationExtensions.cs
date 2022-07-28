// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore;

/// <summary>
///     Navigation extension methods for relational database metadata.
/// </summary>
/// <remarks>
///     See <see href="https://aka.ms/efcore-docs-modeling">Modeling entity types and relationships</see> for more information and examples.
/// </remarks>
public static class RelationalNavigationExtensions
{
    /// <summary>
    /// TODO
    /// </summary>
    public static string GetJsonPropertyName(this IReadOnlyNavigationBase navigation)
        => (string?)navigation.FindAnnotation(RelationalAnnotationNames.JsonPropertyName)?.Value ?? navigation.Name;

    /// <summary>
    /// TODO
    /// </summary>
    public static void SetJsonPropertyName(this IMutableNavigationBase navigation, string? name)
        => navigation.SetOrRemoveAnnotation(
            RelationalAnnotationNames.JsonPropertyName,
            Check.NullButNotEmpty(name, nameof(name)));

    /// <summary>
    /// TODO
    /// </summary>
    public static string? SetJsonPropertyName(
        this IConventionNavigationBase navigation,
        string? name,
        bool fromDataAnnotation = false)
    {
        navigation.SetOrRemoveAnnotation(
            RelationalAnnotationNames.JsonPropertyName,
            Check.NullButNotEmpty(name, nameof(name)),
            fromDataAnnotation);

        return name;
    }

    /// <summary>
    /// TODO
    /// </summary>
    public static ConfigurationSource? GetJsonPropertyNameConfigurationSource(this IConventionNavigationBase navigation)
        => navigation.FindAnnotation(RelationalAnnotationNames.JsonPropertyName)?.GetConfigurationSource();
}

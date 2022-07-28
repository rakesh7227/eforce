// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore.Metadata.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public class JsonColumn : Column, IColumn
{
    private readonly ValueComparer _providerValueComparer;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public JsonColumn(string name, string type, Table table, ValueComparer provierValueComparer)
        : base(name, type, table)
    {
        _providerValueComparer = provierValueComparer;
    }

    /// <inheritdoc />
    int? IColumn.MaxLength
        => null;

    /// <inheritdoc />
    int? IColumn.Precision
        => null;

    /// <inheritdoc />
    int? IColumn.Scale
        => null;

    /// <inheritdoc />
    bool? IColumn.IsUnicode
        => null;

    /// <inheritdoc />
    bool? IColumn.IsFixedLength
        => null;

    /// <inheritdoc />
    bool IColumn.IsRowVersion
        => false;

    /// <inheritdoc />
    int? IColumn.Order
        => null;

    /// <inheritdoc />
    object? IColumn.DefaultValue
        => null;

    /// <inheritdoc />
    string? IColumn.DefaultValueSql
        => null;

    /// <inheritdoc />
    string? IColumn.ComputedColumnSql
        => null;

    /// <inheritdoc />
    bool? IColumn.IsStored
        => null;

    /// <inheritdoc />
    string? IColumn.Comment
        => null;

    /// <inheritdoc />
    string? IColumn.Collation
        => null;

    /// <inheritdoc />
    ValueComparer IColumn.ProviderValueComparer
        => _providerValueComparer;

    /// <inheritdoc />
    IColumnMapping? IColumn.FindColumnMapping(IReadOnlyEntityType entityType)
        => null;
}

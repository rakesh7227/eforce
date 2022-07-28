﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore.Query;

public class JsonQuerySqlServerTest : JsonQueryTestBase<JsonQuerySqlServerFixture>
{
    public JsonQuerySqlServerTest(JsonQuerySqlServerFixture fixture, ITestOutputHelper testOutputHelper)
        : base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        //Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

    public override async Task Basic_json_projection_owner_entity(bool async)
    {
        await base.Basic_json_projection_owner_entity(async);

        AssertSql(
            @"SELECT [j].[Id], [j].[Name], JSON_QUERY([j].[OwnedCollectionRoot],'$'), JSON_QUERY([j].[OwnedReferenceRoot],'$')
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Basic_json_projection_owned_reference_root(bool async)
    {
        await base.Basic_json_projection_owned_reference_root(async);

        AssertSql(
            @"SELECT JSON_QUERY([j].[OwnedReferenceRoot],'$'), [j].[Id]
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Basic_json_projection_owned_collection_root(bool async)
    {
        await base.Basic_json_projection_owned_collection_root(async);

        AssertSql(
            @"SELECT JSON_QUERY([j].[OwnedCollectionRoot],'$'), [j].[Id]
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Basic_json_projection_owned_reference_branch(bool async)
    {
        await base.Basic_json_projection_owned_reference_branch(async);

        AssertSql(
            @"SELECT JSON_QUERY([j].[OwnedReferenceRoot],'$.OwnedReferenceBranch'), [j].[Id]
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Basic_json_projection_owned_collection_branch(bool async)
    {
        await base.Basic_json_projection_owned_collection_branch(async);

        AssertSql(
            @"SELECT JSON_QUERY([j].[OwnedReferenceRoot],'$.OwnedCollectionBranch'), [j].[Id]
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Basic_json_projection_owned_reference_leaf(bool async)
    {
        await base.Basic_json_projection_owned_reference_leaf(async);

        AssertSql(
            @"SELECT JSON_QUERY([j].[OwnedReferenceRoot],'$.OwnedReferenceBranch.OwnedReferenceLeaf'), [j].[Id]
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Basic_json_projection_owned_collection_leaf(bool async)
    {
        await base.Basic_json_projection_owned_collection_leaf(async);

        AssertSql(
            @"SELECT JSON_QUERY([j].[OwnedReferenceRoot],'$.OwnedReferenceBranch.OwnedCollectionLeaf'), [j].[Id]
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Basic_json_projection_scalar(bool async)
    {
        await base.Basic_json_projection_scalar(async);

        AssertSql(
            @"SELECT CAST(JSON_VALUE([j].[OwnedReferenceRoot],'$.Name') AS nvarchar(max))
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Basic_json_projection_enum_inside_json_entity(bool async)
    {
        await base.Basic_json_projection_enum_inside_json_entity(async);

        AssertSql(
            @"SELECT [j].[Id], CAST(JSON_VALUE([j].[OwnedReferenceRoot],'$.OwnedReferenceBranch.Enum') AS nvarchar(max)) AS [Enum]
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Json_projection_enum_with_custom_conversion(bool async)
    {
        await base.Json_projection_enum_with_custom_conversion(async);

        AssertSql(
            @"SELECT [j].[Id], CAST(JSON_VALUE([j].[json_reference_custom_naming],'$.CustomEnum') AS int) AS [Enum]
FROM [JsonEntitiesCustomNaming] AS [j]");
    }

    public override async Task Json_projection_with_deduplication(bool async)
    {
        await base.Json_projection_with_deduplication(async);

        AssertSql(
            @"SELECT [j].[Id], [j].[Name], JSON_QUERY([j].[OwnedCollectionRoot],'$'), JSON_QUERY([j].[OwnedReferenceRoot],'$'), CAST(JSON_VALUE([j].[OwnedReferenceRoot],'$.OwnedReferenceBranch.OwnedReferenceLeaf.SomethingSomething') AS nvarchar(max))
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Json_projection_with_deduplication_reverse_order(bool async)
    {
        await base.Json_projection_with_deduplication_reverse_order(async);

        AssertSql(
            @"SELECT JSON_QUERY([j].[OwnedReferenceRoot],'$'), [j].[Id], [j].[Name], JSON_QUERY([j].[OwnedCollectionRoot],'$')
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Json_property_in_predicate(bool async)
    {
        await base.Json_property_in_predicate(async);

        AssertSql(
            @"SELECT [j].[Id]
FROM [JsonEntitiesBasic] AS [j]
WHERE CAST(JSON_VALUE([j].[OwnedReferenceRoot],'$.OwnedReferenceBranch.Fraction') AS decimal(18,2)) < 20.5");
    }

    public override async Task Json_subquery_property_pushdown_length(bool async)
    {
        await base.Json_subquery_property_pushdown_length(async);

        AssertSql(
            @"@__p_0='3'

SELECT CAST(LEN([t0].[c]) AS int)
FROM (
    SELECT DISTINCT [t].[c]
    FROM (
        SELECT TOP(@__p_0) CAST(JSON_VALUE([j].[OwnedReferenceRoot],'$.OwnedReferenceBranch.OwnedReferenceLeaf.SomethingSomething') AS nvarchar(max)) AS [c]
        FROM [JsonEntitiesBasic] AS [j]
        ORDER BY [j].[Id]
    ) AS [t]
) AS [t0]");
    }

    public override async Task Json_subquery_reference_pushdown_reference(bool async)
    {
        await base.Json_subquery_reference_pushdown_reference(async);

        AssertSql(
            @"@__p_0='10'

SELECT JSON_QUERY([t0].[c],'$.OwnedReferenceBranch'), [t0].[Id]
FROM (
    SELECT DISTINCT JSON_QUERY([t].[c],'$') AS [c], [t].[Id]
    FROM (
        SELECT TOP(@__p_0) JSON_QUERY([j].[OwnedReferenceRoot],'$') AS [c], [j].[Id]
        FROM [JsonEntitiesBasic] AS [j]
        ORDER BY [j].[Id]
    ) AS [t]
) AS [t0]");
    }

    public override async Task Json_subquery_reference_pushdown_reference_anonymous_projection(bool async)
    {
        await base.Json_subquery_reference_pushdown_reference_anonymous_projection(async);

        AssertSql(
            @"@__p_0='10'

SELECT JSON_QUERY([t0].[c],'$.OwnedReferenceSharedBranch'), [t0].[Id], CAST(LEN([t0].[c0]) AS int)
FROM (
    SELECT DISTINCT JSON_QUERY([t].[c],'$') AS [c], [t].[Id], [t].[c0]
    FROM (
        SELECT TOP(@__p_0) JSON_QUERY([j].[json_reference_shared],'$') AS [c], [j].[Id], CAST(JSON_VALUE([j].[json_reference_shared],'$.OwnedReferenceSharedBranch.OwnedReferenceSharedLeaf.SomethingSomething') AS nvarchar(max)) AS [c0]
        FROM [JsonEntitiesBasic] AS [j]
        ORDER BY [j].[Id]
    ) AS [t]
) AS [t0]");
    }

    public override async Task Json_subquery_reference_pushdown_reference_pushdown_anonymous_projection(bool async)
    {
        await base.Json_subquery_reference_pushdown_reference_pushdown_anonymous_projection(async);

        AssertSql(
            @"@__p_0='10'

SELECT JSON_QUERY([t2].[c],'$.OwnedReferenceSharedLeaf'), [t2].[Id], JSON_QUERY([t2].[c],'$.OwnedCollectionSharedLeaf'), [t2].[Length]
FROM (
    SELECT DISTINCT JSON_QUERY([t1].[c],'$') AS [c], [t1].[Id], [t1].[Length]
    FROM (
        SELECT TOP(@__p_0) JSON_QUERY([t0].[c],'$.OwnedReferenceSharedBranch') AS [c], [t0].[Id], CAST(LEN([t0].[Scalar]) AS int) AS [Length]
        FROM (
            SELECT DISTINCT JSON_QUERY([t].[c],'$') AS [c], [t].[Id], [t].[Scalar]
            FROM (
                SELECT TOP(@__p_0) JSON_QUERY([j].[json_reference_shared],'$') AS [c], [j].[Id], CAST(JSON_VALUE([j].[json_reference_shared],'$.OwnedReferenceSharedBranch.OwnedReferenceSharedLeaf.SomethingSomething') AS nvarchar(max)) AS [Scalar]
                FROM [JsonEntitiesBasic] AS [j]
                ORDER BY [j].[Id]
            ) AS [t]
        ) AS [t0]
        ORDER BY CAST(LEN([t0].[Scalar]) AS int)
    ) AS [t1]
) AS [t2]");
    }

    public override async Task Json_subquery_reference_pushdown_reference_pushdown_reference(bool async)
    {
        await base.Json_subquery_reference_pushdown_reference_pushdown_reference(async);

        AssertSql(
            @"@__p_0='10'

SELECT JSON_QUERY([t2].[c],'$.OwnedReferenceLeaf'), [t2].[Id]
FROM (
    SELECT DISTINCT JSON_QUERY([t1].[c],'$') AS [c], [t1].[Id]
    FROM (
        SELECT TOP(@__p_0) JSON_QUERY([t0].[c],'$.OwnedReferenceBranch') AS [c], [t0].[Id]
        FROM (
            SELECT DISTINCT JSON_QUERY([t].[c],'$') AS [c], [t].[Id], [t].[c] AS [c0]
            FROM (
                SELECT TOP(@__p_0) JSON_QUERY([j].[OwnedReferenceRoot],'$') AS [c], [j].[Id]
                FROM [JsonEntitiesBasic] AS [j]
                ORDER BY [j].[Id]
            ) AS [t]
        ) AS [t0]
        ORDER BY CAST(JSON_VALUE([t0].[c0],'$.Name') AS nvarchar(max))
    ) AS [t1]
) AS [t2]");
    }

    public override async Task Json_subquery_reference_pushdown_reference_pushdown_collection(bool async)
    {
        await base.Json_subquery_reference_pushdown_reference_pushdown_collection(async);

        AssertSql(
            @"@__p_0='10'

SELECT JSON_QUERY([t2].[c],'$.OwnedCollectionLeaf'), [t2].[Id]
FROM (
    SELECT DISTINCT JSON_QUERY([t1].[c],'$') AS [c], [t1].[Id]
    FROM (
        SELECT TOP(@__p_0) JSON_QUERY([t0].[c],'$.OwnedReferenceBranch') AS [c], [t0].[Id]
        FROM (
            SELECT DISTINCT JSON_QUERY([t].[c],'$') AS [c], [t].[Id], [t].[c] AS [c0]
            FROM (
                SELECT TOP(@__p_0) JSON_QUERY([j].[OwnedReferenceRoot],'$') AS [c], [j].[Id]
                FROM [JsonEntitiesBasic] AS [j]
                ORDER BY [j].[Id]
            ) AS [t]
        ) AS [t0]
        ORDER BY CAST(JSON_VALUE([t0].[c0],'$.Name') AS nvarchar(max))
    ) AS [t1]
) AS [t2]");
    }

    public override async Task Json_subquery_reference_pushdown_property(bool async)
    {
        await base.Json_subquery_reference_pushdown_property(async);

        AssertSql(
            @"@__p_0='10'

SELECT CAST(JSON_VALUE([t0].[c],'$.SomethingSomething') AS nvarchar(max))
FROM (
    SELECT DISTINCT JSON_QUERY([t].[c],'$') AS [c], [t].[Id]
    FROM (
        SELECT TOP(@__p_0) JSON_QUERY([j].[OwnedReferenceRoot],'$.OwnedReferenceBranch.OwnedReferenceLeaf') AS [c], [j].[Id]
        FROM [JsonEntitiesBasic] AS [j]
        ORDER BY [j].[Id]
    ) AS [t]
) AS [t0]");
    }

    public override async Task Custom_naming_projection_owner_entity(bool async)
    {
        await base.Custom_naming_projection_owner_entity(async);

        AssertSql(
            @"SELECT [j].[Id], [j].[Title], JSON_QUERY([j].[json_collection_custom_naming],'$'), JSON_QUERY([j].[json_reference_custom_naming],'$')
FROM [JsonEntitiesCustomNaming] AS [j]");
    }

    public override async Task Custom_naming_projection_owned_reference(bool async)
    {
        await base.Custom_naming_projection_owned_reference(async);

        AssertSql(
            @"SELECT JSON_QUERY([j].[json_reference_custom_naming],'$.CustomOwnedReferenceBranch'), [j].[Id]
FROM [JsonEntitiesCustomNaming] AS [j]");
    }

    public override async Task Custom_naming_projection_owned_collection(bool async)
    {
        await base.Custom_naming_projection_owned_collection(async);

        AssertSql(
            @"SELECT JSON_QUERY([j].[json_collection_custom_naming],'$'), [j].[Id]
FROM [JsonEntitiesCustomNaming] AS [j]
ORDER BY [j].[Id]");
    }

    public override async Task Custom_naming_projection_owned_scalar(bool async)
    {
        await base.Custom_naming_projection_owned_scalar(async);

        AssertSql(
            @"SELECT CAST(JSON_VALUE([j].[json_reference_custom_naming],'$.CustomOwnedReferenceBranch.CustomFraction') AS float)
FROM [JsonEntitiesCustomNaming] AS [j]");
    }

    public override async Task Custom_naming_projection_everything(bool async)
    {
        await base.Custom_naming_projection_everything(async);

        AssertSql(
            @"SELECT [j].[Id], [j].[Title], JSON_QUERY([j].[json_collection_custom_naming],'$'), JSON_QUERY([j].[json_reference_custom_naming],'$'), CAST(JSON_VALUE([j].[json_reference_custom_naming],'$.CustomName') AS nvarchar(max)), CAST(JSON_VALUE([j].[json_reference_custom_naming],'$.CustomOwnedReferenceBranch.CustomFraction') AS float)
FROM [JsonEntitiesCustomNaming] AS [j]");
    }

    public override async Task Project_entity_with_single_owned(bool async)
    {
        await base.Project_entity_with_single_owned(async);

        AssertSql(
            @"SELECT [j].[Id], [j].[Name], JSON_QUERY([j].[OwnedCollection],'$')
FROM [JsonEntitiesSingleOwned] AS [j]");
    }

    public override async Task Left_join_json_entities(bool async)
    {
        await base.Left_join_json_entities(async);

        AssertSql(
            @"SELECT [j].[Id], [j].[Name], JSON_QUERY([j].[OwnedCollection],'$'), [j0].[Id], [j0].[Name], JSON_QUERY([j0].[OwnedCollectionRoot],'$'), JSON_QUERY([j0].[OwnedReferenceRoot],'$')
FROM [JsonEntitiesSingleOwned] AS [j]
LEFT JOIN [JsonEntitiesBasic] AS [j0] ON [j].[Id] = [j0].[Id]");
    }

    public override async Task Left_join_json_entities_complex_projection(bool async)
    {
        await base.Left_join_json_entities_complex_projection(async);

        AssertSql(
            @"SELECT [j].[Id], [j0].[Id], [j0].[Name], JSON_QUERY([j0].[OwnedCollectionRoot],'$'), JSON_QUERY([j0].[OwnedReferenceRoot],'$')
FROM [JsonEntitiesSingleOwned] AS [j]
LEFT JOIN [JsonEntitiesBasic] AS [j0] ON [j].[Id] = [j0].[Id]");
    }

    public override async Task Project_json_entity_FirstOrDefault_subquery(bool async)
    {
        await base.Project_json_entity_FirstOrDefault_subquery(async);

        AssertSql(
            @"SELECT JSON_QUERY([t].[c],'$'), [t].[Id]
FROM [JsonEntitiesBasic] AS [j]
OUTER APPLY (
    SELECT TOP(1) JSON_QUERY([j0].[OwnedReferenceRoot],'$.OwnedReferenceBranch') AS [c], [j0].[Id]
    FROM [JsonEntitiesBasic] AS [j0]
    ORDER BY [j0].[Id]
) AS [t]
ORDER BY [j].[Id]");
    }

    public override async Task Json_entity_with_inheritance_basic_projection(bool async)
    {
        await base.Json_entity_with_inheritance_basic_projection(async);

        AssertSql(
            @"SELECT [j].[Id], [j].[Discriminator], [j].[Name], [j].[Fraction], JSON_QUERY([j].[CollectionOnBase],'$'), JSON_QUERY([j].[ReferenceOnBase],'$'), JSON_QUERY([j].[CollectionOnDerived],'$'), JSON_QUERY([j].[ReferenceOnDerived],'$')
FROM [JsonEntitiesInheritance] AS [j]");
    }

    public override async Task Json_entity_with_inheritance_project_derived(bool async)
    {
        await base.Json_entity_with_inheritance_project_derived(async);

        AssertSql(
            @"SELECT [j].[Id], [j].[Discriminator], [j].[Name], [j].[Fraction], JSON_QUERY([j].[CollectionOnBase],'$'), JSON_QUERY([j].[ReferenceOnBase],'$'), JSON_QUERY([j].[CollectionOnDerived],'$'), JSON_QUERY([j].[ReferenceOnDerived],'$')
FROM [JsonEntitiesInheritance] AS [j]
WHERE [j].[Discriminator] = N'JsonEntityInheritanceDerived'");
    }

    public override async Task Json_entity_with_inheritance_project_navigations(bool async)
    {
        await base.Json_entity_with_inheritance_project_navigations(async);

        AssertSql(
            @"SELECT [j].[Id], JSON_QUERY([j].[ReferenceOnBase],'$'), JSON_QUERY([j].[CollectionOnBase],'$')
FROM [JsonEntitiesInheritance] AS [j]");
    }

    public override async Task Json_entity_with_inheritance_project_navigations_on_derived(bool async)
    {
        await base.Json_entity_with_inheritance_project_navigations_on_derived(async);

        AssertSql(
            @"SELECT [j].[Id], [j].[Discriminator], [j].[Name], [j].[Fraction], JSON_QUERY([j].[CollectionOnBase],'$'), JSON_QUERY([j].[ReferenceOnBase],'$'), JSON_QUERY([j].[CollectionOnDerived],'$'), JSON_QUERY([j].[ReferenceOnDerived],'$')
FROM [JsonEntitiesInheritance] AS [j]
WHERE [j].[Discriminator] = N'JsonEntityInheritanceDerived'");
    }

    private void AssertSql(params string[] expected)
        => Fixture.TestSqlLoggerFactory.AssertBaseline(expected);
}

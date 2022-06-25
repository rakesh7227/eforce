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
            @"SELECT [j].[Id], [j].[Name], JSON_QUERY([j].[json_collection_shared],'$'), JSON_QUERY([j].[json_reference_shared],'$')
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Basic_json_projection_owned_reference_root(bool async)
    {
        await base.Basic_json_projection_owned_reference_root(async);

        AssertSql(
            @"SELECT JSON_QUERY([j].[json_reference_shared],'$'), [j].[Id]
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Basic_json_projection_owned_collection_root(bool async)
    {
        await base.Basic_json_projection_owned_collection_root(async);

        AssertSql(
            @"SELECT JSON_QUERY([j].[json_collection_shared],'$'), [j].[Id]
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Basic_json_projection_owned_reference_branch(bool async)
    {
        await base.Basic_json_projection_owned_reference_branch(async);

        AssertSql(
            @"SELECT JSON_QUERY([j].[json_reference_shared],'$.OwnedReferenceSharedBranch'), [j].[Id]
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Basic_json_projection_owned_collection_branch(bool async)
    {
        await base.Basic_json_projection_owned_collection_branch(async);

        AssertSql(
            @"SELECT JSON_QUERY([j].[json_reference_shared],'$.OwnedCollectionSharedBranch'), [j].[Id]
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Basic_json_projection_owned_reference_leaf(bool async)
    {
        await base.Basic_json_projection_owned_reference_leaf(async);

        AssertSql(
            @"SELECT JSON_QUERY([j].[json_reference_shared],'$.OwnedReferenceSharedBranch.OwnedReferenceSharedLeaf'), [j].[Id]
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Basic_json_projection_owned_collection_leaf(bool async)
    {
        await base.Basic_json_projection_owned_collection_leaf(async);

        AssertSql(
            @"SELECT JSON_QUERY([j].[json_reference_shared],'$.OwnedReferenceSharedBranch.OwnedCollectionSharedLeaf'), [j].[Id]
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Basic_json_projection_scalar(bool async)
    {
        await base.Basic_json_projection_scalar(async);

        AssertSql(
            @"SELECT CAST(JSON_VALUE([j].[json_reference_shared],'$.Name') AS nvarchar(max))
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Json_projection_with_deduplication(bool async)
    {
        await base.Json_projection_with_deduplication(async);

        AssertSql(
            @"SELECT JSON_QUERY([j].[json_reference_shared],'$.OwnedReferenceSharedBranch'), [j].[Id], JSON_QUERY([j].[json_reference_shared],'$.OwnedCollectionSharedBranch'), CAST(JSON_VALUE([j].[json_reference_shared],'$.OwnedReferenceSharedBranch.OwnedReferenceSharedLeaf.SomethingSomething') AS nvarchar(max))
FROM [JsonEntitiesBasic] AS [j]");
    }

    public override async Task Json_projection_with_deduplication_reverse_order(bool async)
    {
        await base.Json_projection_with_deduplication_reverse_order(async);

        AssertSql(
            @"");
    }

    public override async Task Json_property_in_predicate(bool async)
    {
        await base.Json_property_in_predicate(async);

        AssertSql(
            @"SELECT [j].[Id]
FROM [JsonEntitiesBasic] AS [j]
WHERE CAST(JSON_VALUE([j].[json_reference_shared],'$.OwnedReferenceSharedBranch.Fraction') AS decimal(18,2)) < 20.5");
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
        SELECT TOP(@__p_0) CAST(JSON_VALUE([j].[json_reference_shared],'$.OwnedReferenceSharedBranch.OwnedReferenceSharedLeaf.SomethingSomething') AS nvarchar(max)) AS [c]
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

SELECT JSON_QUERY([t0].[c],'$.OwnedReferenceSharedBranch'), [t0].[Id]
FROM (
    SELECT DISTINCT JSON_QUERY([t].[c],'$') AS [c], [t].[Id]
    FROM (
        SELECT TOP(@__p_0) JSON_QUERY([j].[json_reference_shared],'$') AS [c], [j].[Id]
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

SELECT JSON_QUERY([t2].[c],'$.OwnedReferenceSharedLeaf'), [t2].[Id], JSON_QUERY([t2].[c],'$.OwnedCollectionSharedLeaf'), [t2].[c0]
FROM (
    SELECT DISTINCT JSON_QUERY([t1].[c],'$') AS [c], [t1].[Id], [t1].[c0]
    FROM (
        SELECT TOP(@__p_0) JSON_QUERY([t0].[c],'$.OwnedReferenceSharedBranch') AS [c], [t0].[Id], CAST(LEN([t0].[c0]) AS int) AS [c0]
        FROM (
            SELECT DISTINCT JSON_QUERY([t].[c],'$') AS [c], [t].[Id], [t].[c0]
            FROM (
                SELECT TOP(@__p_0) JSON_QUERY([j].[json_reference_shared],'$') AS [c], [j].[Id], CAST(JSON_VALUE([j].[json_reference_shared],'$.OwnedReferenceSharedBranch.OwnedReferenceSharedLeaf.SomethingSomething') AS nvarchar(max)) AS [c0]
                FROM [JsonEntitiesBasic] AS [j]
                ORDER BY [j].[Id]
            ) AS [t]
        ) AS [t0]
        ORDER BY CAST(LEN([t0].[c0]) AS int)
    ) AS [t1]
) AS [t2]");
    }

    public override async Task Json_subquery_reference_pushdown_reference_pushdown_reference(bool async)
    {
        await base.Json_subquery_reference_pushdown_reference_pushdown_reference(async);

        AssertSql(
            @"@__p_0='10'

SELECT JSON_QUERY([t2].[c],'$.OwnedReferenceSharedLeaf'), [t2].[Id]
FROM (
    SELECT DISTINCT JSON_QUERY([t1].[c],'$') AS [c], [t1].[Id]
    FROM (
        SELECT TOP(@__p_0) JSON_QUERY([t0].[c],'$.OwnedReferenceSharedBranch') AS [c], [t0].[Id]
        FROM (
            SELECT DISTINCT JSON_QUERY([t].[c],'$') AS [c], [t].[Id], [t].[c] AS [c0]
            FROM (
                SELECT TOP(@__p_0) JSON_QUERY([j].[json_reference_shared],'$') AS [c], [j].[Id]
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

SELECT JSON_QUERY([t2].[c],'$.OwnedCollectionSharedLeaf'), [t2].[Id]
FROM (
    SELECT DISTINCT JSON_QUERY([t1].[c],'$') AS [c], [t1].[Id]
    FROM (
        SELECT TOP(@__p_0) JSON_QUERY([t0].[c],'$.OwnedReferenceSharedBranch') AS [c], [t0].[Id]
        FROM (
            SELECT DISTINCT JSON_QUERY([t].[c],'$') AS [c], [t].[Id], [t].[c] AS [c0]
            FROM (
                SELECT TOP(@__p_0) JSON_QUERY([j].[json_reference_shared],'$') AS [c], [j].[Id]
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
            @"");
    }

    private void AssertSql(params string[] expected)
        => Fixture.TestSqlLoggerFactory.AssertBaseline(expected);
}
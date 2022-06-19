﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.EntityFrameworkCore.TestModels.JsonQuery;

namespace Microsoft.EntityFrameworkCore.Query;

public abstract class JsonQueryTestBase<TFixture> : QueryTestBase<TFixture>
    where TFixture : JsonQueryFixtureBase, new()
{
    protected JsonQueryTestBase(TFixture fixture)
        : base(fixture)
    {
    }

    [ConditionalTheory]
    [MemberData(nameof(IsAsyncData))]
    public virtual Task Basic_json_projection_owner_entity(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<JsonBasicEntity>(),
            entryCount: 40);

    [ConditionalTheory]
    [MemberData(nameof(IsAsyncData))]
    public virtual Task Basic_json_projection_owned_reference_root(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<JsonBasicEntity>().Select(x => x.OwnedReferenceSharedRoot).AsNoTracking());

    [ConditionalTheory]
    [MemberData(nameof(IsAsyncData))]
    public virtual Task Basic_json_projection_owned_collection_root(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<JsonBasicEntity>().Select(x => x.OwnedCollectionSharedRoot).AsNoTracking(),
            elementAsserter: (e, a) => AssertCollection(e, a, ordered: true));

    [ConditionalTheory]
    [MemberData(nameof(IsAsyncData))]
    public virtual Task Basic_json_projection_owned_reference_branch(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<JsonBasicEntity>().Select(x => x.OwnedReferenceSharedRoot.OwnedReferenceSharedBranch).AsNoTracking());

    [ConditionalTheory]
    [MemberData(nameof(IsAsyncData))]
    public virtual Task Basic_json_projection_owned_collection_branch(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<JsonBasicEntity>().Select(x => x.OwnedReferenceSharedRoot.OwnedCollectionSharedBranch).AsNoTracking(),
            elementAsserter: (e, a) => AssertCollection(e, a, ordered: true));

    [ConditionalTheory]
    [MemberData(nameof(IsAsyncData))]
    public virtual Task Basic_json_projection_owned_reference_leaf(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<JsonBasicEntity>().Select(x => x.OwnedReferenceSharedRoot.OwnedReferenceSharedBranch.OwnedReferenceSharedLeaf).AsNoTracking());

    [ConditionalTheory]
    [MemberData(nameof(IsAsyncData))]
    public virtual Task Basic_json_projection_owned_collection_leaf(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<JsonBasicEntity>().Select(x => x.OwnedReferenceSharedRoot.OwnedReferenceSharedBranch.OwnedCollectionSharedLeaf).AsNoTracking(),
            elementAsserter: (e, a) => AssertCollection(e, a, ordered: true));

    [ConditionalTheory]
    [MemberData(nameof(IsAsyncData))]
    public virtual Task Basic_json_projection_scalar(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<JsonBasicEntity>().Select(x => x.OwnedReferenceSharedRoot.Name));

    [ConditionalTheory]
    [MemberData(nameof(IsAsyncData))]
    public virtual Task Json_projection_with_deduplication(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<JsonBasicEntity>().Select(x => new
            {
                x.OwnedReferenceSharedRoot.OwnedReferenceSharedBranch,
                x.OwnedReferenceSharedRoot.OwnedReferenceSharedBranch.OwnedReferenceSharedLeaf,
                x.OwnedReferenceSharedRoot.OwnedCollectionSharedBranch,
                x.OwnedReferenceSharedRoot.OwnedReferenceSharedBranch.OwnedReferenceSharedLeaf.SomethingSomething
            }).AsNoTracking(),

            elementAsserter: (e, a) =>
            {
                AssertEqual(e.OwnedReferenceSharedBranch, a.OwnedReferenceSharedBranch);
                AssertEqual(e.OwnedReferenceSharedLeaf, a.OwnedReferenceSharedLeaf);
                AssertCollection(e.OwnedCollectionSharedBranch, a.OwnedCollectionSharedBranch, ordered: true);
                Assert.Equal(e.SomethingSomething, a.SomethingSomething);
            });
}

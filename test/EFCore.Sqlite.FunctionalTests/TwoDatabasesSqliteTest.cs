// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore;

public class TwoDatabasesSqliteTest : TwoDatabasesTestBase, IClassFixture<TwoDatabasesSqliteTest.TwoDatabasesFixture>
{
    public TwoDatabasesSqliteTest(TwoDatabasesFixture fixture)
        : base(fixture)
    {
    }

    protected new TwoDatabasesFixture Fixture
        => (TwoDatabasesFixture)base.Fixture;

    protected override DbContextOptionsBuilder CreateTestOptions(
        DbContextOptionsBuilder optionsBuilder,
        bool withConnectionString = false,
        bool withNullConnectionString = false)
        => withConnectionString
            ? withNullConnectionString
                ? optionsBuilder.UseSqlite((string)null)
                : optionsBuilder.UseSqlite(DummyConnectionString)
            : optionsBuilder.UseSqlite();

    protected override TwoDatabasesWithDataContext CreateBackingContext(string databaseName)
        => new(Fixture.CreateOptions(SqliteTestStore.Create(databaseName)));

    protected override string DummyConnectionString
        => "DataSource=DummyDatabase";

    public class TwoDatabasesFixture : ServiceProviderFixtureBase
    {
        protected override ITestStoreFactory TestStoreFactory
            => SqliteTestStoreFactory.Instance;
    }
}

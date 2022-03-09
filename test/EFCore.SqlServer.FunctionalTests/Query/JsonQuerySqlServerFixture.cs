// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.EntityFrameworkCore.TestModels.JsonQuery;

namespace Microsoft.EntityFrameworkCore.Query;

public class JsonQuerySqlServerFixture : JsonQueryFixtureBase
{
    protected override ITestStoreFactory TestStoreFactory
        => SqlServerTestStoreFactory.Instance;

    protected override void Seed(JsonQueryContext context)
    {
        base.Seed(context);

     //   var jsonEntitiesBasic = JsonQueryData.CreateJsonEntitiesBasic();
     //   foreach (var jsonEntityBasic in jsonEntitiesBasic)
     //   {
     //       var jsonReference = JsonSerializer.Serialize(jsonEntityBasic.OwnedReferenceRoot);
     //       var jsonCollection = JsonSerializer.Serialize(jsonEntityBasic.OwnedCollectionRoot);

     //       var sql = $@"INSERT INTO [dbo].[JsonEntitiesBasic]
     //      ([Name]
     //      ,[OwnedCollectionRoot]
     //      ,[OwnedReferenceRoot])
     //VALUES
     //      ('{jsonEntityBasic.Name}'
     //      ,'{jsonCollection}'
     //      ,'{jsonReference}')";

     //       context.Database.ExecuteSqlRaw(sql
     //           .Replace("{", "{{")
     //           .Replace("}", "}}")
     //           .Replace(@"""Enum"":0", @"""Enum"":""One""")
     //           .Replace(@"""Enum"":1", @"""Enum"":""Two""")
     //           .Replace(@"""Enum"":2", @"""Enum"":""Three"""));
     //   }

//        var jsonEntitiesCustomNaming = JsonQueryData.CreateJsonEntitiesCustomNaming();
//        foreach (var jsonEntityCustomNaming in jsonEntitiesCustomNaming)
//        {
//            // reference has explicit conversion defined to store enum as int
//            var jsonReference = JsonSerializer.Serialize(jsonEntityCustomNaming.OwnedReferenceRoot);
//            var jsonCollection = JsonSerializer.Serialize(jsonEntityCustomNaming.OwnedCollectionRoot)
//                .Replace(@"""CustomEnum"":0", @"""CustomEnum"":""One""")
//                .Replace(@"""CustomEnum"":1", @"""CustomEnum"":""Two""")
//                .Replace(@"""CustomEnum"":2", @"""CustomEnum"":""Three""");

//            var sql = $@"INSERT INTO [dbo].[JsonEntitiesCustomNaming]
//           ([Title]
//           ,[json_collection_custom_naming]
//           ,[json_reference_custom_naming])
//     VALUES
//           ('{jsonEntityCustomNaming.Title}'
//           ,'{AddCustomNaming(jsonCollection)}'
//           ,'{AddCustomNaming(jsonReference)}')";

//            context.Database.ExecuteSqlRaw(sql.Replace("{", "{{").Replace("}", "}}"));
//        }

//        var jsonEntitiesSingleOwned = JsonQueryData.CreateJsonEntitiesSingleOwned();
//        foreach (var jsonEntitySingleOwned in jsonEntitiesSingleOwned)
//        {
//            var jsonCollection = JsonSerializer.Serialize(jsonEntitySingleOwned.OwnedCollection);

//            var sql = $@"INSERT INTO [dbo].[JsonEntitiesSingleOwned]
//           ([Name]
//           ,[OwnedCollection])
//     VALUES
//           ('{jsonEntitySingleOwned.Name}'
//           ,'{AddCustomNaming(jsonCollection)}')";

//            context.Database.ExecuteSqlRaw(sql.Replace("{", "{{").Replace("}", "}}"));
//        }

//        var jsonEntitiesInheritance = JsonQueryData.CreateJsonEntitiesInheritance();
//        foreach (var jsonEntityInheritance in jsonEntitiesInheritance)
//        {
//            var jsonReferenceOnBase = JsonSerializer.Serialize(jsonEntityInheritance.ReferenceOnBase);
//            var jsonCollectionOnBase = JsonSerializer.Serialize(jsonEntityInheritance.CollectionOnBase);

//            var values = $@"
//           ('{jsonEntityInheritance.Name}'
//           ,NULL
//           ,'JsonEntityInheritanceBase'
//           ,'{jsonReferenceOnBase}'
//           ,'{jsonCollectionOnBase}'
//           ,NULL
//           ,NULL)";

//            if (jsonEntityInheritance is JsonEntityInheritanceDerived derived)
//            {
//                var jsonReferenceOnDerived = JsonSerializer.Serialize(derived.ReferenceOnDerived);
//                var jsonCollectionOnDerived = JsonSerializer.Serialize(derived.CollectionOnDerived);

//                values = $@"
//    ('{jsonEntityInheritance.Name}'
//    ,{derived.Fraction}
//    ,'JsonEntityInheritanceDerived'
//    ,'{jsonReferenceOnBase}'
//    ,'{jsonCollectionOnBase}'
//    ,'{jsonReferenceOnDerived}'
//    ,'{jsonCollectionOnDerived}')";
//            }

//            var sql = $@"INSERT INTO [dbo].[JsonEntityInheritanceBase]
//    ([Name]
//    ,[Fraction]
//    ,[Discriminator]
//    ,[ReferenceOnBase]
//    ,[CollectionOnBase]
//    ,[ReferenceOnDerived]
//    ,[CollectionOnDerived])
//VALUES" + values;

//            context.Database.ExecuteSqlRaw(sql
//                .Replace("{", "{{")
//                .Replace("}", "}}")
//                .Replace(@"""Enum"":0", @"""Enum"":""One""")
//                .Replace(@"""Enum"":1", @"""Enum"":""Two""")
//                .Replace(@"""Enum"":2", @"""Enum"":""Three"""));
//        }

//        static string AddCustomNaming(string json)
//            => json;
    }
}

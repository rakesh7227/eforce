// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Data;

namespace Microsoft.EntityFrameworkCore.Storage
{
    /// <summary>
    /// TODO
    /// </summary>
    public abstract class JsonTypeMapping : RelationalTypeMapping
    {
        /// <summary>
        /// TODO
        /// </summary>
        protected JsonTypeMapping(string storeType, Type clrType, DbType? dbType)
            : base(storeType, clrType, dbType)
        {
        }

        /// <summary>
        /// TODO
        /// </summary>
        protected JsonTypeMapping(RelationalTypeMappingParameters parameters)
            : base(parameters)
        {
        }

        /// <inheritdoc />
        protected override string GenerateNonNullSqlLiteral(object value)
            => throw new InvalidOperationException("This method needs to be implemented in the provider.");
    }
}

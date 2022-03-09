// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    /// <summary>
    /// TODO
    /// </summary>
    public class JsonScalarExpression : SqlExpression
    {
        /// <summary>
        /// TODO
        /// </summary>
        public JsonScalarExpression(
            ColumnExpression jsonColumn,
            Type type,
            RelationalTypeMapping typeMapping,
            IReadOnlyList<SqlExpression> jsonPath)
            : base(type, typeMapping)
        {
            JsonColumn = jsonColumn;
            JsonPath = jsonPath;
        }

        /// <summary>
        /// TODO
        /// </summary>
        public virtual ColumnExpression JsonColumn { get; }

        /// <summary>
        /// TODO
        /// </summary>
        public virtual IReadOnlyList<SqlExpression> JsonPath { get; }

        /// <inheritdoc />
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var jsonColumn = (ColumnExpression)visitor.Visit(JsonColumn);

            return jsonColumn != JsonColumn
                ? new JsonScalarExpression(jsonColumn, Type, TypeMapping!, JsonPath.ToList())
                : this;
        }

        /// <inheritdoc />
        protected override void Print(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.Append("JsonScalarExpression(column: ");
            expressionPrinter.Visit(JsonColumn);
            expressionPrinter.Append("  Path: " + string.Join(".", JsonPath) + ")");
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
            => obj is JsonScalarExpression jsonScalarExpression
            ? JsonColumn.Equals(jsonScalarExpression.JsonColumn)
                && JsonPath.SequenceEqual(jsonScalarExpression.JsonPath)
            : false;

        /// <inheritdoc />
        public override int GetHashCode()
            => HashCode.Combine(base.GetHashCode(), JsonColumn, JsonPath);
    }
}

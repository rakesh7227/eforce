﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.EntityFrameworkCore.Query.SqlExpressions
{
    /// <summary>
    /// TODO
    /// </summary>
    public class JsonPathExpression : SqlExpression
    {
//        private readonly List<string> _jsonPath;

        /// <summary>
        /// TODO
        /// </summary>
        public virtual ColumnExpression JsonColumn { get; }

        /// <summary>
        /// TODO
        /// </summary>
        public virtual IReadOnlyList<string> JsonPath { get; }

        /// <summary>
        /// TODO
        /// </summary>
        public virtual IReadOnlyDictionary<IProperty, ColumnExpression> KeyPropertyMap { get; } // TODO: this should be sorted!

        /// <summary>
        /// TODO
        /// </summary>
        public JsonPathExpression(
            ColumnExpression jsonColumn,
            Type type,
            RelationalTypeMapping? typeMapping,
            IReadOnlyDictionary<IProperty, ColumnExpression> keyPropertyMap)
            : this(jsonColumn, type, typeMapping, keyPropertyMap, new List<string>())
        {
        }

        /// <summary>
        /// TODO
        /// </summary>
        public JsonPathExpression(
            ColumnExpression jsonColumn,
            Type type,
            RelationalTypeMapping? typeMapping,
            IReadOnlyDictionary<IProperty, ColumnExpression> keyPropertyMap,
            List<string> jsonPath)
            : base(type, typeMapping)
        {
            JsonColumn = jsonColumn;
            KeyPropertyMap = keyPropertyMap;
            JsonPath = jsonPath.AsReadOnly();
        }

        /// <inheritdoc />
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var jsonColumn = (ColumnExpression)visitor.Visit(JsonColumn);
            var keyPropertyMapChanged = false;

            var newKeyPropertyMap = new Dictionary<IProperty, ColumnExpression>();
            foreach (var keyPropertyMapElement in KeyPropertyMap)
            {
                var newColumn = (ColumnExpression)visitor.Visit(keyPropertyMapElement.Value);
                if (newColumn != keyPropertyMapElement.Value)
                {
                    newKeyPropertyMap[keyPropertyMapElement.Key] = newColumn;
                    keyPropertyMapChanged = true;
                }
                else
                {
                    newKeyPropertyMap[keyPropertyMapElement.Key] = keyPropertyMapElement.Value;
                }
            }

            return jsonColumn != JsonColumn || keyPropertyMapChanged
                ? new JsonPathExpression(jsonColumn, Type, TypeMapping, newKeyPropertyMap)
                : this;
        }

        /// <summary>
        /// TODO
        /// </summary>
        protected override void Print(ExpressionPrinter expressionPrinter)
        {
            expressionPrinter.Append("SqlPathExpression(column: " + JsonColumn.Name + "  Path: " + string.Join(".", JsonPath) + ")");
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (obj is JsonPathExpression jsonPathExpression)
            {
                var result = true;
                result = result && JsonColumn.Equals(jsonPathExpression.JsonColumn);
                result = result && JsonPath.Count == jsonPathExpression.JsonPath.Count;

                if (result)
                {
                    result = result && JsonPath.Zip(jsonPathExpression.JsonPath, (l, r) => l == r).All(x => true);
                }

                return result;
            }
            else
            {
                return false;
            }
        }

        /// <inheritdoc />
        public override int GetHashCode()
            => HashCode.Combine(base.GetHashCode(), JsonColumn, JsonPath);
    }
}

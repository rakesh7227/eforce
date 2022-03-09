﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Microsoft.EntityFrameworkCore.Query;

public partial class RelationalShapedQueryCompilingExpressionVisitor
{
    private sealed class ShaperProcessingExpressionVisitor : ExpressionVisitor
    {
        // Reading database values
        private static readonly MethodInfo IsDbNullMethod =
            typeof(DbDataReader).GetRuntimeMethod(nameof(DbDataReader.IsDBNull), new[] { typeof(int) })!;

        public static readonly MethodInfo GetFieldValueMethod =
            typeof(DbDataReader).GetRuntimeMethod(nameof(DbDataReader.GetFieldValue), new[] { typeof(int) })!;

        private static readonly MethodInfo ThrowReadValueExceptionMethod =
            typeof(ShaperProcessingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(ThrowReadValueException))!;

        // Coordinating results
        private static readonly MemberInfo ResultContextValuesMemberInfo
            = typeof(ResultContext).GetMember(nameof(ResultContext.Values))[0];

        private static readonly MemberInfo SingleQueryResultCoordinatorResultReadyMemberInfo
            = typeof(SingleQueryResultCoordinator).GetMember(nameof(SingleQueryResultCoordinator.ResultReady))[0];

        // Performing collection materialization
        private static readonly MethodInfo IncludeReferenceMethodInfo
            = typeof(ShaperProcessingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(IncludeReference))!;

        private static readonly MethodInfo InitializeIncludeCollectionMethodInfo
            = typeof(ShaperProcessingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(InitializeIncludeCollection))!;

        private static readonly MethodInfo PopulateIncludeCollectionMethodInfo
            = typeof(ShaperProcessingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(PopulateIncludeCollection))!;

        private static readonly MethodInfo InitializeSplitIncludeCollectionMethodInfo
            = typeof(ShaperProcessingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(InitializeSplitIncludeCollection))!;

        private static readonly MethodInfo PopulateSplitIncludeCollectionMethodInfo
            = typeof(ShaperProcessingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(PopulateSplitIncludeCollection))!;

        private static readonly MethodInfo PopulateSplitIncludeCollectionAsyncMethodInfo
            = typeof(ShaperProcessingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(PopulateSplitIncludeCollectionAsync))!;

        private static readonly MethodInfo InitializeCollectionMethodInfo
            = typeof(ShaperProcessingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(InitializeCollection))!;

        private static readonly MethodInfo PopulateCollectionMethodInfo
            = typeof(ShaperProcessingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(PopulateCollection))!;

        private static readonly MethodInfo InitializeSplitCollectionMethodInfo
            = typeof(ShaperProcessingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(InitializeSplitCollection))!;

        private static readonly MethodInfo PopulateSplitCollectionMethodInfo
            = typeof(ShaperProcessingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(PopulateSplitCollection))!;

        private static readonly MethodInfo PopulateSplitCollectionAsyncMethodInfo
            = typeof(ShaperProcessingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(PopulateSplitCollectionAsync))!;

        private static readonly MethodInfo TaskAwaiterMethodInfo
            = typeof(ShaperProcessingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(TaskAwaiter))!;

        private static readonly MethodInfo IncludeJsonEntityReferenceMethodInfo
            = typeof(ShaperProcessingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(IncludeJsonEntityReference))!;

        private static readonly MethodInfo IncludeJsonEntityCollectionMethodInfo
            = typeof(ShaperProcessingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(IncludeJsonEntityCollection))!;

        private static readonly MethodInfo MaterializeJsonEntityMethodInfo
            = typeof(ShaperProcessingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(MaterializeJsonEntity))!;

        private static readonly MethodInfo MaterializeJsonEntityCollectionMethodInfo
            = typeof(ShaperProcessingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(MaterializeJsonEntityCollection))!;

        private static readonly MethodInfo CollectionAccessorAddMethodInfo
            = typeof(IClrCollectionAccessor).GetTypeInfo().GetDeclaredMethod(nameof(IClrCollectionAccessor.Add))!;

        private static readonly MethodInfo ExtractJsonPropertyMethodInfo
            = typeof(ShaperProcessingExpressionVisitor).GetTypeInfo().GetDeclaredMethod(nameof(ExtractJsonProperty))!;

        private static readonly MethodInfo JsonElementGetPropertyMethod
            = typeof(JsonElement).GetMethod(nameof(JsonElement.GetProperty), new[] { typeof(string) })!;

        private static readonly PropertyInfo _objectArrayIndexerPropertyInfo
            = typeof(object[]).GetProperty("Item")!;

        private static readonly PropertyInfo _nullableJsonElementHasValuePropertyInfo
            = typeof(JsonElement?).GetProperty(nameof(Nullable<JsonElement>.HasValue))!;

        private static readonly PropertyInfo _nullableJsonElementValuePropertyInfo
            = typeof(JsonElement?).GetProperty(nameof(Nullable<JsonElement>.Value))!;

        private readonly RelationalShapedQueryCompilingExpressionVisitor _parentVisitor;
        private readonly ISet<string>? _tags;
        private readonly bool _isTracking;
        private readonly bool _isAsync;
        private readonly bool _splitQuery;
        private readonly bool _detailedErrorsEnabled;
        private readonly bool _generateCommandCache;
        private readonly ParameterExpression _resultCoordinatorParameter;
        private readonly ParameterExpression? _executionStrategyParameter;

        // States scoped to SelectExpression
        private readonly SelectExpression _selectExpression;
        private readonly ParameterExpression _dataReaderParameter;
        private readonly ParameterExpression _resultContextParameter;
        private readonly ParameterExpression? _indexMapParameter;
        private readonly ReaderColumn?[]? _readerColumns;

        // States to materialize only once
        private readonly Dictionary<Expression, Expression> _variableShaperMapping = new(ReferenceEqualityComparer.Instance);

        // There are always entity variables to avoid materializing same entity twice
        private readonly List<ParameterExpression> _variables = new();

        private readonly List<Expression> _expressions = new();

        // IncludeExpressions are added at the end in case they are using ValuesArray
        private readonly List<Expression> _includeExpressions = new();

        // If there is collection shaper then we need to construct ValuesArray to store values temporarily in ResultContext
        private List<Expression>? _collectionPopulatingExpressions;
        private Expression? _valuesArrayExpression;
        private List<Expression>? _valuesArrayInitializers;

        private bool _containsCollectionMaterialization;

        // Since identifiers for collection are not part of larger lambda they don't cannot use caching to materialize only once.
        private bool _inline;
        private int _collectionId;

        // States to convert code to data reader read
        private readonly Dictionary<ParameterExpression, IDictionary<IProperty, int>> _materializationContextBindings = new();
        private readonly Dictionary<ParameterExpression, object> _entityTypeIdentifyingExpressionInfo = new();
        private readonly Dictionary<ProjectionBindingExpression, string> _singleEntityTypeDiscriminatorValues = new();
        private readonly Dictionary<ParameterExpression, (ParameterExpression, ParameterExpression)> _jsonValueBufferParameterMapping = new();
        private readonly Dictionary<ParameterExpression, (ParameterExpression, ParameterExpression)> _jsonMaterializationContextParameterMapping = new();
        private readonly Dictionary<(int, string[]), ParameterExpression> _existingJsonElementMap
            = new(new ExisitingJsonElementMapKeyComparer());

        public ShaperProcessingExpressionVisitor(
            RelationalShapedQueryCompilingExpressionVisitor parentVisitor,
            SelectExpression selectExpression,
            ISet<string> tags,
            bool splitQuery,
            bool indexMap)
        {
            _parentVisitor = parentVisitor;
            _resultCoordinatorParameter = Expression.Parameter(
                splitQuery ? typeof(SplitQueryResultCoordinator) : typeof(SingleQueryResultCoordinator), "resultCoordinator");
            _executionStrategyParameter = splitQuery ? Expression.Parameter(typeof(IExecutionStrategy), "executionStrategy") : null;

            _selectExpression = selectExpression;
            _tags = tags;
            _dataReaderParameter = Expression.Parameter(typeof(DbDataReader), "dataReader");
            _resultContextParameter = Expression.Parameter(typeof(ResultContext), "resultContext");
            _indexMapParameter = indexMap ? Expression.Parameter(typeof(int[]), "indexMap") : null;
            if (parentVisitor.QueryCompilationContext.IsBuffering)
            {
                _readerColumns = new ReaderColumn?[_selectExpression.Projection.Count];
            }

            _generateCommandCache = true;
            _detailedErrorsEnabled = parentVisitor._detailedErrorsEnabled;
            _isTracking = parentVisitor.QueryCompilationContext.QueryTrackingBehavior == QueryTrackingBehavior.TrackAll;
            _isAsync = parentVisitor.QueryCompilationContext.IsAsync;
            _splitQuery = splitQuery;

            _selectExpression.ApplyTags(_tags);
        }

        // For single query scenario
        private ShaperProcessingExpressionVisitor(
            RelationalShapedQueryCompilingExpressionVisitor parentVisitor,
            ParameterExpression resultCoordinatorParameter,
            SelectExpression selectExpression,
            ParameterExpression dataReaderParameter,
            ParameterExpression resultContextParameter,
            ReaderColumn?[]? readerColumns)
        {
            _parentVisitor = parentVisitor;
            _resultCoordinatorParameter = resultCoordinatorParameter;

            _selectExpression = selectExpression;
            _dataReaderParameter = dataReaderParameter;
            _resultContextParameter = resultContextParameter;
            _readerColumns = readerColumns;
            _generateCommandCache = false;
            _detailedErrorsEnabled = parentVisitor._detailedErrorsEnabled;
            _isTracking = parentVisitor.QueryCompilationContext.QueryTrackingBehavior == QueryTrackingBehavior.TrackAll;
            _isAsync = parentVisitor.QueryCompilationContext.IsAsync;
            _splitQuery = false;
        }

        // For split query scenario
        private ShaperProcessingExpressionVisitor(
            RelationalShapedQueryCompilingExpressionVisitor parentVisitor,
            ParameterExpression resultCoordinatorParameter,
            ParameterExpression executionStrategyParameter,
            SelectExpression selectExpression,
            ISet<string> tags)
        {
            _parentVisitor = parentVisitor;
            _resultCoordinatorParameter = resultCoordinatorParameter;
            _executionStrategyParameter = executionStrategyParameter;

            _selectExpression = selectExpression;
            _tags = tags;
            _dataReaderParameter = Expression.Parameter(typeof(DbDataReader), "dataReader");
            _resultContextParameter = Expression.Parameter(typeof(ResultContext), "resultContext");
            if (parentVisitor.QueryCompilationContext.IsBuffering)
            {
                _readerColumns = new ReaderColumn[_selectExpression.Projection.Count];
            }

            _generateCommandCache = true;
            _detailedErrorsEnabled = parentVisitor._detailedErrorsEnabled;
            _isTracking = parentVisitor.QueryCompilationContext.QueryTrackingBehavior == QueryTrackingBehavior.TrackAll;
            _isAsync = parentVisitor.QueryCompilationContext.IsAsync;
            _splitQuery = true;

            _selectExpression.ApplyTags(_tags);
        }

        public LambdaExpression ProcessShaper(
            Expression shaperExpression,
            out RelationalCommandCache? relationalCommandCache,
            out IReadOnlyList<ReaderColumn?>? readerColumns,
            out LambdaExpression? relatedDataLoaders,
            ref int collectionId)
        {
            relatedDataLoaders = null;
            _collectionId = collectionId;

            if (_indexMapParameter != null)
            {
                var result = Visit(shaperExpression);
                _expressions.Add(result);
                result = Expression.Block(_variables, _expressions);

                relationalCommandCache = new RelationalCommandCache(
                    _parentVisitor.Dependencies.MemoryCache,
                    _parentVisitor.RelationalDependencies.QuerySqlGeneratorFactory,
                    _parentVisitor.RelationalDependencies.RelationalParameterBasedSqlProcessorFactory,
                    _selectExpression,
                    _parentVisitor._useRelationalNulls);
                readerColumns = _readerColumns;

                return Expression.Lambda(
                    result,
                    QueryCompilationContext.QueryContextParameter,
                    _dataReaderParameter,
                    _indexMapParameter);
            }

            _containsCollectionMaterialization = new CollectionShaperFindingExpressionVisitor()
                .ContainsCollectionMaterialization(shaperExpression);

            if (!_containsCollectionMaterialization)
            {
                var result = Visit(shaperExpression);
                _expressions.AddRange(_includeExpressions);
                _expressions.Add(result);
                result = Expression.Block(_variables, _expressions);

                relationalCommandCache = _generateCommandCache
                    ? new RelationalCommandCache(
                        _parentVisitor.Dependencies.MemoryCache,
                        _parentVisitor.RelationalDependencies.QuerySqlGeneratorFactory,
                        _parentVisitor.RelationalDependencies.RelationalParameterBasedSqlProcessorFactory,
                        _selectExpression,
                        _parentVisitor._useRelationalNulls)
                    : null;
                readerColumns = _readerColumns;

                return Expression.Lambda(
                    result,
                    QueryCompilationContext.QueryContextParameter,
                    _dataReaderParameter,
                    _resultContextParameter,
                    _resultCoordinatorParameter);
            }
            else
            {
                _valuesArrayExpression = Expression.MakeMemberAccess(_resultContextParameter, ResultContextValuesMemberInfo);
                _collectionPopulatingExpressions = new List<Expression>();
                _valuesArrayInitializers = new List<Expression>();

                var result = Visit(shaperExpression);

                var valueArrayInitializationExpression = Expression.Assign(
                    _valuesArrayExpression, Expression.NewArrayInit(typeof(object), _valuesArrayInitializers));

                _expressions.Add(valueArrayInitializationExpression);
                _expressions.AddRange(_includeExpressions);

                if (_splitQuery)
                {
                    _expressions.Add(Expression.Default(result.Type));

                    var initializationBlock = Expression.Block(_variables, _expressions);
                    result = Expression.Condition(
                        Expression.Equal(_valuesArrayExpression, Expression.Constant(null, typeof(object[]))),
                        initializationBlock,
                        result);

                    if (_isAsync)
                    {
                        var tasks = Expression.NewArrayInit(
                            typeof(Func<Task>), _collectionPopulatingExpressions.Select(
                                e => Expression.Lambda<Func<Task>>(e)));
                        relatedDataLoaders =
                            Expression.Lambda<Func<QueryContext, IExecutionStrategy, SplitQueryResultCoordinator, Task>>(
                                Expression.Call(TaskAwaiterMethodInfo, tasks),
                                QueryCompilationContext.QueryContextParameter,
                                _executionStrategyParameter!,
                                _resultCoordinatorParameter);
                    }
                    else
                    {
                        relatedDataLoaders =
                            Expression.Lambda<Action<QueryContext, IExecutionStrategy, SplitQueryResultCoordinator>>(
                                Expression.Block(_collectionPopulatingExpressions),
                                QueryCompilationContext.QueryContextParameter,
                                _executionStrategyParameter!,
                                _resultCoordinatorParameter);
                    }
                }
                else
                {
                    var initializationBlock = Expression.Block(_variables, _expressions);

                    var conditionalMaterializationExpressions = new List<Expression>
                    {
                        Expression.IfThen(
                            Expression.Equal(_valuesArrayExpression, Expression.Constant(null, typeof(object[]))),
                            initializationBlock)
                    };

                    conditionalMaterializationExpressions.AddRange(_collectionPopulatingExpressions);

                    conditionalMaterializationExpressions.Add(
                        Expression.Condition(
                            Expression.IsTrue(
                                Expression.MakeMemberAccess(
                                    _resultCoordinatorParameter, SingleQueryResultCoordinatorResultReadyMemberInfo)),
                            result,
                            Expression.Default(result.Type)));

                    result = Expression.Block(conditionalMaterializationExpressions);
                }

                relationalCommandCache = _generateCommandCache
                    ? new RelationalCommandCache(
                        _parentVisitor.Dependencies.MemoryCache,
                        _parentVisitor.RelationalDependencies.QuerySqlGeneratorFactory,
                        _parentVisitor.RelationalDependencies.RelationalParameterBasedSqlProcessorFactory,
                        _selectExpression,
                        _parentVisitor._useRelationalNulls)
                    : null;
                readerColumns = _readerColumns;

                collectionId = _collectionId;

                return Expression.Lambda(
                    result,
                    QueryCompilationContext.QueryContextParameter,
                    _dataReaderParameter,
                    _resultContextParameter,
                    _resultCoordinatorParameter);
            }
        }

        protected override Expression VisitBinary(BinaryExpression binaryExpression)
        {
            if (binaryExpression.NodeType == ExpressionType.Assign
                && binaryExpression.Left is ParameterExpression parameterExpression
                && parameterExpression.Type == typeof(MaterializationContext))
            {
                var newExpression = (NewExpression)binaryExpression.Right;

                if (newExpression.Arguments[0] is ProjectionBindingExpression projectionBindingExpression)
                {
                    var propertyMap = (IDictionary<IProperty, int>)GetProjectionIndex(projectionBindingExpression);
                    _materializationContextBindings[parameterExpression] = propertyMap;
                    _entityTypeIdentifyingExpressionInfo[parameterExpression] =
                        // If single entity type is being selected in hierarchy then we use the value directly else we store the offset to
                        // read discriminator value.
                        _singleEntityTypeDiscriminatorValues.TryGetValue(projectionBindingExpression, out var value)
                        ? value
                        : propertyMap.Values.Max() + 1;

                    var updatedExpression = newExpression.Update(
                        new[] { Expression.Constant(ValueBuffer.Empty), newExpression.Arguments[1] });

                    return Expression.Assign(binaryExpression.Left, updatedExpression);
                }
                else if (newExpression.Arguments[0] is ParameterExpression valueBufferParameter
                    && _jsonValueBufferParameterMapping.ContainsKey(valueBufferParameter))
                {
                    _jsonMaterializationContextParameterMapping[parameterExpression] = _jsonValueBufferParameterMapping[valueBufferParameter];

                    var updatedExpression = newExpression.Update(
                        new[] { Expression.Constant(ValueBuffer.Empty), newExpression.Arguments[1] });

                    return Expression.Assign(binaryExpression.Left, updatedExpression);
                }
            }

            if (binaryExpression.NodeType == ExpressionType.Assign
                && binaryExpression.Left is MemberExpression memberExpression
                && memberExpression.Member is FieldInfo fieldInfo
                && fieldInfo.IsInitOnly)
            {
                return memberExpression.Assign(Visit(binaryExpression.Right));
            }

            return base.VisitBinary(binaryExpression);
        }

        private Expression CreateJsonShapers(
            IEntityType entityType,
            bool nullable,
            bool collection,
            ParameterExpression jsonElementParameter,
            ParameterExpression keyValuesParameter,
            ParameterExpression? outerEntityInstanceParameter,
            INavigation? navigation)
        {
            var jsonElementShaperLambdaParameter = Expression.Parameter(typeof(JsonElement));
            var keyValuesShaperLambdaParameter = Expression.Parameter(typeof(object[]));
            var shaperBlockVariables = new List<ParameterExpression>();
            var shaperBlockExpressions = new List<Expression>();

            var valueBufferParameter = Expression.Parameter(typeof(ValueBuffer));

            _jsonValueBufferParameterMapping[valueBufferParameter] = (jsonElementShaperLambdaParameter, keyValuesShaperLambdaParameter);

            var entityShaperExpression = new RelationalEntityShaperExpression(
                entityType,
                valueBufferParameter,
                nullable);

            var entityShaperMaterializer = (BlockExpression)_parentVisitor.InjectEntityMaterializers(entityShaperExpression);

            // the result of the injected materializer (i.e. the entity instance parameter) will be added to the block at the very end,
            // once we process all it's owned navigations
            var visitedExpressionsArray = entityShaperMaterializer.Expressions.ToArray();
            shaperBlockVariables.AddRange(entityShaperMaterializer.Variables);
            shaperBlockExpressions.AddRange(entityShaperMaterializer.Expressions.ToArray()[..^1]);
            var entityInstanceParameter = (ParameterExpression)entityShaperMaterializer.Result;

            foreach (var ownedNavigation in entityType.GetNavigations().Where(
                n => n.TargetEntityType.IsMappedToJson() && n.ForeignKey.IsOwnership && n == n.ForeignKey.PrincipalToDependent))
            {
                var ownedNullable = nullable || !ownedNavigation.ForeignKey.IsRequired;

                // TODO: use caching like we do in pre-process, there's chance we already have this json element
                var innerJsonElementParameter = Expression.Variable(
                    typeof(JsonElement?));

                shaperBlockVariables.Add(innerJsonElementParameter);

                // TODO: do TryGetProperty and short circuit if failed instead
                var innerJsonElementAssignment = Expression.Assign(
                    innerJsonElementParameter,
                    Expression.Convert(
                        Expression.Call(
                            jsonElementShaperLambdaParameter,
                            JsonElementGetPropertyMethod,
                            Expression.Constant(ownedNavigation.GetJsonElementName())),
                        typeof(JsonElement?)));

                shaperBlockExpressions.Add(innerJsonElementAssignment);

                var innerShaperResult = CreateJsonShapers(
                    ownedNavigation.TargetEntityType,
                    nullable || !ownedNavigation.ForeignKey.IsRequired,
                    ownedNavigation.IsCollection,
                    innerJsonElementParameter,
                    keyValuesShaperLambdaParameter,
                    entityInstanceParameter,
                    ownedNavigation);

                shaperBlockExpressions.Add(innerShaperResult);
            }

            shaperBlockExpressions.Add(entityInstanceParameter);

            var shaperBlock = Expression.Block(
                shaperBlockVariables,
                shaperBlockExpressions);

            var shaperLambda = Expression.Lambda(
                shaperBlock,
                QueryCompilationContext.QueryContextParameter,
                keyValuesShaperLambdaParameter,
                jsonElementShaperLambdaParameter);

            if (outerEntityInstanceParameter != null)
            {
                Debug.Assert(navigation != null, "Navigation shouldn't be null when including.");

                var fixup = GenerateFixup(
                    navigation.DeclaringEntityType.ClrType,
                    navigation.TargetEntityType.ClrType,
                    navigation,
                    navigation.Inverse);

                // inheritance scenario - navigation defined on derived
                var outerEntityInstanceExpression = outerEntityInstanceParameter.Type != navigation.DeclaringEntityType.ClrType
                    ? Expression.Convert(outerEntityInstanceParameter, navigation.DeclaringEntityType.ClrType)
                    : (Expression)outerEntityInstanceParameter;

                if (navigation.IsCollection)
                {
                    var includeJsonEntityCollectionMethodCall = 
                        Expression.Call(
                            null,
                            IncludeJsonEntityCollectionMethodInfo.MakeGenericMethod(
                                navigation.DeclaringEntityType.ClrType,
                                navigation.TargetEntityType.ClrType),
                            QueryCompilationContext.QueryContextParameter,
                            jsonElementParameter,
                            keyValuesParameter,
                            outerEntityInstanceExpression,
                            shaperLambda,
                            fixup);

                    return navigation.DeclaringEntityType.ClrType.IsAssignableFrom(outerEntityInstanceParameter.Type)
                        ? includeJsonEntityCollectionMethodCall
                        : Expression.IfThen(
                            Expression.TypeIs(
                                outerEntityInstanceParameter,
                                navigation.DeclaringEntityType.ClrType),
                            includeJsonEntityCollectionMethodCall);
                }
                else
                {
                    var table = entityType.GetViewOrTableMappings().SingleOrDefault()?.Table
                        ?? entityType.GetDefaultMappings().Single().Table;
                    var optionalDependent = table.IsOptional(entityType);

                    var includeJsonEntityReferenceMethodCall = 
                        Expression.Call(
                            null,
                            IncludeJsonEntityReferenceMethodInfo.MakeGenericMethod(
                                navigation.DeclaringEntityType.ClrType,
                                navigation.TargetEntityType.ClrType),
                            QueryCompilationContext.QueryContextParameter,
                            jsonElementParameter,
                            keyValuesParameter,
                            outerEntityInstanceExpression,
                            Expression.Constant(optionalDependent),
                            shaperLambda,
                            fixup);

                    return navigation.DeclaringEntityType.ClrType.IsAssignableFrom(outerEntityInstanceParameter.Type)
                        ? includeJsonEntityReferenceMethodCall
                        : Expression.IfThen(
                            Expression.TypeIs(
                                outerEntityInstanceParameter,
                                navigation.DeclaringEntityType.ClrType),
                            includeJsonEntityReferenceMethodCall);
                }
            }
            else
            {
                if (collection)
                { 
                    Debug.Assert(navigation != null, "navigation shouldn't be null when materializing collection.");

                    var materializeJsonEntityCollection = Expression.Call(
                        null,
                        MaterializeJsonEntityCollectionMethodInfo.MakeGenericMethod(
                            entityType.ClrType,
                            navigation.ClrType),
                        QueryCompilationContext.QueryContextParameter,
                        jsonElementParameter,
                        keyValuesParameter,
                        Expression.Constant(navigation),
                        shaperLambda);

                    return materializeJsonEntityCollection;
                }

                // TODO: just remap the shaper and return it instead
                var materializedRootJsonEntity = Expression.Call(
                    null,
                    MaterializeJsonEntityMethodInfo.MakeGenericMethod(entityType.ClrType),
                    QueryCompilationContext.QueryContextParameter,
                    jsonElementParameter,
                    Expression.Constant(nullable),
                    keyValuesParameter,
                    shaperLambda);

                return materializedRootJsonEntity;
            }
        }

        protected override Expression VisitExtension(Expression extensionExpression)
        {
            switch (extensionExpression)
            {
                case RelationalEntityShaperExpression entityShaperExpression
                    when entityShaperExpression.ValueBufferExpression is ProjectionBindingExpression projectionBindingExpression:
                {
                    if (GetProjectionIndex(projectionBindingExpression) is ValueTuple<int, List<(IProperty, int)>, string[]> jsonProjectionIndex)
                    {
                        // json entity at the root
                        var (jsonElementParameter, keyValuesParameter) = JsonShapingPreProcess(
                            jsonProjectionIndex,
                            entityShaperExpression.EntityType,
                            isCollection: false);

                        var shaperResult = CreateJsonShapers(
                            entityShaperExpression.EntityType,
                            entityShaperExpression.IsNullable,
                            collection: false,
                            jsonElementParameter,
                            keyValuesParameter,
                            outerEntityInstanceParameter: null,
                            navigation: null);

                        var visitedShaperResult = Visit(shaperResult);

                        return visitedShaperResult;
                    }

                    if (!_variableShaperMapping.TryGetValue(entityShaperExpression.ValueBufferExpression, out var accessor))
                    {
                        var entityParameter = Expression.Parameter(entityShaperExpression.Type);
                        _variables.Add(entityParameter);
                        if (entityShaperExpression.EntityType.GetMappingStrategy() == RelationalAnnotationNames.TpcMappingStrategy)
                        {
                            var concreteTypes = entityShaperExpression.EntityType.GetDerivedTypesInclusive().Where(e => !e.IsAbstract()).ToArray();
                            // Single concrete TPC entity type won't have discriminator column.
                            // We store the value here and inject it directly rather than reading from server.
                            if (concreteTypes.Length == 1)
                                _singleEntityTypeDiscriminatorValues[(ProjectionBindingExpression)entityShaperExpression.ValueBufferExpression]
                                    = concreteTypes[0].ShortName();
                        }

                        var entityMaterializationExpression = _parentVisitor.InjectEntityMaterializers(entityShaperExpression);
                        entityMaterializationExpression = Visit(entityMaterializationExpression);

                        _expressions.Add(Expression.Assign(entityParameter, entityMaterializationExpression));

                        if (_containsCollectionMaterialization)
                        {
                            _valuesArrayInitializers!.Add(entityParameter);
                            accessor = Expression.Convert(
                                Expression.ArrayIndex(
                                    _valuesArrayExpression!,
                                    Expression.Constant(_valuesArrayInitializers.Count - 1)),
                                entityShaperExpression.Type);
                        }
                        else
                        {
                            accessor = entityParameter;
                        }

                        _variableShaperMapping[entityShaperExpression.ValueBufferExpression] = accessor;
                    }

                    return accessor;
                }

                case CollectionResultExpression collectionResultExpression
                    when collectionResultExpression.Navigation is INavigation navigation
                    && GetProjectionIndex(collectionResultExpression.ProjectionBindingExpression)
                    is ValueTuple<int, List<(IProperty, int)>, string[]> jsonProjectionIndex:
                {
                    // json entity collection at the root
                    var (jsonElementParameter, keyValuesParameter) = JsonShapingPreProcess(
                        jsonProjectionIndex,
                        navigation.TargetEntityType,
                        isCollection: true);

                    var shaperResult = CreateJsonShapers(
                        navigation.TargetEntityType,
                        nullable: true,
                        collection: true,
                        jsonElementParameter,
                        keyValuesParameter,
                        outerEntityInstanceParameter: null,
                        navigation);

                    var visitedShaperResult = Visit(shaperResult);

                    return visitedShaperResult;
                }

                case ProjectionBindingExpression projectionBindingExpression
                    when _inline:
                {
                    var projectionIndex = (int)GetProjectionIndex(projectionBindingExpression);
                    var projection = _selectExpression.Projection[projectionIndex];

                    return CreateGetValueExpression(
                        _dataReaderParameter,
                        projectionIndex,
                        IsNullableProjection(projection),
                        projection.Expression.TypeMapping!,
                        projectionBindingExpression.Type);
                }

                case ProjectionBindingExpression projectionBindingExpression
                    when !_inline:
                {
                    if (_variableShaperMapping.TryGetValue(projectionBindingExpression, out var accessor))
                    {
                        return accessor;
                    }

                    var projectionIndex = (int)GetProjectionIndex(projectionBindingExpression);
                    var projection = _selectExpression.Projection[projectionIndex];
                    var nullable = IsNullableProjection(projection);

                    var valueParameter = Expression.Parameter(projectionBindingExpression.Type);
                    _variables.Add(valueParameter);

                    _expressions.Add(
                        Expression.Assign(
                            valueParameter,
                            CreateGetValueExpression(
                                _dataReaderParameter,
                                projectionIndex,
                                nullable,
                                projection.Expression.TypeMapping!,
                                valueParameter.Type)));

                    if (_containsCollectionMaterialization)
                    {
                        var expressionToAdd = (Expression)valueParameter;
                        if (expressionToAdd.Type.IsValueType)
                        {
                            expressionToAdd = Expression.Convert(expressionToAdd, typeof(object));
                        }

                        _valuesArrayInitializers!.Add(expressionToAdd);
                        accessor = Expression.Convert(
                            Expression.ArrayIndex(
                                _valuesArrayExpression!,
                                Expression.Constant(_valuesArrayInitializers.Count - 1)),
                            projectionBindingExpression.Type);
                    }
                    else
                    {
                        accessor = valueParameter;
                    }

                    _variableShaperMapping[projectionBindingExpression] = accessor;

                    return accessor;
                }

                case IncludeExpression includeExpression:
                {
                    var entity = Visit(includeExpression.EntityExpression);
                    if (includeExpression.NavigationExpression is RelationalCollectionShaperExpression
                        relationalCollectionShaperExpression)
                    {
                        var collectionIdConstant = Expression.Constant(_collectionId++);
                        var innerShaper = new ShaperProcessingExpressionVisitor(
                                _parentVisitor, _resultCoordinatorParameter, _selectExpression, _dataReaderParameter,
                                _resultContextParameter,
                                _readerColumns)
                            .ProcessShaper(relationalCollectionShaperExpression.InnerShaper, out _, out _, out _, ref _collectionId);

                        var entityType = entity.Type;
                        var navigation = includeExpression.Navigation;
                        var includingEntityType = navigation.DeclaringEntityType.ClrType;
                        if (includingEntityType != entityType
                            && includingEntityType.IsAssignableFrom(entityType))
                        {
                            includingEntityType = entityType;
                        }

                        _inline = true;

                        var parentIdentifierLambda = Expression.Lambda(
                            Visit(relationalCollectionShaperExpression.ParentIdentifier),
                            QueryCompilationContext.QueryContextParameter,
                            _dataReaderParameter);

                        var outerIdentifierLambda = Expression.Lambda(
                            Visit(relationalCollectionShaperExpression.OuterIdentifier),
                            QueryCompilationContext.QueryContextParameter,
                            _dataReaderParameter);

                        var selfIdentifierLambda = Expression.Lambda(
                            Visit(relationalCollectionShaperExpression.SelfIdentifier),
                            QueryCompilationContext.QueryContextParameter,
                            _dataReaderParameter);

                        _inline = false;

                        _includeExpressions.Add(
                            Expression.Call(
                                InitializeIncludeCollectionMethodInfo.MakeGenericMethod(entityType, includingEntityType),
                                collectionIdConstant,
                                QueryCompilationContext.QueryContextParameter,
                                _dataReaderParameter,
                                _resultCoordinatorParameter,
                                entity,
                                Expression.Constant(parentIdentifierLambda.Compile()),
                                Expression.Constant(outerIdentifierLambda.Compile()),
                                Expression.Constant(navigation),
                                Expression.Constant(navigation.GetCollectionAccessor()),
                                Expression.Constant(_isTracking),
#pragma warning disable EF1001 // Internal EF Core API usage.
                                Expression.Constant(includeExpression.SetLoaded)));
#pragma warning restore EF1001 // Internal EF Core API usage.

                        var relatedEntityType = innerShaper.ReturnType;
                        var inverseNavigation = navigation.Inverse;

                        _collectionPopulatingExpressions!.Add(
                            Expression.Call(
                                PopulateIncludeCollectionMethodInfo.MakeGenericMethod(includingEntityType, relatedEntityType),
                                collectionIdConstant,
                                QueryCompilationContext.QueryContextParameter,
                                _dataReaderParameter,
                                _resultCoordinatorParameter,
                                Expression.Constant(parentIdentifierLambda.Compile()),
                                Expression.Constant(outerIdentifierLambda.Compile()),
                                Expression.Constant(selfIdentifierLambda.Compile()),
                                Expression.Constant(
                                    relationalCollectionShaperExpression.ParentIdentifierValueComparers,
                                    typeof(IReadOnlyList<ValueComparer>)),
                                Expression.Constant(
                                    relationalCollectionShaperExpression.OuterIdentifierValueComparers,
                                    typeof(IReadOnlyList<ValueComparer>)),
                                Expression.Constant(
                                    relationalCollectionShaperExpression.SelfIdentifierValueComparers,
                                    typeof(IReadOnlyList<ValueComparer>)),
                                Expression.Constant(innerShaper.Compile()),
                                Expression.Constant(inverseNavigation, typeof(INavigationBase)),
                                Expression.Constant(
                                    GenerateFixup(
                                        includingEntityType, relatedEntityType, navigation, inverseNavigation).Compile()),
                                Expression.Constant(_isTracking)));
                    }
                    else if (includeExpression.NavigationExpression is RelationalSplitCollectionShaperExpression
                             relationalSplitCollectionShaperExpression)
                    {
                        var collectionIdConstant = Expression.Constant(_collectionId++);
                        var innerProcessor = new ShaperProcessingExpressionVisitor(
                            _parentVisitor, _resultCoordinatorParameter,
                            _executionStrategyParameter!, relationalSplitCollectionShaperExpression.SelectExpression, _tags!);
                        var innerShaper = innerProcessor.ProcessShaper(
                            relationalSplitCollectionShaperExpression.InnerShaper,
                            out var relationalCommandCache,
                            out var readerColumns,
                            out var relatedDataLoaders,
                            ref _collectionId);

                        var entityType = entity.Type;
                        var navigation = includeExpression.Navigation;
                        var includingEntityType = navigation.DeclaringEntityType.ClrType;
                        if (includingEntityType != entityType
                            && includingEntityType.IsAssignableFrom(entityType))
                        {
                            includingEntityType = entityType;
                        }

                        _inline = true;

                        var parentIdentifierLambda = Expression.Lambda(
                            Visit(relationalSplitCollectionShaperExpression.ParentIdentifier),
                            QueryCompilationContext.QueryContextParameter,
                            _dataReaderParameter);

                        _inline = false;

                        innerProcessor._inline = true;

                        var childIdentifierLambda = Expression.Lambda(
                            innerProcessor.Visit(relationalSplitCollectionShaperExpression.ChildIdentifier),
                            QueryCompilationContext.QueryContextParameter,
                            innerProcessor._dataReaderParameter);

                        innerProcessor._inline = false;

                        _includeExpressions.Add(
                            Expression.Call(
                                InitializeSplitIncludeCollectionMethodInfo.MakeGenericMethod(entityType, includingEntityType),
                                collectionIdConstant,
                                QueryCompilationContext.QueryContextParameter,
                                _dataReaderParameter,
                                _resultCoordinatorParameter,
                                entity,
                                Expression.Constant(parentIdentifierLambda.Compile()),
                                Expression.Constant(navigation),
                                Expression.Constant(navigation.GetCollectionAccessor()),
                                Expression.Constant(_isTracking),
#pragma warning disable EF1001 // Internal EF Core API usage.
                                Expression.Constant(includeExpression.SetLoaded)));
#pragma warning restore EF1001 // Internal EF Core API usage.

                        var relatedEntityType = innerShaper.ReturnType;
                        var inverseNavigation = navigation.Inverse;

                        _collectionPopulatingExpressions!.Add(
                            Expression.Call(
                                (_isAsync ? PopulateSplitIncludeCollectionAsyncMethodInfo : PopulateSplitIncludeCollectionMethodInfo)
                                .MakeGenericMethod(includingEntityType, relatedEntityType),
                                collectionIdConstant,
                                Expression.Convert(QueryCompilationContext.QueryContextParameter, typeof(RelationalQueryContext)),
                                _executionStrategyParameter!,
                                Expression.Constant(relationalCommandCache),
                                Expression.Constant(readerColumns, typeof(IReadOnlyList<ReaderColumn?>)),
                                Expression.Constant(_detailedErrorsEnabled),
                                _resultCoordinatorParameter,
                                Expression.Constant(childIdentifierLambda.Compile()),
                                Expression.Constant(
                                    relationalSplitCollectionShaperExpression.IdentifierValueComparers,
                                    typeof(IReadOnlyList<ValueComparer>)),
                                Expression.Constant(innerShaper.Compile()),
                                Expression.Constant(
                                    relatedDataLoaders?.Compile(),
                                    _isAsync
                                        ? typeof(Func<QueryContext, IExecutionStrategy, SplitQueryResultCoordinator, Task>)
                                        : typeof(Action<QueryContext, IExecutionStrategy, SplitQueryResultCoordinator>)),
                                Expression.Constant(inverseNavigation, typeof(INavigationBase)),
                                Expression.Constant(
                                    GenerateFixup(
                                        includingEntityType, relatedEntityType, navigation, inverseNavigation).Compile()),
                                Expression.Constant(_isTracking)));
                    }
                    else
                    {
                        var projectionBindingExpression = (includeExpression.NavigationExpression as CollectionResultExpression)?.ProjectionBindingExpression
                            ?? (includeExpression.NavigationExpression as RelationalEntityShaperExpression)?.ValueBufferExpression as ProjectionBindingExpression;

                        // json include case
                        if (projectionBindingExpression != null
                            && GetProjectionIndex(projectionBindingExpression) is ValueTuple<int, List<(IProperty, int)>, string[]> jsonProjectionIndex)
                        {
                            var (jsonElementParameter, keyValuesParameter) = JsonShapingPreProcess(
                                jsonProjectionIndex,
                                includeExpression.Navigation.TargetEntityType,
                                includeExpression.Navigation.IsCollection);

                            var shaperResult = CreateJsonShapers(
                                includeExpression.Navigation.TargetEntityType,
                                nullable: true,
                                collection: includeExpression.NavigationExpression is CollectionResultExpression,
                                jsonElementParameter,
                                keyValuesParameter,
                                outerEntityInstanceParameter: (ParameterExpression)entity,
                                navigation: (INavigation)includeExpression.Navigation);

                            var visitedShaperResult = Visit(shaperResult);

                            _expressions.Add(visitedShaperResult);

                            return entity;
                        }

                        var navigationExpression = Visit(includeExpression.NavigationExpression);
                        var entityType = entity.Type;
                        var navigation = includeExpression.Navigation;
                        var includingType = navigation.DeclaringEntityType.ClrType;
                        var inverseNavigation = navigation.Inverse;
                        var relatedEntityType = navigation.TargetEntityType.ClrType;
                        if (includingType != entityType
                            && includingType.IsAssignableFrom(entityType))
                        {
                            includingType = entityType;
                        }

                        var updatedExpression = Expression.Call(
                            IncludeReferenceMethodInfo.MakeGenericMethod(entityType, includingType, relatedEntityType),
                            QueryCompilationContext.QueryContextParameter,
                            entity,
                            navigationExpression,
                            Expression.Constant(navigation),
                            Expression.Constant(inverseNavigation, typeof(INavigationBase)),
                            Expression.Constant(
                                GenerateFixup(
                                    includingType, relatedEntityType, navigation, inverseNavigation).Compile()),
                            Expression.Constant(_isTracking));

                        _includeExpressions.Add(updatedExpression);
                    }

                    return entity;
                }

                case RelationalCollectionShaperExpression relationalCollectionShaperExpression:
                {
                    if (!_variableShaperMapping.TryGetValue(relationalCollectionShaperExpression, out var accessor))
                    {
                        var collectionIdConstant = Expression.Constant(_collectionId++);
                        var innerShaper = new ShaperProcessingExpressionVisitor(
                                _parentVisitor, _resultCoordinatorParameter, _selectExpression, _dataReaderParameter,
                                _resultContextParameter,
                                _readerColumns)
                            .ProcessShaper(relationalCollectionShaperExpression.InnerShaper, out _, out _, out _, ref _collectionId);

                        var navigation = relationalCollectionShaperExpression.Navigation;
                        var collectionAccessor = navigation?.GetCollectionAccessor();
                        var collectionType = collectionAccessor?.CollectionType ?? relationalCollectionShaperExpression.Type;
                        var elementType = relationalCollectionShaperExpression.ElementType;
                        var relatedElementType = innerShaper.ReturnType;

                        _inline = true;

                        var parentIdentifierLambda = Expression.Lambda(
                            Visit(relationalCollectionShaperExpression.ParentIdentifier),
                            QueryCompilationContext.QueryContextParameter,
                            _dataReaderParameter);

                        var outerIdentifierLambda = Expression.Lambda(
                            Visit(relationalCollectionShaperExpression.OuterIdentifier),
                            QueryCompilationContext.QueryContextParameter,
                            _dataReaderParameter);

                        var selfIdentifierLambda = Expression.Lambda(
                            Visit(relationalCollectionShaperExpression.SelfIdentifier),
                            QueryCompilationContext.QueryContextParameter,
                            _dataReaderParameter);

                        _inline = false;

                        var collectionParameter = Expression.Parameter(relationalCollectionShaperExpression.Type);
                        _variables.Add(collectionParameter);
                        _expressions.Add(
                            Expression.Assign(
                                collectionParameter,
                                Expression.Call(
                                    InitializeCollectionMethodInfo.MakeGenericMethod(elementType, collectionType),
                                    collectionIdConstant,
                                    QueryCompilationContext.QueryContextParameter,
                                    _dataReaderParameter,
                                    _resultCoordinatorParameter,
                                    Expression.Constant(parentIdentifierLambda.Compile()),
                                    Expression.Constant(outerIdentifierLambda.Compile()),
                                    Expression.Constant(collectionAccessor, typeof(IClrCollectionAccessor)))));

                        _valuesArrayInitializers!.Add(collectionParameter);
                        accessor = Expression.Convert(
                            Expression.ArrayIndex(
                                _valuesArrayExpression!,
                                Expression.Constant(_valuesArrayInitializers.Count - 1)),
                            relationalCollectionShaperExpression.Type);

                        _collectionPopulatingExpressions!.Add(
                            Expression.Call(
                                PopulateCollectionMethodInfo.MakeGenericMethod(collectionType, elementType, relatedElementType),
                                collectionIdConstant,
                                QueryCompilationContext.QueryContextParameter,
                                _dataReaderParameter,
                                _resultCoordinatorParameter,
                                Expression.Constant(parentIdentifierLambda.Compile()),
                                Expression.Constant(outerIdentifierLambda.Compile()),
                                Expression.Constant(selfIdentifierLambda.Compile()),
                                Expression.Constant(
                                    relationalCollectionShaperExpression.ParentIdentifierValueComparers,
                                    typeof(IReadOnlyList<ValueComparer>)),
                                Expression.Constant(
                                    relationalCollectionShaperExpression.OuterIdentifierValueComparers,
                                    typeof(IReadOnlyList<ValueComparer>)),
                                Expression.Constant(
                                    relationalCollectionShaperExpression.SelfIdentifierValueComparers,
                                    typeof(IReadOnlyList<ValueComparer>)),
                                Expression.Constant(innerShaper.Compile())));

                        _variableShaperMapping[relationalCollectionShaperExpression] = accessor;
                    }

                    return accessor;
                }

                case RelationalSplitCollectionShaperExpression relationalSplitCollectionShaperExpression:
                {
                    if (!_variableShaperMapping.TryGetValue(relationalSplitCollectionShaperExpression, out var accessor))
                    {
                        var collectionIdConstant = Expression.Constant(_collectionId++);
                        var innerProcessor = new ShaperProcessingExpressionVisitor(
                            _parentVisitor, _resultCoordinatorParameter,
                            _executionStrategyParameter!, relationalSplitCollectionShaperExpression.SelectExpression, _tags!);
                        var innerShaper = innerProcessor.ProcessShaper(
                            relationalSplitCollectionShaperExpression.InnerShaper,
                            out var relationalCommandCache,
                            out var readerColumns,
                            out var relatedDataLoaders,
                            ref _collectionId);

                        var navigation = relationalSplitCollectionShaperExpression.Navigation;
                        var collectionAccessor = navigation?.GetCollectionAccessor();
                        var collectionType = collectionAccessor?.CollectionType ?? relationalSplitCollectionShaperExpression.Type;
                        var elementType = relationalSplitCollectionShaperExpression.ElementType;
                        var relatedElementType = innerShaper.ReturnType;

                        _inline = true;

                        var parentIdentifierLambda = Expression.Lambda(
                            Visit(relationalSplitCollectionShaperExpression.ParentIdentifier),
                            QueryCompilationContext.QueryContextParameter,
                            _dataReaderParameter);

                        _inline = false;

                        innerProcessor._inline = true;

                        var childIdentifierLambda = Expression.Lambda(
                            innerProcessor.Visit(relationalSplitCollectionShaperExpression.ChildIdentifier),
                            QueryCompilationContext.QueryContextParameter,
                            innerProcessor._dataReaderParameter);

                        innerProcessor._inline = false;

                        var collectionParameter = Expression.Parameter(collectionType);
                        _variables.Add(collectionParameter);
                        _expressions.Add(
                            Expression.Assign(
                                collectionParameter,
                                Expression.Call(
                                    InitializeSplitCollectionMethodInfo.MakeGenericMethod(elementType, collectionType),
                                    collectionIdConstant,
                                    QueryCompilationContext.QueryContextParameter,
                                    _dataReaderParameter,
                                    _resultCoordinatorParameter,
                                    Expression.Constant(parentIdentifierLambda.Compile()),
                                    Expression.Constant(collectionAccessor, typeof(IClrCollectionAccessor)))));

                        _valuesArrayInitializers!.Add(collectionParameter);
                        accessor = Expression.Convert(
                            Expression.ArrayIndex(
                                _valuesArrayExpression!,
                                Expression.Constant(_valuesArrayInitializers.Count - 1)),
                            relationalSplitCollectionShaperExpression.Type);

                        _collectionPopulatingExpressions!.Add(
                            Expression.Call(
                                (_isAsync ? PopulateSplitCollectionAsyncMethodInfo : PopulateSplitCollectionMethodInfo)
                                .MakeGenericMethod(collectionType, elementType, relatedElementType),
                                collectionIdConstant,
                                Expression.Convert(QueryCompilationContext.QueryContextParameter, typeof(RelationalQueryContext)),
                                _executionStrategyParameter!,
                                Expression.Constant(relationalCommandCache),
                                Expression.Constant(readerColumns, typeof(IReadOnlyList<ReaderColumn?>)),
                                Expression.Constant(_detailedErrorsEnabled),
                                _resultCoordinatorParameter,
                                Expression.Constant(childIdentifierLambda.Compile()),
                                Expression.Constant(
                                    relationalSplitCollectionShaperExpression.IdentifierValueComparers,
                                    typeof(IReadOnlyList<ValueComparer>)),
                                Expression.Constant(innerShaper.Compile()),
                                Expression.Constant(
                                    relatedDataLoaders?.Compile(),
                                    _isAsync
                                        ? typeof(Func<QueryContext, IExecutionStrategy, SplitQueryResultCoordinator, Task>)
                                        : typeof(Action<QueryContext, IExecutionStrategy, SplitQueryResultCoordinator>))));

                        _variableShaperMapping[relationalSplitCollectionShaperExpression] = accessor;
                    }

                    return accessor;
                }

                case GroupByShaperExpression:
                    throw new InvalidOperationException(RelationalStrings.ClientGroupByNotSupported);
            }

            return base.VisitExtension(extensionExpression);
        }

        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            if (methodCallExpression.Method.IsGenericMethod
                && methodCallExpression.Method.GetGenericMethodDefinition()
                == Infrastructure.ExpressionExtensions.ValueBufferTryReadValueMethod)
            {
                var index = methodCallExpression.Arguments[1].GetConstantValue<int>();
                var property = methodCallExpression.Arguments[2].GetConstantValue<IProperty>();
                var mappingParameter = (ParameterExpression)((MethodCallExpression)methodCallExpression.Arguments[0]).Object!;

                if (_jsonMaterializationContextParameterMapping.ContainsKey(mappingParameter))
                {
                    (var jsonElementParameter, var keyPropertyValuesParameter) = _jsonMaterializationContextParameterMapping[mappingParameter];

                    if (property.IsPrimaryKey())
                    {
                        return Expression.MakeIndex(
                            keyPropertyValuesParameter,
                            _objectArrayIndexerPropertyInfo,
                            new[] { Expression.Constant(index) });
                    }

                    return Expression.Convert(
                        Expression.Call(
                            null,
                            ExtractJsonPropertyMethodInfo,
                            jsonElementParameter,
                            Expression.Constant(property.GetJsonElementName()),
                            Expression.Constant(property.ClrType)),
                        property.ClrType);
                }
                else
                {
                    int projectionIndex;
                    if (property == null)
                    {
                        // This is trying to read the computed discriminator value
                        var storedInfo = _entityTypeIdentifyingExpressionInfo[mappingParameter];
                        if (storedInfo is string s)
                        {
                            // If the value is fixed then there is single entity type and discriminator is not present in query
                            // We just return the value as-is.
                            return Expression.Constant(s);
                        }

                        projectionIndex = (int)_entityTypeIdentifyingExpressionInfo[mappingParameter] + index;
                    }
                    else
                    {
                        projectionIndex = _materializationContextBindings[mappingParameter][property];
                    }

                    var projection = _selectExpression.Projection[projectionIndex];
                    var nullable = IsNullableProjection(projection);

                    Check.DebugAssert(
                        !nullable || property != null || methodCallExpression.Type.IsNullableType(),
                        "For nullable reads the return type must be null unless property is specified.");

                    return CreateGetValueExpression(
                        _dataReaderParameter,
                        projectionIndex,
                        nullable,
                        projection.Expression.TypeMapping!,
                        methodCallExpression.Type,
                        property);
                }
            }

            return base.VisitMethodCall(methodCallExpression);
        }

        private (ParameterExpression, ParameterExpression) JsonShapingPreProcess(
            ValueTuple<int, List<(IProperty, int)>, string[]>  projectionIndex,
            IEntityType entityType,
            bool isCollection)
        {
            var jsonColumnProjectionIndex = projectionIndex.Item1;
            var keyInfo = projectionIndex.Item2;
            var additionalPath = projectionIndex.Item3;

            var keyValuesParameter = Expression.Parameter(typeof(object[]));
            var keyValues = new Expression[keyInfo.Count];

            for (var i = 0; i < keyInfo.Count; i++)
            {
                var projection = _selectExpression.Projection[keyInfo[i].Item2];

                keyValues[i] = Expression.Convert(
                    CreateGetValueExpression(
                        _dataReaderParameter,
                        keyInfo[i].Item2,
                        IsNullableProjection(projection),
                        projection.Expression.TypeMapping!,
                        keyInfo[i].Item1.ClrType,
                        keyInfo[i].Item1),
                    typeof(object));
            }

            var keyValuesInitialize = Expression.NewArrayInit(typeof(object), keyValues);
            var keyValuesAssignment = Expression.Assign(keyValuesParameter, keyValuesInitialize);

            _variables.Add(keyValuesParameter);
            _expressions.Add(keyValuesAssignment);

            var jsonTypeMapping = (RelationalTypeMapping)entityType.FindRuntimeAnnotationValue(RelationalAnnotationNames.MapToJsonTypeMapping)!;

            if (_existingJsonElementMap.TryGetValue((jsonColumnProjectionIndex, additionalPath), out var exisitingJsonElementVariable))
            {
                return (exisitingJsonElementVariable, keyValuesParameter);
            }

            // TODO: this logic could/should be improved (later)
            var currentJsonElementVariable = default(ParameterExpression);
            var index = 0;
            do
            {
                // try to find JsonElement variable for this json column and path if we encountered (and cached it) before
                // otherwise either create new JsonElement from the data reader if we are at root level
                // or build on top of previous variable withing the navigation chain (e.g. when we encountered the root before, but not this entire path)
                if (!_existingJsonElementMap.TryGetValue((jsonColumnProjectionIndex, additionalPath[..index]), out var exisitingJsonElementVariable2))
                {
                    var jsonElementVariable = Expression.Variable(
                        typeof(JsonElement?));

                    var jsonElementValueExpression = index == 0
                        ? CreateGetValueExpression(
                            _dataReaderParameter,
                            jsonColumnProjectionIndex,
                            nullable: true,
                            jsonTypeMapping,
                            typeof(JsonElement?),
                            property: null)
                        : Expression.Condition(
                            Expression.Property(currentJsonElementVariable!, nameof(Nullable<JsonElement>.HasValue)),
                            Expression.Convert(
                                Expression.Call(
                                    Expression.Property(
                                        currentJsonElementVariable!,
                                        nameof(Nullable<JsonElement>.Value)),
                                    JsonElementGetPropertyMethod,
                                    Expression.Constant(additionalPath[index - 1])),
                                currentJsonElementVariable!.Type),
                                Expression.Default(currentJsonElementVariable!.Type));

                    var jsonElementAssignment = Expression.Assign(
                        jsonElementVariable,
                        jsonElementValueExpression);

                    _variables.Add(jsonElementVariable);
                    _expressions.Add(jsonElementAssignment);
                    _existingJsonElementMap[(jsonColumnProjectionIndex, additionalPath[..index])] = jsonElementVariable;

                    currentJsonElementVariable = jsonElementVariable;
                }
                else
                {
                    currentJsonElementVariable = exisitingJsonElementVariable2;
                }

                index++;
            }
            while (index <= additionalPath.Length);

            return (currentJsonElementVariable!, keyValuesParameter);
        }

        private static LambdaExpression GenerateFixup(
            Type entityType,
            Type relatedEntityType,
            INavigationBase navigation,
            INavigationBase? inverseNavigation)
        {
            var entityParameter = Expression.Parameter(entityType);
            var relatedEntityParameter = Expression.Parameter(relatedEntityType);
            var expressions = new List<Expression>
            {
                navigation.IsCollection
                    ? AddToCollectionNavigation(entityParameter, relatedEntityParameter, navigation)
                    : AssignReferenceNavigation(entityParameter, relatedEntityParameter, navigation)
            };

            if (inverseNavigation != null)
            {
                expressions.Add(
                    inverseNavigation.IsCollection
                        ? AddToCollectionNavigation(relatedEntityParameter, entityParameter, inverseNavigation)
                        : AssignReferenceNavigation(relatedEntityParameter, entityParameter, inverseNavigation));
            }

            return Expression.Lambda(Expression.Block(typeof(void), expressions), entityParameter, relatedEntityParameter);
        }

        private static Expression AssignReferenceNavigation(
            ParameterExpression entity,
            ParameterExpression relatedEntity,
            INavigationBase navigation)
            => entity.MakeMemberAccess(navigation.GetMemberInfo(forMaterialization: true, forSet: true)).Assign(relatedEntity);

        private static Expression AddToCollectionNavigation(
            ParameterExpression entity,
            ParameterExpression relatedEntity,
            INavigationBase navigation)
            => Expression.Call(
                Expression.Constant(navigation.GetCollectionAccessor()),
                CollectionAccessorAddMethodInfo,
                entity,
                relatedEntity,
                Expression.Constant(true));

        private object GetProjectionIndex(ProjectionBindingExpression projectionBindingExpression)
            => _selectExpression.GetProjection(projectionBindingExpression).GetConstantValue<object>();

        private static bool IsNullableProjection(ProjectionExpression projection)
            => projection.Expression is not ColumnExpression column || column.IsNullable;

        private Expression CreateGetValueExpression(
            ParameterExpression dbDataReader,
            int index,
            bool nullable,
            RelationalTypeMapping typeMapping,
            Type type,
            IPropertyBase? property = null)
        {
            Check.DebugAssert(
                property != null || type.IsNullableType(), "Must read nullable value from database if property is not specified.");

            var getMethod = typeMapping.GetDataReaderMethod();

            Expression indexExpression = Expression.Constant(index);
            if (_indexMapParameter != null)
            {
                indexExpression = Expression.ArrayIndex(_indexMapParameter, indexExpression);
            }

            Expression valueExpression
                = Expression.Call(
                    getMethod.DeclaringType != typeof(DbDataReader)
                        ? Expression.Convert(dbDataReader, getMethod.DeclaringType!)
                        : dbDataReader,
                    getMethod,
                    indexExpression);

            var buffering = false;

            if (_readerColumns != null)
            {
                buffering = true;
                var columnType = valueExpression.Type;
                var bufferedColumnType = columnType;
                if (!bufferedColumnType.IsValueType
                    || !BufferedDataReader.IsSupportedValueType(bufferedColumnType))
                {
                    bufferedColumnType = typeof(object);
                }

                if (_readerColumns[index] == null)
                {
                    var bufferedReaderLambdaExpression = valueExpression;
                    if (columnType != bufferedColumnType)
                    {
                        bufferedReaderLambdaExpression = Expression.Convert(bufferedReaderLambdaExpression, bufferedColumnType);
                    }

                    _readerColumns[index] = ReaderColumn.Create(
                        bufferedColumnType,
                        nullable,
                        _indexMapParameter != null ? ((ColumnExpression)_selectExpression.Projection[index].Expression).Name : null,
                        property,
                        Expression.Lambda(
                            bufferedReaderLambdaExpression,
                            dbDataReader,
                            _indexMapParameter ?? Expression.Parameter(typeof(int[]))).Compile());
                }

                valueExpression = Expression.Call(
                    dbDataReader, RelationalTypeMapping.GetDataReaderMethod(bufferedColumnType), indexExpression);
                if (valueExpression.Type != columnType)
                {
                    valueExpression = Expression.Convert(valueExpression, columnType);
                }
            }

            valueExpression = typeMapping.CustomizeDataReaderExpression(valueExpression);

            var converter = typeMapping.Converter;

            if (converter != null)
            {
                if (valueExpression.Type != converter.ProviderClrType)
                {
                    valueExpression = Expression.Convert(valueExpression, converter.ProviderClrType);
                }

                valueExpression = ReplacingExpressionVisitor.Replace(
                    converter.ConvertFromProviderExpression.Parameters.Single(),
                    valueExpression,
                    converter.ConvertFromProviderExpression.Body);
            }

            if (valueExpression.Type != type)
            {
                valueExpression = Expression.Convert(valueExpression, type);
            }

            if (nullable)
            {
                Expression replaceExpression;
                if (converter?.ConvertsNulls == true)
                {
                    replaceExpression = ReplacingExpressionVisitor.Replace(
                        converter.ConvertFromProviderExpression.Parameters.Single(),
                        Expression.Default(converter.ProviderClrType),
                        converter.ConvertFromProviderExpression.Body);

                    if (replaceExpression.Type != type)
                    {
                        replaceExpression = Expression.Convert(replaceExpression, type);
                    }
                }
                else
                {
                    replaceExpression = Expression.Default(valueExpression.Type);
                }

                valueExpression = Expression.Condition(
                    Expression.Call(dbDataReader, IsDbNullMethod, indexExpression),
                    replaceExpression,
                    valueExpression);
            }

            if (_detailedErrorsEnabled
                && !buffering)
            {
                var exceptionParameter = Expression.Parameter(typeof(Exception), name: "e");

                var catchBlock = Expression.Catch(
                    exceptionParameter,
                    Expression.Call(
                        ThrowReadValueExceptionMethod.MakeGenericMethod(valueExpression.Type),
                        exceptionParameter,
                        Expression.Call(dbDataReader, GetFieldValueMethod.MakeGenericMethod(typeof(object)), indexExpression),
                        Expression.Constant(valueExpression.Type.MakeNullable(nullable), typeof(Type)),
                        Expression.Constant(property, typeof(IPropertyBase))));

                valueExpression = Expression.TryCatch(valueExpression, catchBlock);
            }

            return valueExpression;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TValue ThrowReadValueException<TValue>(
            Exception exception,
            object? value,
            Type expectedType,
            IPropertyBase? property = null)
        {
            var actualType = value?.GetType();

            string message;

            if (property != null)
            {
                var entityType = property.DeclaringType.DisplayName();
                var propertyName = property.Name;
                if (expectedType == typeof(object))
                {
                    expectedType = property.ClrType;
                }

                message = exception is NullReferenceException
                    || Equals(value, DBNull.Value)
                        ? RelationalStrings.ErrorMaterializingPropertyNullReference(entityType, propertyName, expectedType)
                        : exception is InvalidCastException
                            ? CoreStrings.ErrorMaterializingPropertyInvalidCast(entityType, propertyName, expectedType, actualType)
                            : RelationalStrings.ErrorMaterializingProperty(entityType, propertyName);
            }
            else
            {
                message = exception is NullReferenceException
                    || Equals(value, DBNull.Value)
                        ? RelationalStrings.ErrorMaterializingValueNullReference(expectedType)
                        : exception is InvalidCastException
                            ? RelationalStrings.ErrorMaterializingValueInvalidCast(expectedType, actualType)
                            : RelationalStrings.ErrorMaterializingValue;
            }

            throw new InvalidOperationException(message, exception);
        }

        private static object? ExtractJsonProperty(JsonElement element, string propertyName, Type returnType)
        {
            var jsonElementProperty = element.GetProperty(propertyName);

            return jsonElementProperty.Deserialize(returnType);
        }

        private static void IncludeReference<TEntity, TIncludingEntity, TIncludedEntity>(
            QueryContext queryContext,
            TEntity entity,
            TIncludedEntity? relatedEntity,
            INavigationBase navigation,
            INavigationBase? inverseNavigation,
            Action<TIncludingEntity, TIncludedEntity> fixup,
            bool trackingQuery)
            where TEntity : class
            where TIncludingEntity : class, TEntity
            where TIncludedEntity : class
        {
            if (entity is TIncludingEntity includingEntity)
            {
                if (trackingQuery
                    && navigation.DeclaringEntityType.FindPrimaryKey() != null)
                {
                    // For non-null relatedEntity StateManager will set the flag
                    if (relatedEntity == null)
                    {
                        queryContext.SetNavigationIsLoaded(includingEntity, navigation);
                    }
                }
                else
                {
                    navigation.SetIsLoadedWhenNoTracking(includingEntity);
                    if (relatedEntity != null)
                    {
                        fixup(includingEntity, relatedEntity);
                        if (inverseNavigation != null
                            && !inverseNavigation.IsCollection)
                        {
                            inverseNavigation.SetIsLoadedWhenNoTracking(relatedEntity);
                        }
                    }
                }
            }
        }

        private static void InitializeIncludeCollection<TParent, TNavigationEntity>(
            int collectionId,
            QueryContext queryContext,
            DbDataReader dbDataReader,
            SingleQueryResultCoordinator resultCoordinator,
            TParent entity,
            Func<QueryContext, DbDataReader, object[]> parentIdentifier,
            Func<QueryContext, DbDataReader, object[]> outerIdentifier,
            INavigationBase navigation,
            IClrCollectionAccessor clrCollectionAccessor,
            bool trackingQuery,
            bool setLoaded)
            where TParent : class
            where TNavigationEntity : class, TParent
        {
            object? collection = null;
            if (entity is TNavigationEntity)
            {
                if (setLoaded)
                {
                    if (trackingQuery)
                    {
                        queryContext.SetNavigationIsLoaded(entity, navigation);
                    }
                    else
                    {
                        navigation.SetIsLoadedWhenNoTracking(entity);
                    }
                }

                collection = clrCollectionAccessor.GetOrCreate(entity, forMaterialization: true);
            }

            var parentKey = parentIdentifier(queryContext, dbDataReader);
            var outerKey = outerIdentifier(queryContext, dbDataReader);

            var collectionMaterializationContext = new SingleQueryCollectionContext(entity, collection, parentKey, outerKey);

            resultCoordinator.SetSingleQueryCollectionContext(collectionId, collectionMaterializationContext);
        }

        private static void PopulateIncludeCollection<TIncludingEntity, TIncludedEntity>(
            int collectionId,
            QueryContext queryContext,
            DbDataReader dbDataReader,
            SingleQueryResultCoordinator resultCoordinator,
            Func<QueryContext, DbDataReader, object[]> parentIdentifier,
            Func<QueryContext, DbDataReader, object[]> outerIdentifier,
            Func<QueryContext, DbDataReader, object[]> selfIdentifier,
            IReadOnlyList<ValueComparer> parentIdentifierValueComparers,
            IReadOnlyList<ValueComparer> outerIdentifierValueComparers,
            IReadOnlyList<ValueComparer> selfIdentifierValueComparers,
            Func<QueryContext, DbDataReader, ResultContext, SingleQueryResultCoordinator, TIncludedEntity> innerShaper,
            INavigationBase? inverseNavigation,
            Action<TIncludingEntity, TIncludedEntity> fixup,
            bool trackingQuery)
            where TIncludingEntity : class
            where TIncludedEntity : class
        {
            var collectionMaterializationContext = resultCoordinator.Collections[collectionId]!;
            if (collectionMaterializationContext.Parent is TIncludingEntity entity)
            {
                if (resultCoordinator.HasNext == false)
                {
                    // Outer Enumerator has ended
                    GenerateCurrentElementIfPending();
                    return;
                }

                if (!CompareIdentifiers(
                        outerIdentifierValueComparers,
                        outerIdentifier(queryContext, dbDataReader), collectionMaterializationContext.OuterIdentifier))
                {
                    // Outer changed so collection has ended. Materialize last element.
                    GenerateCurrentElementIfPending();
                    // If parent also changed then this row is now pointing to element of next collection
                    if (!CompareIdentifiers(
                            parentIdentifierValueComparers,
                            parentIdentifier(queryContext, dbDataReader), collectionMaterializationContext.ParentIdentifier))
                    {
                        resultCoordinator.HasNext = true;
                    }

                    return;
                }

                var innerKey = selfIdentifier(queryContext, dbDataReader);
                if (innerKey.All(e => e == null))
                {
                    // No correlated element
                    return;
                }

                if (collectionMaterializationContext.SelfIdentifier != null)
                {
                    if (CompareIdentifiers(selfIdentifierValueComparers, innerKey, collectionMaterializationContext.SelfIdentifier))
                    {
                        // repeated row for current element
                        // If it is pending materialization then it may have nested elements
                        if (collectionMaterializationContext.ResultContext.Values != null)
                        {
                            ProcessCurrentElementRow();
                        }

                        resultCoordinator.ResultReady = false;
                        return;
                    }

                    // Row for new element which is not first element
                    // So materialize the element
                    GenerateCurrentElementIfPending();
                    resultCoordinator.HasNext = null;
                    collectionMaterializationContext.UpdateSelfIdentifier(innerKey);
                }
                else
                {
                    // First row for current element
                    collectionMaterializationContext.UpdateSelfIdentifier(innerKey);
                }

                ProcessCurrentElementRow();
                resultCoordinator.ResultReady = false;
            }

            void ProcessCurrentElementRow()
            {
                var previousResultReady = resultCoordinator.ResultReady;
                resultCoordinator.ResultReady = true;
                var relatedEntity = innerShaper(
                    queryContext, dbDataReader, collectionMaterializationContext.ResultContext, resultCoordinator);
                if (resultCoordinator.ResultReady)
                {
                    // related entity is materialized
                    collectionMaterializationContext.ResultContext.Values = null;
                    if (!trackingQuery)
                    {
                        fixup(entity, relatedEntity);
                        if (inverseNavigation != null)
                        {
                            inverseNavigation.SetIsLoadedWhenNoTracking(relatedEntity);
                        }
                    }
                }

                resultCoordinator.ResultReady &= previousResultReady;
            }

            void GenerateCurrentElementIfPending()
            {
                if (collectionMaterializationContext.ResultContext.Values != null)
                {
                    resultCoordinator.HasNext = false;
                    ProcessCurrentElementRow();
                }

                collectionMaterializationContext.UpdateSelfIdentifier(null);
            }
        }

        private static void InitializeSplitIncludeCollection<TParent, TNavigationEntity>(
            int collectionId,
            QueryContext queryContext,
            DbDataReader parentDataReader,
            SplitQueryResultCoordinator resultCoordinator,
            TParent entity,
            Func<QueryContext, DbDataReader, object[]> parentIdentifier,
            INavigationBase navigation,
            IClrCollectionAccessor clrCollectionAccessor,
            bool trackingQuery,
            bool setLoaded)
            where TParent : class
            where TNavigationEntity : class, TParent
        {
            object? collection = null;
            if (entity is TNavigationEntity)
            {
                if (setLoaded)
                {
                    if (trackingQuery)
                    {
                        queryContext.SetNavigationIsLoaded(entity, navigation);
                    }
                    else
                    {
                        navigation.SetIsLoadedWhenNoTracking(entity);
                    }
                }

                collection = clrCollectionAccessor.GetOrCreate(entity, forMaterialization: true);
            }

            var parentKey = parentIdentifier(queryContext, parentDataReader);

            var splitQueryCollectionContext = new SplitQueryCollectionContext(entity, collection, parentKey);

            resultCoordinator.SetSplitQueryCollectionContext(collectionId, splitQueryCollectionContext);
        }

        private static void PopulateSplitIncludeCollection<TIncludingEntity, TIncludedEntity>(
            int collectionId,
            RelationalQueryContext queryContext,
            IExecutionStrategy executionStrategy,
            RelationalCommandCache relationalCommandCache,
            IReadOnlyList<ReaderColumn?>? readerColumns,
            bool detailedErrorsEnabled,
            SplitQueryResultCoordinator resultCoordinator,
            Func<QueryContext, DbDataReader, object[]> childIdentifier,
            IReadOnlyList<ValueComparer> identifierValueComparers,
            Func<QueryContext, DbDataReader, ResultContext, SplitQueryResultCoordinator, TIncludedEntity> innerShaper,
            Action<QueryContext, IExecutionStrategy, SplitQueryResultCoordinator>? relatedDataLoaders,
            INavigationBase? inverseNavigation,
            Action<TIncludingEntity, TIncludedEntity> fixup,
            bool trackingQuery)
            where TIncludingEntity : class
            where TIncludedEntity : class
        {
            if (resultCoordinator.DataReaders.Count <= collectionId
                || resultCoordinator.DataReaders[collectionId] == null)
            {
                // Execute and fetch data reader
                var dataReader = executionStrategy.Execute(
                    (queryContext, relationalCommandCache, readerColumns, detailedErrorsEnabled),
                    ((RelationalQueryContext, RelationalCommandCache, IReadOnlyList<ReaderColumn?>?, bool) tup)
                        => InitializeReader(tup.Item1, tup.Item2, tup.Item3, tup.Item4),
                    verifySucceeded: null);

                static RelationalDataReader InitializeReader(
                    RelationalQueryContext queryContext,
                    RelationalCommandCache relationalCommandCache,
                    IReadOnlyList<ReaderColumn?>? readerColumns,
                    bool detailedErrorsEnabled)
                {
                    var relationalCommand = relationalCommandCache.RentAndPopulateRelationalCommand(queryContext);

                    return relationalCommand.ExecuteReader(
                        new RelationalCommandParameterObject(
                            queryContext.Connection,
                            queryContext.ParameterValues,
                            readerColumns,
                            queryContext.Context,
                            queryContext.CommandLogger,
                            detailedErrorsEnabled, CommandSource.LinqQuery));
                }

                resultCoordinator.SetDataReader(collectionId, dataReader);
            }

            var splitQueryCollectionContext = resultCoordinator.Collections[collectionId]!;
            var dataReaderContext = resultCoordinator.DataReaders[collectionId]!;
            var dbDataReader = dataReaderContext.DataReader.DbDataReader;
            if (splitQueryCollectionContext.Parent is TIncludingEntity entity)
            {
                while (dataReaderContext.HasNext ?? dbDataReader.Read())
                {
                    if (!CompareIdentifiers(
                            identifierValueComparers,
                            splitQueryCollectionContext.ParentIdentifier, childIdentifier(queryContext, dbDataReader)))
                    {
                        dataReaderContext.HasNext = true;

                        return;
                    }

                    dataReaderContext.HasNext = null;
                    splitQueryCollectionContext.ResultContext.Values = null;

                    innerShaper(queryContext, dbDataReader, splitQueryCollectionContext.ResultContext, resultCoordinator);
                    relatedDataLoaders?.Invoke(queryContext, executionStrategy, resultCoordinator);
                    var relatedEntity = innerShaper(
                        queryContext, dbDataReader, splitQueryCollectionContext.ResultContext, resultCoordinator);

                    if (!trackingQuery)
                    {
                        fixup(entity, relatedEntity);
                        inverseNavigation?.SetIsLoadedWhenNoTracking(relatedEntity);
                    }
                }

                dataReaderContext.HasNext = false;
            }
        }

        private static async Task PopulateSplitIncludeCollectionAsync<TIncludingEntity, TIncludedEntity>(
            int collectionId,
            RelationalQueryContext queryContext,
            IExecutionStrategy executionStrategy,
            RelationalCommandCache relationalCommandCache,
            IReadOnlyList<ReaderColumn?>? readerColumns,
            bool detailedErrorsEnabled,
            SplitQueryResultCoordinator resultCoordinator,
            Func<QueryContext, DbDataReader, object[]> childIdentifier,
            IReadOnlyList<ValueComparer> identifierValueComparers,
            Func<QueryContext, DbDataReader, ResultContext, SplitQueryResultCoordinator, TIncludedEntity> innerShaper,
            Func<QueryContext, IExecutionStrategy, SplitQueryResultCoordinator, Task>? relatedDataLoaders,
            INavigationBase? inverseNavigation,
            Action<TIncludingEntity, TIncludedEntity> fixup,
            bool trackingQuery)
            where TIncludingEntity : class
            where TIncludedEntity : class
        {
            if (resultCoordinator.DataReaders.Count <= collectionId
                || resultCoordinator.DataReaders[collectionId] == null)
            {
                // Execute and fetch data reader
                var dataReader = await executionStrategy.ExecuteAsync(
                        (queryContext, relationalCommandCache, readerColumns, detailedErrorsEnabled),
                        ((RelationalQueryContext, RelationalCommandCache, IReadOnlyList<ReaderColumn?>?, bool) tup, CancellationToken cancellationToken)
                            => InitializeReaderAsync(tup.Item1, tup.Item2, tup.Item3, tup.Item4, cancellationToken),
                        verifySucceeded: null,
                        queryContext.CancellationToken)
                    .ConfigureAwait(false);

                static async Task<RelationalDataReader> InitializeReaderAsync(
                    RelationalQueryContext queryContext,
                    RelationalCommandCache relationalCommandCache,
                    IReadOnlyList<ReaderColumn?>? readerColumns,
                    bool detailedErrorsEnabled,
                    CancellationToken cancellationToken)
                {
                    var relationalCommand = relationalCommandCache.RentAndPopulateRelationalCommand(queryContext);

                    return await relationalCommand.ExecuteReaderAsync(
                            new RelationalCommandParameterObject(
                                queryContext.Connection,
                                queryContext.ParameterValues,
                                readerColumns,
                                queryContext.Context,
                                queryContext.CommandLogger,
                                detailedErrorsEnabled,
                                CommandSource.LinqQuery),
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                resultCoordinator.SetDataReader(collectionId, dataReader);
            }

            var splitQueryCollectionContext = resultCoordinator.Collections[collectionId]!;
            var dataReaderContext = resultCoordinator.DataReaders[collectionId]!;
            var dbDataReader = dataReaderContext.DataReader.DbDataReader;
            if (splitQueryCollectionContext.Parent is TIncludingEntity entity)
            {
                while (dataReaderContext.HasNext ?? await dbDataReader.ReadAsync(queryContext.CancellationToken).ConfigureAwait(false))
                {
                    if (!CompareIdentifiers(
                            identifierValueComparers,
                            splitQueryCollectionContext.ParentIdentifier, childIdentifier(queryContext, dbDataReader)))
                    {
                        dataReaderContext.HasNext = true;

                        return;
                    }

                    dataReaderContext.HasNext = null;
                    splitQueryCollectionContext.ResultContext.Values = null;

                    innerShaper(queryContext, dbDataReader, splitQueryCollectionContext.ResultContext, resultCoordinator);
                    if (relatedDataLoaders != null)
                    {
                        await relatedDataLoaders(queryContext, executionStrategy, resultCoordinator).ConfigureAwait(false);
                    }

                    var relatedEntity = innerShaper(
                        queryContext, dbDataReader, splitQueryCollectionContext.ResultContext, resultCoordinator);

                    if (!trackingQuery)
                    {
                        fixup(entity, relatedEntity);
                        inverseNavigation?.SetIsLoadedWhenNoTracking(relatedEntity);
                    }
                }

                dataReaderContext.HasNext = false;
            }
        }

        private static TCollection InitializeCollection<TElement, TCollection>(
            int collectionId,
            QueryContext queryContext,
            DbDataReader dbDataReader,
            SingleQueryResultCoordinator resultCoordinator,
            Func<QueryContext, DbDataReader, object[]> parentIdentifier,
            Func<QueryContext, DbDataReader, object[]> outerIdentifier,
            IClrCollectionAccessor? clrCollectionAccessor)
            where TCollection : class, ICollection<TElement>
        {
            var collection = clrCollectionAccessor?.Create() ?? new List<TElement>();

            var parentKey = parentIdentifier(queryContext, dbDataReader);
            var outerKey = outerIdentifier(queryContext, dbDataReader);

            var collectionMaterializationContext = new SingleQueryCollectionContext(null, collection, parentKey, outerKey);

            resultCoordinator.SetSingleQueryCollectionContext(collectionId, collectionMaterializationContext);

            return (TCollection)collection;
        }

        private static void PopulateCollection<TCollection, TElement, TRelatedEntity>(
            int collectionId,
            QueryContext queryContext,
            DbDataReader dbDataReader,
            SingleQueryResultCoordinator resultCoordinator,
            Func<QueryContext, DbDataReader, object[]> parentIdentifier,
            Func<QueryContext, DbDataReader, object[]> outerIdentifier,
            Func<QueryContext, DbDataReader, object[]> selfIdentifier,
            IReadOnlyList<ValueComparer> parentIdentifierValueComparers,
            IReadOnlyList<ValueComparer> outerIdentifierValueComparers,
            IReadOnlyList<ValueComparer> selfIdentifierValueComparers,
            Func<QueryContext, DbDataReader, ResultContext, SingleQueryResultCoordinator, TRelatedEntity> innerShaper)
            where TRelatedEntity : TElement
            where TCollection : class, ICollection<TElement>
        {
            var collectionMaterializationContext = resultCoordinator.Collections[collectionId]!;
            if (collectionMaterializationContext.Collection is null)
            {
                // nothing to materialize since no collection created
                return;
            }

            if (resultCoordinator.HasNext == false)
            {
                // Outer Enumerator has ended
                GenerateCurrentElementIfPending();
                return;
            }

            if (!CompareIdentifiers(
                    outerIdentifierValueComparers,
                    outerIdentifier(queryContext, dbDataReader), collectionMaterializationContext.OuterIdentifier))
            {
                // Outer changed so collection has ended. Materialize last element.
                GenerateCurrentElementIfPending();
                // If parent also changed then this row is now pointing to element of next collection
                if (!CompareIdentifiers(
                        parentIdentifierValueComparers,
                        parentIdentifier(queryContext, dbDataReader), collectionMaterializationContext.ParentIdentifier))
                {
                    resultCoordinator.HasNext = true;
                }

                return;
            }

            var innerKey = selfIdentifier(queryContext, dbDataReader);
            if (innerKey.Length > 0 && innerKey.All(e => e == null))
            {
                // No correlated element
                return;
            }

            if (collectionMaterializationContext.SelfIdentifier != null)
            {
                if (CompareIdentifiers(
                        selfIdentifierValueComparers,
                        innerKey, collectionMaterializationContext.SelfIdentifier))
                {
                    // repeated row for current element
                    // If it is pending materialization then it may have nested elements
                    if (collectionMaterializationContext.ResultContext.Values != null)
                    {
                        ProcessCurrentElementRow();
                    }

                    resultCoordinator.ResultReady = false;
                    return;
                }

                // Row for new element which is not first element
                // So materialize the element
                GenerateCurrentElementIfPending();
                resultCoordinator.HasNext = null;
                collectionMaterializationContext.UpdateSelfIdentifier(innerKey);
            }
            else
            {
                // First row for current element
                collectionMaterializationContext.UpdateSelfIdentifier(innerKey);
            }

            ProcessCurrentElementRow();
            resultCoordinator.ResultReady = false;

            void ProcessCurrentElementRow()
            {
                var previousResultReady = resultCoordinator.ResultReady;
                resultCoordinator.ResultReady = true;
                var element = innerShaper(
                    queryContext, dbDataReader, collectionMaterializationContext.ResultContext, resultCoordinator);
                if (resultCoordinator.ResultReady)
                {
                    // related element is materialized
                    collectionMaterializationContext.ResultContext.Values = null;
                    ((TCollection)collectionMaterializationContext.Collection).Add(element);
                }

                resultCoordinator.ResultReady &= previousResultReady;
            }

            void GenerateCurrentElementIfPending()
            {
                if (collectionMaterializationContext.ResultContext.Values != null)
                {
                    resultCoordinator.HasNext = false;
                    ProcessCurrentElementRow();
                }

                collectionMaterializationContext.UpdateSelfIdentifier(null);
            }
        }

        private static TCollection InitializeSplitCollection<TElement, TCollection>(
            int collectionId,
            QueryContext queryContext,
            DbDataReader parentDataReader,
            SplitQueryResultCoordinator resultCoordinator,
            Func<QueryContext, DbDataReader, object[]> parentIdentifier,
            IClrCollectionAccessor? clrCollectionAccessor)
            where TCollection : class, ICollection<TElement>
        {
            var collection = clrCollectionAccessor?.Create() ?? new List<TElement>();
            var parentKey = parentIdentifier(queryContext, parentDataReader);
            var splitQueryCollectionContext = new SplitQueryCollectionContext(null, collection, parentKey);

            resultCoordinator.SetSplitQueryCollectionContext(collectionId, splitQueryCollectionContext);

            return (TCollection)collection;
        }

        private static void PopulateSplitCollection<TCollection, TElement, TRelatedEntity>(
            int collectionId,
            RelationalQueryContext queryContext,
            IExecutionStrategy executionStrategy,
            RelationalCommandCache relationalCommandCache,
            IReadOnlyList<ReaderColumn?>? readerColumns,
            bool detailedErrorsEnabled,
            SplitQueryResultCoordinator resultCoordinator,
            Func<QueryContext, DbDataReader, object[]> childIdentifier,
            IReadOnlyList<ValueComparer> identifierValueComparers,
            Func<QueryContext, DbDataReader, ResultContext, SplitQueryResultCoordinator, TRelatedEntity> innerShaper,
            Action<QueryContext, IExecutionStrategy, SplitQueryResultCoordinator>? relatedDataLoaders)
            where TRelatedEntity : TElement
            where TCollection : class, ICollection<TElement>
        {
            if (resultCoordinator.DataReaders.Count <= collectionId
                || resultCoordinator.DataReaders[collectionId] == null)
            {
                // Execute and fetch data reader
                var dataReader = executionStrategy.Execute(
                    (queryContext, relationalCommandCache, readerColumns, detailedErrorsEnabled),
                    ((RelationalQueryContext, RelationalCommandCache, IReadOnlyList<ReaderColumn?>?, bool) tup)
                        => InitializeReader(tup.Item1, tup.Item2, tup.Item3, tup.Item4),
                    verifySucceeded: null);

                static RelationalDataReader InitializeReader(
                    RelationalQueryContext queryContext,
                    RelationalCommandCache relationalCommandCache,
                    IReadOnlyList<ReaderColumn?>? readerColumns,
                    bool detailedErrorsEnabled)
                {
                    var relationalCommand = relationalCommandCache.RentAndPopulateRelationalCommand(queryContext);

                    return relationalCommand.ExecuteReader(
                        new RelationalCommandParameterObject(
                            queryContext.Connection,
                            queryContext.ParameterValues,
                            readerColumns,
                            queryContext.Context,
                            queryContext.CommandLogger,
                            detailedErrorsEnabled, CommandSource.LinqQuery));
                }

                resultCoordinator.SetDataReader(collectionId, dataReader);
            }

            var splitQueryCollectionContext = resultCoordinator.Collections[collectionId]!;
            var dataReaderContext = resultCoordinator.DataReaders[collectionId]!;
            var dbDataReader = dataReaderContext.DataReader.DbDataReader;
            if (splitQueryCollectionContext.Collection is null)
            {
                // nothing to materialize since no collection created
                return;
            }

            while (dataReaderContext.HasNext ?? dbDataReader.Read())
            {
                if (!CompareIdentifiers(
                        identifierValueComparers,
                        splitQueryCollectionContext.ParentIdentifier, childIdentifier(queryContext, dbDataReader)))
                {
                    dataReaderContext.HasNext = true;

                    return;
                }

                dataReaderContext.HasNext = null;
                splitQueryCollectionContext.ResultContext.Values = null;

                innerShaper(queryContext, dbDataReader, splitQueryCollectionContext.ResultContext, resultCoordinator);
                relatedDataLoaders?.Invoke(queryContext, executionStrategy, resultCoordinator);
                var relatedElement = innerShaper(
                    queryContext, dbDataReader, splitQueryCollectionContext.ResultContext, resultCoordinator);
                ((TCollection)splitQueryCollectionContext.Collection).Add(relatedElement);
            }

            dataReaderContext.HasNext = false;
        }

        private static async Task PopulateSplitCollectionAsync<TCollection, TElement, TRelatedEntity>(
            int collectionId,
            RelationalQueryContext queryContext,
            IExecutionStrategy executionStrategy,
            RelationalCommandCache relationalCommandCache,
            IReadOnlyList<ReaderColumn?>? readerColumns,
            bool detailedErrorsEnabled,
            SplitQueryResultCoordinator resultCoordinator,
            Func<QueryContext, DbDataReader, object[]> childIdentifier,
            IReadOnlyList<ValueComparer> identifierValueComparers,
            Func<QueryContext, DbDataReader, ResultContext, SplitQueryResultCoordinator, TRelatedEntity> innerShaper,
            Func<QueryContext, IExecutionStrategy, SplitQueryResultCoordinator, Task>? relatedDataLoaders)
            where TRelatedEntity : TElement
            where TCollection : class, ICollection<TElement>
        {
            if (resultCoordinator.DataReaders.Count <= collectionId
                || resultCoordinator.DataReaders[collectionId] == null)
            {
                // Execute and fetch data reader
                var dataReader = await executionStrategy.ExecuteAsync(
                        (queryContext, relationalCommandCache, readerColumns, detailedErrorsEnabled),
                        ((RelationalQueryContext, RelationalCommandCache, IReadOnlyList<ReaderColumn?>?, bool) tup, CancellationToken cancellationToken)
                            => InitializeReaderAsync(tup.Item1, tup.Item2, tup.Item3, tup.Item4, cancellationToken),
                        verifySucceeded: null,
                        queryContext.CancellationToken)
                    .ConfigureAwait(false);

                static async Task<RelationalDataReader> InitializeReaderAsync(
                    RelationalQueryContext queryContext,
                    RelationalCommandCache relationalCommandCache,
                    IReadOnlyList<ReaderColumn?>? readerColumns,
                    bool detailedErrorsEnabled,
                    CancellationToken cancellationToken)
                {
                    var relationalCommand = relationalCommandCache.RentAndPopulateRelationalCommand(queryContext);

                    return await relationalCommand.ExecuteReaderAsync(
                            new RelationalCommandParameterObject(
                                queryContext.Connection,
                                queryContext.ParameterValues,
                                readerColumns,
                                queryContext.Context,
                                queryContext.CommandLogger,
                                detailedErrorsEnabled,
                                CommandSource.LinqQuery),
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                resultCoordinator.SetDataReader(collectionId, dataReader);
            }

            var splitQueryCollectionContext = resultCoordinator.Collections[collectionId]!;
            var dataReaderContext = resultCoordinator.DataReaders[collectionId]!;
            var dbDataReader = dataReaderContext.DataReader.DbDataReader;
            if (splitQueryCollectionContext.Collection is null)
            {
                // nothing to materialize since no collection created
                return;
            }

            while (dataReaderContext.HasNext ?? await dbDataReader.ReadAsync(queryContext.CancellationToken).ConfigureAwait(false))
            {
                if (!CompareIdentifiers(
                        identifierValueComparers,
                        splitQueryCollectionContext.ParentIdentifier, childIdentifier(queryContext, dbDataReader)))
                {
                    dataReaderContext.HasNext = true;

                    return;
                }

                dataReaderContext.HasNext = null;
                splitQueryCollectionContext.ResultContext.Values = null;

                innerShaper(queryContext, dbDataReader, splitQueryCollectionContext.ResultContext, resultCoordinator);
                if (relatedDataLoaders != null)
                {
                    await relatedDataLoaders(queryContext, executionStrategy, resultCoordinator).ConfigureAwait(false);
                }

                var relatedElement = innerShaper(
                    queryContext, dbDataReader, splitQueryCollectionContext.ResultContext, resultCoordinator);
                ((TCollection)splitQueryCollectionContext.Collection).Add(relatedElement);
            }

            dataReaderContext.HasNext = false;
        }

        private static void IncludeJsonEntityReference<TIncludingEntity, TIncludedEntity>(
            QueryContext queryContext,
            JsonElement? jsonElement,
            object[] keyPropertyValues,
            TIncludingEntity entity,
            bool optionalDependent,
            Func<QueryContext, object[], JsonElement, TIncludedEntity> innerShaper,
            Action<TIncludingEntity, TIncludedEntity> fixup)
            where TIncludingEntity : class
            where TIncludedEntity : class
        {
            if (jsonElement.HasValue)
            {
                if (jsonElement.Value.ValueKind == JsonValueKind.Null)
                {
                    if (optionalDependent)
                    {
                        return;
                    }
                    else
                    {
                        // TODO: resource string
                        throw new InvalidOperationException("Required Json entity not found.");
                    }
                }
                else
                {
                    var included = innerShaper(queryContext, keyPropertyValues, jsonElement.Value);
                    fixup(entity, included);
                }
            }
        }

        private static void IncludeJsonEntityCollection<TIncludingEntity, TIncludedCollectionElement>(
            QueryContext queryContext,
            JsonElement? jsonElement,
            object[] keyPropertyValues,
            TIncludingEntity entity,
            Func<QueryContext, object[], JsonElement, TIncludedCollectionElement> innerShaper,
            Action<TIncludingEntity, TIncludedCollectionElement> fixup)
            where TIncludingEntity : class
            where TIncludedCollectionElement : class
        {
            if (jsonElement.HasValue)
            {
                var newKeyPropertyValues = new object[keyPropertyValues.Length + 1];
                Array.Copy(keyPropertyValues, newKeyPropertyValues, keyPropertyValues.Length);

                var i = 0;
                foreach (var jsonArrayElement in jsonElement.Value.EnumerateArray())
                {
                    newKeyPropertyValues[^1] = ++i;

                    var resultElement = innerShaper(queryContext, newKeyPropertyValues, jsonArrayElement);

                    fixup(entity, resultElement);
                }
            }
        }

        private static TEntity? MaterializeJsonEntity<TEntity>(
            QueryContext queryContext,
            JsonElement? jsonElement,
            bool nullable,
            object[] keyPropertyValues,
            Func<QueryContext, object[], JsonElement, TEntity> shaper)
            where TEntity : class
        {
            if (jsonElement.HasValue)
            {
                var result = shaper(queryContext, keyPropertyValues, jsonElement.Value);

                return result;
            }

            if (nullable)
            {
                return default(TEntity);
            }

            // TODO: resource string
            throw new InvalidOperationException("Entity is not nullable but json is null.");
        }

        private static TResult? MaterializeJsonEntityCollection<TEntity, TResult>(
            QueryContext queryContext,
            JsonElement? jsonElement,
            object[] keyPropertyValues,
            INavigationBase navigation,
            Func<QueryContext, object[], JsonElement, TEntity> innerShaper)
            where TEntity : class
            where TResult : ICollection<TEntity>
        {
            if (jsonElement.HasValue)
            {
                var collectionAccessor = navigation.GetCollectionAccessor();
                var result = (TResult)collectionAccessor!.Create();

                var newKeyPropertyValues = new object[keyPropertyValues.Length + 1];
                Array.Copy(keyPropertyValues, newKeyPropertyValues, keyPropertyValues.Length);

                var i = 0;
                foreach (var jsonArrayElement in jsonElement.Value.EnumerateArray())
                {
                    newKeyPropertyValues[^1] = ++i;

                    var resultElement = innerShaper(queryContext, newKeyPropertyValues, jsonArrayElement);

                    result.Add(resultElement);
                }

                return result;
            }

            return default(TResult);
        }

        private static async Task TaskAwaiter(Func<Task>[] taskFactories)
        {
            for (var i = 0; i < taskFactories.Length; i++)
            {
                await taskFactories[i]().ConfigureAwait(false);
            }
        }

        private static bool CompareIdentifiers(IReadOnlyList<ValueComparer> valueComparers, object[] left, object[] right)
        {
            // Ignoring size check on all for perf as they should be same unless bug in code.
            for (var i = 0; i < left.Length; i++)
            {
                if (!valueComparers[i].Equals(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class CollectionShaperFindingExpressionVisitor : ExpressionVisitor
        {
            private bool _containsCollection;

            public bool ContainsCollectionMaterialization(Expression expression)
            {
                _containsCollection = false;

                Visit(expression);

                return _containsCollection;
            }

            [return: NotNullIfNotNull("expression")]
            public override Expression? Visit(Expression? expression)
            {
                if (_containsCollection)
                {
                    return expression;
                }

                if (expression is RelationalCollectionShaperExpression
                    || expression is RelationalSplitCollectionShaperExpression)
                {
                    _containsCollection = true;

                    return expression;
                }

                return base.Visit(expression);
            }
        }

        private sealed class ExisitingJsonElementMapKeyComparer : IEqualityComparer<(int, string[])>
        {
            public bool Equals((int, string[]) x, (int, string[]) y)
                => x.Item1 == y.Item1 && x.Item2.Length == y.Item2.Length && x.Item2.SequenceEqual(y.Item2);

            public int GetHashCode([DisallowNull] (int, string[]) obj)
                => HashCode.Combine(obj.Item1, obj.Item2?.Length);
        }
    }
}

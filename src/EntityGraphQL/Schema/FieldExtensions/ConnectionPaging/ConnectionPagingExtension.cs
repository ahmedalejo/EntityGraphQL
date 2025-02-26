using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions
{
    /// <summary>
    /// Sets up a few extensions to modify a simple collection expression - db.Movies.OrderBy() into a connection paging graph
    /// </summary>
    public class ConnectionPagingExtension : BaseFieldExtension
    {
        private readonly int? defaultPageSize;
        private readonly int? maxPageSize;
        private Field edgesField;
        private ConnectionEdgeExtension edgesExtension;
        private List<IFieldExtension> extensions;
        private Type listType;
        private bool isQueryable;
        private Type returnType;
        /// <summary>
        /// This is the original expression that was defined in the schema - the collection
        /// UseConnectionPaging() basically moves it to originalField.edges
        /// </summary>
        private Expression originalFieldExpression;

        public ConnectionPagingExtension(int? defaultPageSize, int? maxPageSize)
        {
            this.defaultPageSize = defaultPageSize;
            this.maxPageSize = maxPageSize;
        }

        /// <summary>
        /// Configure the field for a connection style paging field. Do as much as we can here as it is only executed once.
        ///
        /// There are a few fun things happening.
        ///
        /// 1. In this extension we set up the field with the Connection<T> object graph using the constructor to implement most
        ///    of the fields
        /// 2. We set up an extension on this field.edges.node to capture the selection from the compiled query as node is the <T>
        ///    they are selecting fields from
        /// 3. We set up an extension of field.edges which using data from this extension (we get the context and the args) and the
        ///    field.edges.node Select() to build a EF compatible expression that only returns the fields asked for in edges.node
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="field"></param>
        public override void Configure(ISchemaProvider schema, Field field)
        {
            if (!field.Resolve.Type.IsEnumerableOrArray())
                throw new ArgumentException($"Expression for field {field.Name} must be a collection to use ConnectionPagingExtension. Found type {field.ReturnType.TypeDotnet}");

            // Make sure required types are in the schema
            if (!schema.HasType("PageInfo"))
                schema.AddType(typeof(ConnectionPageInfo), "PageInfo", "Metadata about a page of data").AddAllFields();
            var edgeName = $"{field.ReturnType.SchemaType.Name}Edge";
            listType = field.ReturnType.TypeDotnet.GetEnumerableOrArrayType();
            isQueryable = typeof(IQueryable).IsAssignableFrom(field.ReturnType.TypeDotnet);

            if (!schema.HasType(edgeName))
            {
                var edgeType = typeof(ConnectionEdge<>).MakeGenericType(listType);
                schema.AddType(edgeType, edgeName, "Metadata about an edge of page result").AddAllFields();
            }

            ISchemaType returnSchemaType;
            var connectionName = $"{field.ReturnType.SchemaType.Name}Connection";
            if (!schema.HasType(connectionName))
            {
                var type = typeof(Connection<>)
                    .MakeGenericType(listType);
                returnSchemaType = schema.AddType(type, connectionName, $"Metadata about a {field.ReturnType.SchemaType.Name} connection (paging over people)").AddAllFields();
            }
            else
            {
                returnSchemaType = schema.Type(connectionName);
            }
            returnType = returnSchemaType.TypeDotnet;

            field.UpdateReturnType(SchemaBuilder.MakeGraphQlType(schema, returnType, connectionName));

            // Update field arguments
            field.AddArguments(new ConnectionArgs());

            // Rebuild expression so all the fields and types are known
            // and get it ready for completion at runtime (we need to know the selection fields to complete)
            // it is built to reduce redundant repeated expressions. The whole thing ends up in a null check wrap
            // conceptually it does similar to below (using Demo context)
            // See Connection for implemention details of TotalCount and PageInfo
            // (ctx, arguments) => {
            //      var connection = new Connection<Person>(ctx.Actors.Select(a => a.Person).Count(), arguments)
            //      {
            //          Edges = ctx.Actors.Select(a => a.Person).OrderBy(a => a.Id)
            //              .Skip(GetSkipNumber(arguments))
            //              .Take(GetTakeNumber(arguments))
            //              <----- we insert Select() here so that we do not fetch the whole table if using EF
            //              .Select((a, idx) => new ConnectionEdge<Person> // this is from Enumerable and EF will run the above
            //              {
            //                  Node = a,
            //              })
            //      };
            //      if (connection == null)
            //          return null;
            //      return .... // does the select of only the Conneciton fields asked for

            // set up Extension on Edges.Node field to handle the Select() insertion
            edgesField = returnSchemaType.GetField("edges", null);
            // move expression
            edgesField.UpdateExpression(field.Resolve);
            // We steal any previous extensions as they were expected to work on the original Resolve which we moved to Edges
            extensions = field.Extensions.Take(field.Extensions.FindIndex(e => e is ConnectionPagingExtension)).ToList();
            field.Extensions = field.Extensions.Skip(extensions.Count).ToList();

            // We use this extension to update the Edges context by inserting the Select() which we get from the above extension
            edgesExtension = new ConnectionEdgeExtension(listType, isQueryable, extensions)
            {
                ArgumentParam = field.ArgumentParam
            };
            edgesField.AddExtension(edgesExtension);
        }

        private Expression BuildNewConnectionExpression(Field field, Type returnType, Expression resolve)
        {
            // totalCountExp gets executed once in the new Connection() {} and we can reuse it
            var totalCountExp = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Count", new Type[] { listType }, resolve);
            var expression = Expression.MemberInit(Expression.New(returnType.GetConstructor(new[] { totalCountExp.Type, field.ArgumentParam.Type }), totalCountExp, field.ArgumentParam));
            return expression;
        }

        public override Expression GetExpression(Field field, Expression expression, ParameterExpression argExpression, dynamic arguments, Expression context, ParameterReplacer parameterReplacer)
        {
            if (arguments.Before != null && arguments.After != null)
                throw new ArgumentException($"Field only supports either before or after being supplied, not both.");
            if (arguments.First != null && arguments.First < 0)
                throw new ArgumentException($"first argument can not be less than 0.");
            if (arguments.Last != null && arguments.Last < 0)
                throw new ArgumentException($"last argument can not be less than 0.");

            if (maxPageSize.HasValue)
            {
                if (arguments.First != null && arguments.First > maxPageSize.Value)
                    throw new ArgumentException($"first argument can not be greater than {maxPageSize.Value}.");
                if (arguments.Last != null && arguments.Last > maxPageSize.Value)
                    throw new ArgumentException($"last argument can not be greater than {maxPageSize.Value}.");
            }

            if (arguments.First == null && arguments.Last == null && defaultPageSize != null)
                arguments.First = defaultPageSize;

            // deserialize cursors here once (not many times in the fields)
            arguments.AfterNum = ConnectionHelper.DeserializeCursor(arguments.After);
            arguments.BeforeNum = ConnectionHelper.DeserializeCursor(arguments.Before);

            // we get the arguments at this level but need to use them on the edge field
            edgesExtension.ArgExpression = argExpression;

            // Here we now have the original context needed in our edges expression to use in the sub fields
            originalFieldExpression = parameterReplacer.Replace(expression, field.FieldParam, context);
            // we also can apply any extensions before us
            foreach (var extension in extensions)
            {
                originalFieldExpression = extension.GetExpression(field, originalFieldExpression, argExpression, arguments, context, parameterReplacer);
            }
            // update the edge field
            edgesField.UpdateExpression(originalFieldExpression);

            // we need to return new expressions here so all the types match processing further
            var fieldExpression = BuildNewConnectionExpression(field, returnType, originalFieldExpression);

            return fieldExpression;
        }
    }
}
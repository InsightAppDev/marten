using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Baseline;
using LamarCodeGeneration;
using LamarCodeGeneration.Frames;
using LamarCodeGeneration.Model;
using LamarCompiler;
using Marten.Internal.CodeGeneration;
using Marten.Storage;
using Marten.Storage.Metadata;
using Marten.Util;
using Npgsql;

namespace Marten.Events.V4Concept.CodeGeneration
{
    // TODO -- introduce constants for all the type names
    internal static class EventOperationCodeGenerator
    {
        private const string StreamStateSelectorTypeName = "GeneratedStreamStateQueryHandler";
        private const string InsertStreamOperationName = "GeneratedInsertStream";
        private const string UpdateStreamVersionOperationName = "GeneratedStreamVersionOperation";

        public static IEventSelector GenerateSelector(EventGraph events, ISerializer serializer)
        {
            var assembly = new GeneratedAssembly(new GenerationRules("Marten.Generated"));
            assembly.ReferenceAssembly(typeof(EventGraph).Assembly);

            var type = assembly.AddType("EventSelector", typeof(EventSelectorBase));

            var sync = type.MethodFor("Apply");
            var async = type.MethodFor("ApplyAsync");

            // The json data column has to go first
            var table = new EventsTable(events);
            var columns = table.SelectColumns();

            for (var i = 3; i < columns.Count; i++)
            {
                columns[i].GenerateSelectorCodeSync(sync, events, i);
                columns[i].GenerateSelectorCodeAsync(async, events, i);
            }

            var compiler = new AssemblyGenerator();
            compiler.ReferenceAssembly(typeof(AggregationTypeBuilder).Assembly);
            compiler.Compile(assembly);

            return (IEventSelector) Activator.CreateInstance(type.CompiledType, events, serializer);
        }

        public static IEventOperationBuilder GenerateOperationBuilder(EventGraph graph)
        {
            var assembly = new GeneratedAssembly(new GenerationRules("Marten.Generated"));
            assembly.ReferenceAssembly(typeof(EventGraph).Assembly);

            var builderType = assembly.AddType("EventOperatorBuilder", typeof(IEventOperationBuilder));
            buildAppendEventOperation(graph, assembly);

            builderType.MethodFor(nameof(IEventOperationBuilder.AppendEvent))
                .Frames.Code($"return new Marten.Generated.AppendEventOperation(stream, e);");

            buildInsertStream(builderType, assembly, graph);

            buildStreamQueryHandlerType(graph, assembly);

            buildQueryForStreamMethod(graph, builderType);

            buildUpdateStreamVersion(builderType, assembly, graph);


            var compiler = new AssemblyGenerator();
            compiler.ReferenceAssembly(typeof(AggregationTypeBuilder).Assembly);
            compiler.Compile(assembly);

            return (IEventOperationBuilder) Activator.CreateInstance(builderType.CompiledType);

        }

        private static void buildUpdateStreamVersion(GeneratedType builderType, GeneratedAssembly assembly, EventGraph graph)
        {
            var operationType = assembly.AddType(UpdateStreamVersionOperationName, typeof(UpdateStreamVersion));
            operationType.AllInjectedFields.Add(new InjectedField(typeof(EventStream)));

            var sql = $"update {graph.DatabaseSchemaName}.mt_streams set version = ? where id = ? and version = ?";
            if (graph.TenancyStyle == TenancyStyle.Conjoined)
            {
                sql += $" and {TenantIdColumn.Name} = ?";
            }

            var configureCommand = operationType.MethodFor("ConfigureCommand");

            configureCommand.Frames.Code($"var parameters = {{0}}.{nameof(CommandBuilder.AppendWithParameters)}(\"{sql}\");",
                Use.Type<CommandBuilder>());

            configureCommand.SetParameterFromMember<EventStream>(0, x => x.Version);

            if (graph.StreamIdentity == StreamIdentity.AsGuid)
            {
                configureCommand.SetParameterFromMember<EventStream>(1, x => x.Id);
            }
            else
            {
                configureCommand.SetParameterFromMember<EventStream>(1, x => x.Key);
            }

            configureCommand.SetParameterFromMember<EventStream>(2, x => x.ExpectedVersionOnServer);

            if (graph.TenancyStyle == TenancyStyle.Conjoined)
            {
                new TenantIdColumn().As<IStreamTableColumn>().GenerateAppendCode(configureCommand, 3);
            }

            builderType.MethodFor(nameof(IEventOperationBuilder.UpdateStreamVersion))
                .Frames.Code($"return new Marten.Generated.{UpdateStreamVersionOperationName}({{0}});",
                    Use.Type<EventStream>());
        }

        private static void buildQueryForStreamMethod(EventGraph graph, GeneratedType builderType)
        {
            var arguments = new List<string>();
            arguments.Add(graph.StreamIdentity == StreamIdentity.AsGuid
                ? $"stream.{nameof(EventStream.Id)}"
                : $"stream.{nameof(EventStream.Key)}");

            if (graph.TenancyStyle == TenancyStyle.Conjoined)
            {
                arguments.Add($"stream.{nameof(EventStream.TenantId)}");
            }

            builderType.MethodFor(nameof(IEventOperationBuilder.QueryForStream))
                .Frames.Code($"return new Marten.Generated.{StreamStateSelectorTypeName}({arguments.Join(", ")});");
        }

        private static void buildStreamQueryHandlerType(EventGraph graph, GeneratedAssembly assembly)
        {
            var streamQueryHandlerType =
                assembly.AddType(StreamStateSelectorTypeName, typeof(StreamStateQueryHandler));

            streamQueryHandlerType.AllInjectedFields.Add(graph.StreamIdentity == StreamIdentity.AsGuid
                ? new InjectedField(typeof(Guid), "streamId")
                : new InjectedField(typeof(string), "streamId"));

            buildConfigureCommandMethodForStreamState(graph, streamQueryHandlerType);

            var sync = streamQueryHandlerType.MethodFor("Resolve");
            var async = streamQueryHandlerType.MethodFor("ResolveAsync");


            sync.Frames.Add(new ConstructorFrame<StreamState>(() => new StreamState()));
            async.Frames.Add(new ConstructorFrame<StreamState>(() => new StreamState()));

            if (graph.StreamIdentity == StreamIdentity.AsGuid)
            {
                sync.AssignMemberFromReader<StreamState>(streamQueryHandlerType, 0, x => x.Id);
                async.AssignMemberFromReaderAsync<StreamState>(streamQueryHandlerType, 0, x => x.Id);
            }
            else
            {
                sync.AssignMemberFromReader<StreamState>(streamQueryHandlerType, 0, x => x.Key);
                async.AssignMemberFromReaderAsync<StreamState>(streamQueryHandlerType, 0, x => x.Key);
            }

            sync.AssignMemberFromReader<StreamState>(streamQueryHandlerType, 1, x => x.Version);
            async.AssignMemberFromReaderAsync<StreamState>(streamQueryHandlerType, 1, x => x.Version);

            sync.Frames.Call<StreamStateQueryHandler>(x => x.SetAggregateType(null, null, null), @call =>
            {
                @call.IsLocal = true;
            });

#pragma warning disable 4014
            async.Frames.Call<StreamStateQueryHandler>(x => x.SetAggregateTypeAsync(null, null, null, CancellationToken.None), @call =>
#pragma warning restore 4014
            {
                @call.IsLocal = true;
            });

            sync.AssignMemberFromReader<StreamState>(streamQueryHandlerType, 3, x => x.LastTimestamp);
            async.AssignMemberFromReaderAsync<StreamState>(streamQueryHandlerType, 3, x => x.LastTimestamp);

            sync.AssignMemberFromReader<StreamState>(streamQueryHandlerType, 4, x => x.Created);
            async.AssignMemberFromReaderAsync<StreamState>(streamQueryHandlerType, 4, x => x.Created);

            sync.Frames.Return(typeof(StreamState));
            async.Frames.Return(typeof(StreamState));


        }

        private static void buildConfigureCommandMethodForStreamState(EventGraph graph, GeneratedType streamQueryHandlerType)
        {
            var sql =
                $"select id, version, type, timestamp, created as timestamp from {graph.DatabaseSchemaName}.mt_streams where id = ?";
            if (graph.TenancyStyle == TenancyStyle.Conjoined)
            {
                streamQueryHandlerType.AllInjectedFields.Add(new InjectedField(typeof(string), "tenantId"));
                sql += $" and {TenantIdColumn.Name} = ?";
            }

            var configureCommand = streamQueryHandlerType.MethodFor("ConfigureCommand");
            configureCommand.Frames.Call<CommandBuilder>(x => x.AppendWithParameters(""), @call =>
            {
                @call.Arguments[0] = Constant.ForString(sql);
                @call.ReturnAction = ReturnAction.Initialize;
            });

            var idDbType = graph.StreamIdentity == StreamIdentity.AsGuid ? DbType.Guid : DbType.String;
            configureCommand.Frames.Code("{0}[0].Value = _streamId;", Use.Type<NpgsqlParameter[]>());
            configureCommand.Frames.Code("{0}[0].DbType = {1};", Use.Type<NpgsqlParameter[]>(), idDbType);

            if (graph.TenancyStyle == TenancyStyle.Conjoined)
            {
                configureCommand.Frames.Code("{0}[1].Value = _tenantId;", Use.Type<NpgsqlParameter[]>());
                configureCommand.Frames.Code("{0}[1].DbType = {1};", Use.Type<NpgsqlParameter[]>(), DbType.String);
            }
        }

        private static void buildAppendEventOperation(EventGraph graph, GeneratedAssembly assembly)
        {
            var operationType = assembly.AddType("AppendEventOperation", typeof(AppendEventOperationBase));

            var configure = operationType.MethodFor(nameof(AppendEventOperationBase.ConfigureCommand));
            configure.DerivedVariables.Add(new Variable(typeof(IEvent), nameof(AppendEventOperationBase.Event)));
            configure.DerivedVariables.Add(new Variable(typeof(EventStream), nameof(AppendEventOperationBase.Stream)));

            var columns = new EventsTable(graph).SelectColumns();

            var sql =
                $"insert into {graph.DatabaseSchemaName}.mt_events ({columns.Select(x => x.Name).Join(", ")}) values ({columns.Select(x => "?").Join(", ")})";

            configure.Frames.Code($"var parameters = {{0}}.{nameof(CommandBuilder.AppendWithParameters)}(\"{sql}\");",
                Use.Type<CommandBuilder>());

            for (var i = 0; i < columns.Count; i++)
            {
                columns[i].GenerateAppendCode(configure, graph, i);
            }
        }

        private static void buildInsertStream(GeneratedType builderType, GeneratedAssembly generatedAssembly,
            EventGraph graph)
        {
            var operationType = generatedAssembly.AddType(InsertStreamOperationName, typeof(InsertStreamBase));
            operationType.AllInjectedFields.Add(new InjectedField(typeof(EventStream)));

            var columns = new StreamsTable(graph)
                .Columns
                .OfType<IStreamTableColumn>()
                .Where(x => x.Writes)
                .ToArray();

            var sql = $"insert into {graph.DatabaseSchemaName}.mt_streams ({columns.Select(x => x.Name).Join(", ")}) values ({columns.Select(x => "?").Join(", ")})";
            var configureCommand = operationType.MethodFor("ConfigureCommand");

            configureCommand.Frames.Code($"var parameters = {{0}}.{nameof(CommandBuilder.AppendWithParameters)}(\"{sql}\");",
                Use.Type<CommandBuilder>());

            for (var i = 0; i < columns.Length; i++)
            {
                columns[i].GenerateAppendCode(configureCommand, i);
            }

            builderType.MethodFor(nameof(IEventOperationBuilder.InsertStream))
                .Frames.Code($"return new Marten.Generated.{InsertStreamOperationName}(stream);");
        }
    }


}

using System;
using System.Linq;
using Baseline;
using LamarCodeGeneration;
using LamarCodeGeneration.Frames;
using LamarCodeGeneration.Model;
using LamarCompiler;
using Marten.Storage.Metadata;
using Marten.Util;

namespace Marten.Events.V4Concept.CodeGeneration
{
    internal static class EventOperationCodeGenerator
    {
        public static IEventSelector GenerateSelector(DocumentStore store)
        {
            var assembly = new GeneratedAssembly(new GenerationRules("Marten.Generated"));
            assembly.ReferenceAssembly(typeof(EventGraph).Assembly);

            var type = assembly.AddType("EventSelector", typeof(EventSelectorBase));

            var sync = type.MethodFor("Apply");
            var async = type.MethodFor("ApplyAsync");

            // The json data column has to go first
            var table = new EventsTable(store.Events);
            var columns = table.SelectColumns();

            for (var i = 3; i < columns.Count; i++)
            {
                columns[i].GenerateSelectorCodeSync(sync, store.Events, i);
                columns[i].GenerateSelectorCodeAsync(async, store.Events, i);
            }

            var compiler = new AssemblyGenerator();
            compiler.ReferenceAssembly(typeof(AggregationTypeBuilder).Assembly);
            compiler.Compile(assembly);

            return (IEventSelector) Activator.CreateInstance(type.CompiledType, store.Events, store.Serializer);
        }

        public static IEventOperationBuilder GenerateOperationBuilder(EventGraph graph)
        {
            var assembly = new GeneratedAssembly(new GenerationRules("Marten.Generated"));
            assembly.ReferenceAssembly(typeof(EventGraph).Assembly);

            var operationType = assembly.AddType("AppendEventOperation", typeof(AppendEventOperationBase));

            var configure = operationType.MethodFor(nameof(AppendEventOperationBase.ConfigureCommand));
            configure.DerivedVariables.Add(new Variable(typeof(IEvent), nameof(AppendEventOperationBase.Event)));
            configure.DerivedVariables.Add(new Variable(typeof(EventStream), nameof(AppendEventOperationBase.Stream)));

            var columns = new EventsTable(graph).SelectColumns();

            var sql = $"insert into {graph.DatabaseSchemaName}.mt_events ({columns.Select(x => x.Name).Join(", ")}) values ({columns.Select(x => "?").Join(", ")})";

            configure.Frames.Code($"var parameters = {{0}}.{nameof(CommandBuilder.AppendWithParameters)}(\"{sql}\");", Use.Type<CommandBuilder>());

            for (var i = 0; i < columns.Count; i++)
            {
                columns[i].GenerateAppendCode(configure, graph, i);
            }

            var builderType = assembly.AddType("EventOperatorBuilder", typeof(IEventOperationBuilder));
            builderType.MethodFor(nameof(IEventOperationBuilder.AppendEvent))
                .Frames.Code($"return new Marten.Generated.AppendEventOperation(stream, e);");

            builderType.MethodFor(nameof(IEventOperationBuilder.InsertStream))
                .Frames.Code($"return new {typeof(InsertStream).FullNameInCode()}(stream);");

            var compiler = new AssemblyGenerator();
            compiler.ReferenceAssembly(typeof(AggregationTypeBuilder).Assembly);
            compiler.Compile(assembly);

            return (IEventOperationBuilder) Activator.CreateInstance(builderType.CompiledType);

        }
    }


}

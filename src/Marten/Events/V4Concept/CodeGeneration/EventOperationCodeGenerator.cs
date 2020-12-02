using System;
using System.Linq;
using LamarCodeGeneration;
using LamarCompiler;
using Marten.Storage.Metadata;

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
            throw new NotImplementedException();
        }
    }


}

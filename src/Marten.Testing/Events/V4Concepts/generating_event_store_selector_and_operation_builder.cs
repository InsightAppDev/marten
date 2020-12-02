using System;
using System.Collections.Generic;
using System.Linq;
using Marten.Events;
using Marten.Events.V4Concept.CodeGeneration;
using Marten.Storage;
using Marten.Testing.Harness;
using Xunit;

namespace Marten.Testing.Events.V4Concepts
{
    public class generating_event_store_selector_and_operation_builder
    {
        [Theory]
        [MemberData(nameof(Data))]
        public void generate_event_selector(TestCase @case)
        {
            EventOperationCodeGenerator.GenerateSelector(@case.Store)
                .ShouldNotBeNull();
        }

        [Theory]
        [MemberData(nameof(Data))]
        public void generate_operation_builder(TestCase @case)
        {
            EventOperationCodeGenerator.GenerateOperationBuilder(@case.Store.Events)
                .ShouldNotBeNull();
        }





        public static IEnumerable<object[]> Data()
        {
            return cases().Select(x => new object[] {x});
        }

        private static IEnumerable<TestCase> cases()
        {
            yield return new TestCase("Streams as Guid, Vanilla", e => e.StreamIdentity = StreamIdentity.AsGuid);
            yield return new TestCase("Streams as String, Vanilla", e => e.StreamIdentity = StreamIdentity.AsString);

            yield return new TestCase("Streams as Guid, Multi-tenanted", e =>
            {
                e.StreamIdentity = StreamIdentity.AsGuid;
                e.TenancyStyle = TenancyStyle.Conjoined;
            });

            yield return new TestCase("Streams as String, Multi-tenanted", e =>
            {
                e.StreamIdentity = StreamIdentity.AsString;
                e.TenancyStyle = TenancyStyle.Conjoined;
            });
        }

        public class TestCase : IDisposable
        {
            private readonly string _description;

            public TestCase(string description, Action<EventGraph> config)
            {
                _description = description;

                Store = DocumentStore.For(opts =>
                {
                    config(opts.Events);
                    opts.Connection(ConnectionSource.ConnectionString);
                });
            }

            public DocumentStore Store { get;  }

            public void Dispose()
            {
                Store?.Dispose();
            }

            public override string ToString()
            {
                return _description;
            }
        }
    }
}

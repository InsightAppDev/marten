using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baseline;
using Marten.Events;
using Marten.Events.V4Concept.CodeGeneration;
using Marten.Internal.Sessions;
using Marten.Storage;
using Marten.Testing.Harness;
using Xunit;

namespace Marten.Testing.Events.V4Concepts
{
    [Collection("v4events")]
    public class V4_event_appender_tests
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

        [Theory]
        [MemberData(nameof(Data))]
        public async Task can_fetch_stream_async(TestCase @case)
        {
            @case.Store.Advanced.Clean.CompletelyRemoveAll();
            @case.StartNewStream();
            using var query = @case.Store.QuerySession();

            var builder = EventOperationCodeGenerator.GenerateOperationBuilder(@case.Store.Events);
            var handler = builder.QueryForStream(@case.ToEventStream());

            var state = await query.As<QuerySession>().ExecuteHandlerAsync(handler, CancellationToken.None);
            state.ShouldNotBeNull();
        }

        [Theory]
        [MemberData(nameof(Data))]
        public void can_fetch_stream_sync(TestCase @case)
        {
            @case.Store.Advanced.Clean.CompletelyRemoveAll();
            @case.StartNewStream();
            using var query = @case.Store.QuerySession();

            var builder = EventOperationCodeGenerator.GenerateOperationBuilder(@case.Store.Events);
            var handler = builder.QueryForStream(@case.ToEventStream());

            var state = query.As<QuerySession>().ExecuteHandler(handler);
            state.ShouldNotBeNull();
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
                    opts.DatabaseSchemaName = "v4events";
                    opts.AutoCreateSchemaObjects = AutoCreate.All;
                });

                Store.Advanced.Clean.CompletelyRemoveAll();

                StreamId = Guid.NewGuid();
                TenantId = "KC";
            }

            public void StartNewStream()
            {
                var events = new object[] {new AEvent(), new BEvent(), new CEvent(), new DEvent()};
                using var session = Store.Events.TenancyStyle == TenancyStyle.Conjoined
                    ? Store.LightweightSession(TenantId)
                    : Store.LightweightSession();

                if (Store.Events.StreamIdentity == StreamIdentity.AsGuid)
                {
                    session.Events.StartStream(StreamId, events);
                    session.SaveChanges();
                }
                else
                {
                    session.Events.StartStream(StreamId.ToString(), events);
                    session.SaveChanges();
                }
            }

            public string TenantId { get; set; }

            public Guid StreamId { get;  }

            public DocumentStore Store { get;  }

            public void Dispose()
            {
                Store?.Dispose();
            }

            public override string ToString()
            {
                return _description;
            }

            public EventStream ToEventStream()
            {
                if (Store.Events.StreamIdentity == StreamIdentity.AsGuid)
                {
                    return new EventStream(StreamId, true)
                    {
                        TenantId = TenantId
                    };
                }
                else
                {
                    return new EventStream(StreamId.ToString(), true)
                    {
                        TenantId = TenantId
                    };
                }
            }
        }
    }
}

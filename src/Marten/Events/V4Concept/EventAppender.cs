using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Marten.Internal;
using Marten.Internal.Operations;

namespace Marten.Events.V4Concept
{
    public class EventAppender: IEventAppender
    {
        private readonly EventGraph _graph;
        private readonly IEventOperationBuilder _builder;
        private readonly IInlineProjection[] _projections;

        public EventAppender(EventGraph graph, IEventOperationBuilder builder, IInlineProjection[] projections)
        {
            _graph = graph;
            _builder = builder;
            _projections = projections;

            // TODO -- split out a specific ISelector for StreamState.
            // TODO -- track the event streams separately within the DocumentSessionBase
        }

        public IEnumerable<IStorageOperation> BuildAppendOperations(IMartenSession session, IReadOnlyList<EventStream> streams)
        {
            // 1. load the stream data for each stream & reserve new sequence values.
            // 2. Assign the versions and sequence values to each event stream
            // 3. emit an operation to update each stream version
            // 4. emit an operation to insert each event
            // 5. emit operations for each inline projection
            throw new System.NotImplementedException();
        }

        public Task<IEnumerable<IStorageOperation>> BuildAppendOperationsAsync(IMartenSession session, IReadOnlyList<EventStream> streams, CancellationToken cancellation)
        {
            throw new System.NotImplementedException();
        }

        public void MarkTombstones(IReadOnlyList<EventStream> streams)
        {
            throw new System.NotImplementedException();
        }
    }
}
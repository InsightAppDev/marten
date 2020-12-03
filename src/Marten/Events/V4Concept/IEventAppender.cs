using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Marten.Internal;
using Marten.Internal.Operations;

namespace Marten.Events.V4Concept
{
    public interface IEventAppender
    {
        IEnumerable<IStorageOperation> BuildAppendOperations(IMartenSession session, IReadOnlyList<EventStream> streams);
        Task<IEnumerable<IStorageOperation>> BuildAppendOperationsAsync(IMartenSession session, IReadOnlyList<EventStream> streams, CancellationToken cancellation);

        void MarkTombstones(IReadOnlyList<EventStream> streams);
    }
}

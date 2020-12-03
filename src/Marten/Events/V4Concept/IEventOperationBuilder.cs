using Marten.Internal;
using Marten.Internal.Operations;
using Marten.Linq.QueryHandlers;

namespace Marten.Events.V4Concept
{
    /// <summary>
    /// This is generated at runtime
    /// </summary>
    public interface IEventOperationBuilder
    {
        IStorageOperation AppendEvent(EventGraph events, IMartenSession session, EventStream stream, IEvent e);
        IStorageOperation InsertStream(EventStream stream);
        IQueryHandler<StreamState> QueryForStream(EventStream stream);

        IStorageOperation UpdateStreamVersion(EventStream stream);
    }
}

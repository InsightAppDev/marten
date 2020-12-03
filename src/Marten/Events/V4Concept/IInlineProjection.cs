using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Marten.Events.V4Concept
{
    public interface IInlineProjection : IProjectionBase
    {
        void Apply(IDocumentSession session, IReadOnlyList<EventStream> streams);

        Task ApplyAsync(IDocumentSession session, IReadOnlyList<EventStream> streams, CancellationToken cancellation);
    }
}
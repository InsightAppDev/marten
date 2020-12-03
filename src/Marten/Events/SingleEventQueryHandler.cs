using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Marten.Internal;
using Marten.Linq;
using Marten.Linq.QueryHandlers;
using Marten.Services;
using Marten.Util;

namespace Marten.Events
{
    internal class SingleEventQueryHandler: IQueryHandler<IEvent>
    {
        private readonly Guid _id;
        private readonly IEventSelector _selector;

        public SingleEventQueryHandler(Guid id, EventGraph events)
        {
            _id = id;
            _selector = events.Selector;
        }

        public void ConfigureCommand(CommandBuilder sql, IMartenSession session)
        {
            _selector.WriteSelectClause(sql);

            var param = sql.AddParameter(_id);
            sql.Append(" where id = :");
            sql.Append(param.ParameterName);
        }

        public IEvent Handle(DbDataReader reader, IMartenSession session)
        {
            return reader.Read() ? _selector.Resolve(reader) : null;
        }

        public async Task<IEvent> HandleAsync(DbDataReader reader, IMartenSession session,
            CancellationToken token)
        {
            return await reader.ReadAsync(token).ConfigureAwait(false)
                ? await _selector.ResolveAsync(reader, token).ConfigureAwait(false)
                : null;
        }
    }
}

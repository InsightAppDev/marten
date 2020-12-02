using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baseline;
using Marten.Exceptions;
using Marten.Linq.Selectors;
using Marten.Util;
using Npgsql;

namespace Marten.Events
{
    internal interface IEventSelector: ISelector<IEvent>
    {
        EventGraph Events { get; }
        void WriteSelectClause(CommandBuilder sql);
        string[] SelectFields();
    }

    public abstract class EventSelectorBase : IEventSelector
    {
        private readonly ISerializer _serializer;
        private readonly string[] _fields;
        private readonly string _selectClause;
        public EventGraph Events { get; }

        public EventSelectorBase(EventGraph events, ISerializer serializer)
        {
            _serializer = serializer;
            Events = events;

            // The json data column has to go first
            var table = new EventsTable(events);
            var columns = table.SelectColumns();

            _fields = columns.Select(x => x.Name).ToArray();

            _selectClause = $"select {_fields.Join(", ")} from {Events.DatabaseSchemaName}.mt_events as d";
        }

        public IEvent Resolve(DbDataReader reader)
        {
            var eventTypeName = reader.GetString(1);
            var mapping = Events.EventMappingFor(eventTypeName);
            if (mapping == null)
            {
                var dotnetTypeName = reader.GetFieldValue<string>(2);
                if (dotnetTypeName.IsEmpty())
                {
                    throw new UnknownEventTypeException(eventTypeName);
                }

                var type = Events.TypeForDotNetName(dotnetTypeName);
                mapping = Events.EventMappingFor(type);
            }

            var dataJson = reader.GetTextReader(0);
            var data = _serializer.FromJson(mapping.DocumentType, dataJson).As<object>();

            var @event = mapping.Wrap(data);

            Apply(reader, @event);

            return @event;
        }

        public abstract void Apply(DbDataReader reader, IEvent e);

        public async Task<IEvent> ResolveAsync(DbDataReader reader, CancellationToken token)
        {
            var eventTypeName = await reader.GetFieldValueAsync<string>(1, token);
            var mapping = Events.EventMappingFor(eventTypeName);
            if (mapping == null)
            {
                var dotnetTypeName = await reader.GetFieldValueAsync<string>(2, token).ConfigureAwait(false);
                if (dotnetTypeName.IsEmpty())
                {
                    throw new UnknownEventTypeException(eventTypeName);
                }
                Type type;
                try
                {
                    type = Events.TypeForDotNetName(dotnetTypeName);
                }
                catch (ArgumentNullException)
                {
                    throw new UnknownEventTypeException(dotnetTypeName);
                }
                mapping = Events.EventMappingFor(type);
            }

            var dataJson = await reader.As<NpgsqlDataReader>().GetTextReaderAsync(0, token);
            var data = _serializer.FromJson(mapping.DocumentType, dataJson);

            var @event = mapping.Wrap(data);

            await ApplyAsync(reader, @event, token);

            return @event;
        }

        public abstract Task ApplyAsync(DbDataReader reader, IEvent e, CancellationToken token);

        public void WriteSelectClause(CommandBuilder sql)
        {
            sql.Append(_selectClause);
        }

        public string[] SelectFields()
        {
            return _fields;
        }
    }
}

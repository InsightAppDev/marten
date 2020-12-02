using System;
using System.Linq;
using System.Linq.Expressions;
using LamarCodeGeneration;
using Marten.Internal.CodeGeneration;
using Marten.Linq.Parsing;
using Marten.Schema;
using Marten.Storage;
using Marten.Storage.Metadata;
using Marten.Util;

namespace Marten.Events
{
    // SAMPLE: EventsTable
    public class EventsTable: Table
    {
        public EventsTable(EventGraph events) : base(new DbObjectName(events.DatabaseSchemaName, "mt_events"))
        {
            var stringIdType = events.GetStreamIdDBType();

            AddPrimaryKey(new TableColumn("seq_id", "bigint"));
            AddColumn("id", "uuid", "NOT NULL");
            AddColumn("stream_id", stringIdType, (events.TenancyStyle != TenancyStyle.Conjoined) ? $"REFERENCES {events.DatabaseSchemaName}.mt_streams ON DELETE CASCADE" : null);
            AddColumn("version", "integer", "NOT NULL");
            AddColumn("data", "jsonb", "NOT NULL");
            AddColumn("type", "varchar(500)", "NOT NULL");
            AddColumn("timestamp", "timestamptz", "default (now()) NOT NULL");
            AddColumn<TenantIdColumn>();
            AddColumn(new DotNetTypeColumn { Directive = "NULL" });

            if (events.TenancyStyle == TenancyStyle.Conjoined)
            {
                Constraints.Add($"FOREIGN KEY(stream_id, {TenantIdColumn.Name}) REFERENCES {events.DatabaseSchemaName}.mt_streams(id, {TenantIdColumn.Name})");
                Constraints.Add($"CONSTRAINT pk_mt_events_stream_and_version UNIQUE(stream_id, {TenantIdColumn.Name}, version)");
            }
            else
            {
                Constraints.Add("CONSTRAINT pk_mt_events_stream_and_version UNIQUE(stream_id, version)");
            }

            Constraints.Add("CONSTRAINT pk_mt_events_id_unique UNIQUE(id)");
        }
    }

    // ENDSAMPLE

    public interface IEventTableColumn
    {
        public void GenerateSelectorCodeSync(GeneratedMethod method, EventGraph graph, int index);
        public abstract void GenerateSelectorCodeAsync(GeneratedMethod method, EventGraph graph, int index);
        public abstract void GenerateAppendCode(GeneratedMethod method, EventGraph graph, int index);
    }

    public class EventTableColumn: TableColumn, IEventTableColumn
    {
        private readonly Expression<Func<IEvent, object>> _eventMemberExpression;

        public EventTableColumn(string name, Expression<Func<IEvent, object>> eventMemberExpression) : base(name, "varchar")
        {
            _eventMemberExpression = eventMemberExpression;
            var member = FindMembers.Determine(eventMemberExpression).Single();
            Type = TypeMappings.GetPgType(member.GetMemberType(), EnumStorage.AsInteger);
        }

        public void GenerateSelectorCodeSync(GeneratedMethod method, EventGraph graph, int index)
        {
            method.IfDbReaderValueIsNotNull(index, () =>
            {
                method.AssignMemberFromReader(null, index, _eventMemberExpression);
            });
        }

        public void GenerateSelectorCodeAsync(GeneratedMethod method, EventGraph graph, int index)
        {
            method.IfDbReaderValueIsNotNull(index, () =>
            {
                method.AssignMemberFromReader(null, index, _eventMemberExpression);
            });
        }

        public void GenerateAppendCode(GeneratedMethod method, EventGraph graph, int index)
        {
            throw new NotImplementedException();
        }
    }
}

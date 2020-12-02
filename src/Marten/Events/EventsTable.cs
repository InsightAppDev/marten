using System;
using System.Linq;
using Baseline;
using Marten.Schema;
using Marten.Storage;
using Marten.Storage.Metadata;

namespace Marten.Events
{
    // SAMPLE: EventsTable
    public class EventsTable: Table
    {
        public EventsTable(EventGraph events): base(new DbObjectName(events.DatabaseSchemaName, "mt_events"))
        {
            AddPrimaryKey(new EventTableColumn("seq_id", x => x.Sequence));
            AddColumn(new EventTableColumn("id", x => x.Id) {Directive = "NOT NULL"});
            AddColumn(new StreamIdColumn(events));
            AddColumn(new EventTableColumn("version", x => x.Version) {Directive = "NOT NULL"});
            AddColumn<EventJsonDataColumn>();
            AddColumn<EventTypeColumn>();
            AddColumn(new EventTableColumn("timestamp", x => x.Timestamp)
            {
                Directive = "default (now()) NOT NULL", Type = "timestamptz"
            });

            AddColumn<TenantIdColumn>();
            AddColumn(new DotNetTypeColumn {Directive = "NULL"});

            if (events.TenancyStyle == TenancyStyle.Conjoined)
            {
                Constraints.Add(
                    $"FOREIGN KEY(stream_id, {TenantIdColumn.Name}) REFERENCES {events.DatabaseSchemaName}.mt_streams(id, {TenantIdColumn.Name})");
                Constraints.Add(
                    $"CONSTRAINT pk_mt_events_stream_and_version UNIQUE(stream_id, {TenantIdColumn.Name}, version)");
            }
            else
            {
                Constraints.Add("CONSTRAINT pk_mt_events_stream_and_version UNIQUE(stream_id, version)");
            }

            Constraints.Add("CONSTRAINT pk_mt_events_id_unique UNIQUE(id)");

            var badColumns = Columns.Where(x => !(x is IEventTableColumn)).ToArray();
            if (badColumns.Any())
                throw new InvalidOperationException(
                    $"These columns are NOT implementing IEventTableColumn yet: {badColumns.Select(x => x.Name).Join(", ")}");
        }
    }

    // ENDSAMPLE
}

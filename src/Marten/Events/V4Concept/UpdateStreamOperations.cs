using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Marten.Internal;
using Marten.Internal.Operations;
using Marten.Util;

namespace Marten.Events.V4Concept
{
    public abstract class UpdateStreamVersion : IStorageOperation
    {
        public abstract void ConfigureCommand(CommandBuilder builder, IMartenSession session);

        public Type DocumentType => typeof(EventStream);
        public void Postprocess(DbDataReader reader, IList<Exception> exceptions)
        {
            // TODO -- assert
        }

        public Task PostprocessAsync(DbDataReader reader, IList<Exception> exceptions, CancellationToken token)
        {
            // TODO -- assert
            return Task.CompletedTask;
        }

        public OperationRole Role()
        {
            return OperationRole.Events;
        }
    }
}

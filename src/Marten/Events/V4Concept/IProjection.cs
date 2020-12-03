using LamarCodeGeneration;

namespace Marten.Events.V4Concept
{
    public interface IProjection: IProjectionBase
    {
        // TODO -- eliminate the dependency on DocumentStore
        void GenerateHandlerTypes(DocumentStore store, GeneratedAssembly assembly);

        IProjectionShard Shards();
        IInlineProjection ToInline();

        bool TryResolveLiveAggregator<T>(out ILiveAggregator<T> aggregator);
    }
}
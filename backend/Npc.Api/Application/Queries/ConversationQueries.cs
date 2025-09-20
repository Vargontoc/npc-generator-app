using Npc.Api.Dtos;
using Npc.Api.Repositories;
using Npc.Api.Services;

namespace Npc.Api.Application.Queries
{
    // Query DTOs - Main conversation queries
    public record GetConversationQuery(Guid ConversationId) : IQuery<ConversationResponse?>;
    public record GetUtteranceQuery(Guid UtteranceId) : IQuery<UtteranceDetail?>;
    public record GetLinearPathQuery(Guid ConversationId) : IQuery<PathResponse?>;
    public record GetGraphQuery(Guid ConversationId, int Depth) : IQuery<GraphResponse?>;
    public record GetRandomPathQuery(Guid ConversationId, int MaxDepth) : IQuery<PathResponse?>;

    // Query Handlers
    public class GetConversationQueryHandler : IQueryHandler<GetConversationQuery, ConversationResponse?>
    {
        private readonly IConversationRepository _repository;

        public GetConversationQueryHandler(IConversationRepository repository)
        {
            _repository = repository;
        }

        public async Task<ConversationResponse?> HandleAsync(GetConversationQuery query, CancellationToken ct = default)
        {
            return await _repository.GetConversationAsync(query.ConversationId, ct);
        }
    }

    public class GetUtteranceQueryHandler : IQueryHandler<GetUtteranceQuery, UtteranceDetail?>
    {
        private readonly IConversationRepository _repository;

        public GetUtteranceQueryHandler(IConversationRepository repository)
        {
            _repository = repository;
        }

        public async Task<UtteranceDetail?> HandleAsync(GetUtteranceQuery query, CancellationToken ct = default)
        {
            return await _repository.GetUtteranceAsync(query.UtteranceId, ct);
        }
    }

    public class GetLinearPathQueryHandler : IQueryHandler<GetLinearPathQuery, PathResponse?>
    {
        private readonly IConversationRepository _repository;

        public GetLinearPathQueryHandler(IConversationRepository repository)
        {
            _repository = repository;
        }

        public async Task<PathResponse?> HandleAsync(GetLinearPathQuery query, CancellationToken ct = default)
        {
            return await _repository.GetLinearPathAsync(query.ConversationId, ct);
        }
    }

    public class GetGraphQueryHandler : IQueryHandler<GetGraphQuery, GraphResponse?>
    {
        private readonly IConversationRepository _repository;

        public GetGraphQueryHandler(IConversationRepository repository)
        {
            _repository = repository;
        }

        public async Task<GraphResponse?> HandleAsync(GetGraphQuery query, CancellationToken ct = default)
        {
            return await _repository.GetGraphAsync(query.ConversationId, query.Depth, ct);
        }
    }

    public class GetRandomPathQueryHandler : IQueryHandler<GetRandomPathQuery, PathResponse?>
    {
        private readonly IConversationGraphService _service; // Use existing service for complex logic

        public GetRandomPathQueryHandler(IConversationGraphService service)
        {
            _service = service;
        }

        public async Task<PathResponse?> HandleAsync(GetRandomPathQuery query, CancellationToken ct = default)
        {
            return await _service.GetRandomPathAsync(query.ConversationId, query.MaxDepth, ct);
        }
    }
}
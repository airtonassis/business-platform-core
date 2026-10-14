namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Contrato mínimo de um evento de domínio: registro de um fato ocorrido, com identidade e instante de ocorrência.
/// </summary>
/// <remarks>
/// A interface expõe apenas leitura e não cria nem consulta o relógio: a origem de <see cref="EventId"/> e
/// <see cref="OccurredOnUtc"/> é responsabilidade do código que constrói o evento. Não valida o offset UTC,
/// não garante imutabilidade profunda e não garante, por si só, deduplicação, idempotência ou processamento
/// exatamente uma vez. Eventos concretos podem incluir identificadores de agregados e outros dados de domínio.
/// </remarks>
public interface IDomainEvent
{
    /// <summary>Identificador do evento. A unicidade depende de quem constrói o evento.</summary>
    Guid EventId { get; }

    /// <summary>Instante de ocorrência do fato. Por convenção, com offset UTC; a interface não o valida.</summary>
    DateTimeOffset OccurredOnUtc { get; }
}

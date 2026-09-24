namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Categoria semântica de um <see cref="Error"/>.
/// </summary>
/// <remarks>
/// Os valores numéricos são explícitos e fazem parte do contrato público.
/// O tipo não carrega conhecimento sobre protocolos de transporte (ex.: HTTP).
/// </remarks>
public enum ErrorType
{
    /// <summary>Falha genérica, quando nenhuma categoria mais específica é adequada.</summary>
    Failure = 0,

    /// <summary>Uma entrada ou regra de validação não foi atendida.</summary>
    Validation = 1,

    /// <summary>Um recurso esperado não existe.</summary>
    NotFound = 2,

    /// <summary>A operação conflita com o estado atual do domínio ou da aplicação.</summary>
    Conflict = 3,

    /// <summary>A operação exige uma identidade autenticada.</summary>
    Unauthorized = 4,

    /// <summary>A identidade conhecida não possui permissão para a operação.</summary>
    Forbidden = 5
}

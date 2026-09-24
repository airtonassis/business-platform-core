namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Representa, de forma imutável e com igualdade por valor, uma falha esperada
/// de uma operação de domínio ou caso de uso.
/// </summary>
/// <remarks>
/// <see cref="Code"/> e <see cref="Description"/> não devem conter informações sensíveis,
/// segredos, credenciais, tokens ou detalhes técnicos internos.
/// </remarks>
public sealed record Error
{
    /// <summary>
    /// Constrói exclusivamente a instância canônica de <see cref="None"/>.
    /// </summary>
    private Error()
    {
        Code = string.Empty;
        Description = string.Empty;
        Type = ErrorType.Failure;
    }

    /// <summary>
    /// Cria um erro válido.
    /// </summary>
    /// <param name="code">Identificador programático e estável da falha (ex.: <c>User.NotFound</c>).</param>
    /// <param name="description">Descrição legível da falha.</param>
    /// <param name="type">Categoria semântica da falha. Padrão: <see cref="ErrorType.Failure"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="code"/> ou <paramref name="description"/> é <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="code"/> ou <paramref name="description"/> é vazio ou contém somente espaços.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> não é um valor definido de <see cref="ErrorType"/>.</exception>
    public Error(string code, string description, ErrorType type = ErrorType.Failure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "O ErrorType informado não é um valor definido.");
        }

        Code = code;
        Description = description;
        Type = type;
    }

    /// <summary>
    /// Representação canônica da ausência de erro. Não representa uma falha válida.
    /// </summary>
    public static Error None { get; } = new();

    /// <summary>Identificador programático e estável da falha.</summary>
    public string Code { get; }

    /// <summary>Descrição legível da falha. Não deve ser usada como identificador programático.</summary>
    public string Description { get; }

    /// <summary>Categoria semântica da falha.</summary>
    public ErrorType Type { get; }
}

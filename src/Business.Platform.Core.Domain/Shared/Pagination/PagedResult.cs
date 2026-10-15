using System.Collections.ObjectModel;

namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Representa uma página de itens já obtida, com metadados de navegação.
/// </summary>
/// <typeparam name="T">Tipo dos itens.</typeparam>
/// <remarks>
/// Não executa consultas nem aplica ordenação. <see cref="TotalCount"/> é best-effort: pode ter sido
/// obtido em momento distinto dos itens, por isso não se exige consistência entre ambos. Páginas além do total
/// são permitidas. <see cref="Items"/> é uma coleção não modificável (cópia defensiva), mas os objetos
/// <typeparamref name="T"/> podem ser mutáveis.
/// </remarks>
public sealed class PagedResult<T>
{
    private PagedResult(T[] items, PaginationRequest request, long totalCount)
    {
        Items = new ReadOnlyCollection<T>(items);
        PageNumber = request.PageNumber;
        PageSize = request.PageSize;
        TotalCount = totalCount;
        TotalPages = totalCount == 0
            ? 0
            : (totalCount / PageSize) + (totalCount % PageSize == 0 ? 0 : 1);
        HasPreviousPage = PageNumber > 1;
        HasNextPage = PageNumber < TotalPages;
    }

    /// <summary>Itens da página, na ordem recebida, em coleção não modificável.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>Número da página (1-based), vindo da solicitação.</summary>
    public int PageNumber { get; }

    /// <summary>Tamanho da página, vindo da solicitação.</summary>
    public int PageSize { get; }

    /// <summary>Total de itens (não negativo).</summary>
    public long TotalCount { get; }

    /// <summary>Total de páginas, calculado sem overflow.</summary>
    public long TotalPages { get; }

    /// <summary>Indica se existe página anterior.</summary>
    public bool HasPreviousPage { get; }

    /// <summary>Indica se existe página seguinte.</summary>
    public bool HasNextPage { get; }

    /// <summary>Cria uma página copiando e validando os itens.</summary>
    /// <param name="items">Itens da página (nenhum pode ser nulo; no máximo o tamanho da página).</param>
    /// <param name="request">Solicitação que originou a página.</param>
    /// <param name="totalCount">Total de itens, não negativo.</param>
    /// <returns>A página criada.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> ou <paramref name="request"/> é nulo.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="totalCount"/> é negativo.</exception>
    /// <exception cref="ArgumentException">
    /// <c>Count</c> negativo ou acima do tamanho da página, enumeração inconsistente com <c>Count</c> ou elemento nulo.
    /// </exception>
    public static PagedResult<T> Create(IReadOnlyCollection<T> items, PaginationRequest request, long totalCount)
    {
        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount), totalCount, "O total não pode ser negativo.");
        }

        var count = items.Count;

        if (count < 0)
        {
            throw new ArgumentException("A coleção informou uma quantidade negativa de itens.", nameof(items));
        }

        if (count > request.PageSize)
        {
            throw new ArgumentException("A quantidade de itens excede o tamanho da página.", nameof(items));
        }

        var buffer = new T[count];
        var index = 0;

        foreach (var item in items)
        {
            if (index >= count)
            {
                throw new ArgumentException("A enumeração produziu mais itens do que Count.", nameof(items));
            }

            if (item is null)
            {
                throw new ArgumentException("Os itens não podem conter elementos nulos.", nameof(items));
            }

            buffer[index] = item;
            index++;
        }

        if (index < count)
        {
            throw new ArgumentException("A enumeração produziu menos itens do que Count.", nameof(items));
        }

        return new PagedResult<T>(buffer, request, totalCount);
    }
}

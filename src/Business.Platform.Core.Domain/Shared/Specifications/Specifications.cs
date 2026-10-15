namespace Business.Platform.Core.Domain.Shared;

/// <summary>
/// Identidades lógicas de <see cref="ISpecification{T}"/>.
/// </summary>
public static class Specifications
{
    /// <summary>
    /// Especificação que representa <c>x =&gt; true</c> (elemento neutro de <c>And</c>).
    /// </summary>
    /// <typeparam name="T">Tipo avaliado.</typeparam>
    /// <returns>Nova especificação satisfeita por qualquer elemento.</returns>
    public static ISpecification<T> True<T>() => new Specification<T>(_ => true);

    /// <summary>
    /// Especificação que representa <c>x =&gt; false</c> (elemento neutro de <c>Or</c>).
    /// </summary>
    /// <typeparam name="T">Tipo avaliado.</typeparam>
    /// <returns>Nova especificação que nenhum elemento satisfaz.</returns>
    public static ISpecification<T> False<T>() => new Specification<T>(_ => false);
}

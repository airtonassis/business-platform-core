using System.Reflection;
using Business.Platform.Core.Domain.Shared;

namespace Business.Platform.Core.ArchitectureTests.Domain.Shared.Time;

/// <summary>
/// Verifica por reflexão que <see cref="TimeProvider"/> não aparece em assinaturas do Domain.
/// Limitação: reflexão de metadados não detecta todas as chamadas ao relógio dentro de corpos de métodos.
/// </summary>
public sealed class TimeProviderArchitectureTests
{
    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static bool IsOrContainsTimeProvider(Type? type) => IsOrContainsTimeProvider(type, []);

    private static bool IsOrContainsTimeProvider(Type? type, HashSet<Type> visited)
    {
        if (type is null || !visited.Add(type))
        {
            return false;
        }

        if (type.HasElementType)
        {
            return IsOrContainsTimeProvider(type.GetElementType(), visited);
        }

        if (typeof(TimeProvider).IsAssignableFrom(type))
        {
            return true;
        }

        if (type.IsGenericType && type.GetGenericArguments().Any(a => IsOrContainsTimeProvider(a, visited)))
        {
            return true;
        }

        if (type.IsGenericParameter
            && type.GetGenericParameterConstraints().Any(c => IsOrContainsTimeProvider(c, visited)))
        {
            return true;
        }

        return false;
    }

    private static IEnumerable<string> SignatureViolations(Type type)
    {
        if (IsOrContainsTimeProvider(type.BaseType))
        {
            yield return $"{type.FullName} (base type)";
        }

        foreach (var @interface in type.GetInterfaces().Where(IsOrContainsTimeProvider))
        {
            yield return $"{type.FullName} (interface {@interface.FullName})";
        }

        if (type.IsGenericTypeDefinition
            && type.GetGenericArguments().Any(IsOrContainsTimeProvider))
        {
            yield return $"{type.FullName} (generic parameter constraint)";
        }

        foreach (var field in type.GetFields(AllDeclared).Where(f => IsOrContainsTimeProvider(f.FieldType)))
        {
            yield return $"{type.FullName}.{field.Name} (field)";
        }

        foreach (var property in type.GetProperties(AllDeclared).Where(p => IsOrContainsTimeProvider(p.PropertyType)))
        {
            yield return $"{type.FullName}.{property.Name} (property)";
        }

        foreach (var ctor in type.GetConstructors(AllDeclared).Where(c => c.GetParameters().Any(p => IsOrContainsTimeProvider(p.ParameterType))))
        {
            yield return $"{type.FullName}..ctor (constructor parameter)";
        }

        foreach (var method in type.GetMethods(AllDeclared))
        {
            if (IsOrContainsTimeProvider(method.ReturnType)
                || method.GetParameters().Any(p => IsOrContainsTimeProvider(p.ParameterType))
                || (method.IsGenericMethodDefinition && method.GetGenericArguments().Any(IsOrContainsTimeProvider)))
            {
                yield return $"{type.FullName}.{method.Name} (method)";
            }
        }
    }

    [Fact]
    public void DomainAssembly_ShouldNotExposeTimeProviderInAnySignature()
    {
        // Assembly.GetTypes() inclui tipos não públicos e aninhados.
        var violations = typeof(AuditMetadata).Assembly
            .GetTypes()
            .SelectMany(SignatureViolations)
            .ToArray();

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void DomainAssembly_ShouldNotDeclareTypesDerivedFromTimeProvider()
    {
        typeof(AuditMetadata).Assembly
            .GetTypes()
            .Where(t => typeof(TimeProvider).IsAssignableFrom(t))
            .ShouldBeEmpty();
    }
}

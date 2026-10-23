using System.Reflection;
using Business.Platform.Core.Domain.Shared;
using NetArchTest.Rules;
using SortDirection = Business.Platform.Core.Domain.Shared.SortDirection;

namespace Business.Platform.Core.ArchitectureTests.Domain.Shared.Sorting;

public sealed class SortingArchitectureTests
{
    private const string DomainAssemblyName = "Business.Platform.Core.Domain";
    private const string ComponentNamespace = "Business.Platform.Core.Domain.Shared";
    private const string ComponentNamePattern = "^(SortDirection|SortKey|SortSpecification|SortingErrorCodes)(`[12])?$";

    private static readonly Type KeyType = typeof(SortKey<>);
    private static readonly Type SpecificationType = typeof(SortSpecification<,>);

    private static readonly Type[] ComponentTypes = [typeof(SortDirection), KeyType, SpecificationType, typeof(SortingErrorCodes)];

    private static readonly string[] ForbiddenDependencies =
    [
        "Business.Platform.Core.Application",
        "Business.Platform.Core.Infrastructure",
        "Business.Platform.Core.Api",
        "Business.Platform.Core.Contracts",
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Dapper",
        "System.Data",
        "System.Net.Http",
        "System.Text.Json",
        "System.Linq.Queryable"
    ];

    [Fact]
    public void ComponentFilter_ShouldSelectExactlyTheFourComponents()
    {
        var selected = Types.InAssembly(KeyType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .GetTypes();

        selected.ShouldBe(ComponentTypes, ignoreOrder: true);
    }

    [Fact]
    public void Components_ShouldResideInDomainAssemblyAndSharedNamespace()
    {
        foreach (var type in ComponentTypes)
        {
            type.Assembly.GetName().Name.ShouldBe(DomainAssemblyName);
            type.Namespace.ShouldBe(ComponentNamespace);
            type.IsPublic.ShouldBeTrue();
        }
    }

    [Fact]
    public void SortDirection_ShouldBePublicEnumWithTwoValues()
    {
        typeof(SortDirection).IsEnum.ShouldBeTrue();
        Enum.GetNames<SortDirection>().ShouldBe(["Ascending", "Descending"]);
    }

    [Fact]
    public void SortKeyAndSpecification_ShouldBeSealedClasses()
    {
        KeyType.IsClass.ShouldBeTrue();
        KeyType.IsSealed.ShouldBeTrue();
        SpecificationType.IsClass.ShouldBeTrue();
        SpecificationType.IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void SortingErrorCodes_ShouldBeStaticClassOfPublicConstants()
    {
        var codes = typeof(SortingErrorCodes);

        (codes.IsAbstract && codes.IsSealed).ShouldBeTrue();
        codes.GetFields(BindingFlags.Public | BindingFlags.Static)
            .ShouldAllBe(f => f.IsLiteral && f.FieldType == typeof(string));
        codes.GetFields(BindingFlags.Public | BindingFlags.Static).Length.ShouldBe(7);
    }

    [Fact]
    public void SortSpecification_ShouldHaveAggregateAndIdParametersWithApprovedConstraints()
    {
        var arguments = SpecificationType.GetGenericArguments();
        var constraint = arguments[0].GetGenericParameterConstraints().ShouldHaveSingleItem();

        arguments.Select(a => a.Name).ShouldBe(["TAggregate", "TId"]);
        constraint.GetGenericTypeDefinition().ShouldBe(typeof(AggregateRoot<>));
        constraint.GetGenericArguments().ShouldHaveSingleItem().ShouldBe(arguments[1]);
        arguments[0].GenericParameterAttributes.ShouldBe(GenericParameterAttributes.None);
    }

    // `notnull` não é constraint CLR: verificamos a ausência de constraints (inclusive IComparable<TId>) e a anotação de nulabilidade
    [Fact]
    public void SortSpecification_IdentifierShouldBeNotNullWithoutComparableRequirement()
    {
        var parameter = SpecificationType.GetGenericArguments()[1];

        parameter.GenericParameterAttributes.ShouldBe(GenericParameterAttributes.None);
        parameter.GetGenericParameterConstraints().ShouldBeEmpty();

        var annotations = parameter.GetCustomAttributesData()
            .Concat(SpecificationType.GetCustomAttributesData())
            .Where(data => data.AttributeType.Name is "NullableAttribute" or "NullableContextAttribute")
            .Select(data => data.ConstructorArguments[0].Value)
            .ToArray();

        annotations.ShouldContain((byte)1);
        annotations.ShouldNotContain((byte)2);
    }

    // O Domain é o único construtor de TotalOrder: nenhum construtor público ou protegido, apenas a fábrica Create
    [Theory]
    [InlineData(typeof(SortKey<>))]
    [InlineData(typeof(SortSpecification<,>))]
    public void Components_ShouldNotExposePublicOrProtectedConstructors(Type type)
    {
        type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).ShouldBeEmpty();
        type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .ShouldAllBe(c => c.IsPrivate);
    }

    [Fact]
    public void SortSpecification_ShouldExposeSingleStaticFactoryReturningResult()
    {
        var factories = SpecificationType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .ToArray();

        var factory = factories.ShouldHaveSingleItem();
        factory.Name.ShouldBe("Create");
        factory.ReturnType.GetGenericTypeDefinition().ShouldBe(typeof(Result<>));
    }

    [Fact]
    public void SortSpecification_ShouldExposeOnlyReadOnlyListsForKeys()
    {
        var properties = SpecificationType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        properties.Select(p => p.Name).Order().ShouldBe(["RequestedKeys", "TotalOrder"]);
        properties.ShouldAllBe(p => p.CanRead && p.SetMethod == null);
        properties.ShouldAllBe(p => p.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>));
    }

    [Theory]
    [InlineData(typeof(SortKey<>))]
    [InlineData(typeof(SortSpecification<,>))]
    public void Components_ShouldBeImmutable(Type type)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        type.GetFields(all).Where(f => !f.IsLiteral).ShouldAllBe(f => f.IsInitOnly);
        type.GetFields(BindingFlags.Public | BindingFlags.Instance).ShouldBeEmpty();
        type.GetProperties(all).Where(p => !p.GetMethod!.IsStatic)
            .ShouldAllBe(p => p.SetMethod == null || p.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Length > 0);
        type.GetInterfaces().ShouldContain(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEquatable<>));
    }

    [Fact]
    public void Components_ShouldNotImplementOtherDomainContractsOrComparable()
    {
        KeyType.GetInterfaces().Length.ShouldBe(1);
        SpecificationType.GetInterfaces().Length.ShouldBe(1);
    }

    [Fact]
    public void Components_ShouldNotDependOnForbiddenLayersOrFrameworks()
    {
        var result = Types.InAssembly(KeyType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenDependencies)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências proibidas: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Components_ShouldOnlyDependOnSystemAndSharedNamespace()
    {
        var result = Types.InAssembly(KeyType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .Should()
            .OnlyHaveDependenciesOn("System", ComponentNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências fora de System: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    // Nenhuma dependência de IQueryable/ORM: o assembly Domain referencia somente a allowlist aprovada
    [Fact]
    public void DomainAssembly_ShouldKeepApprovedReferencesOnly()
    {
        var references = KeyType.Assembly.GetReferencedAssemblies().Select(a => a.Name).Order(StringComparer.Ordinal).ToArray();

        references.ShouldBe(["System.Collections", "System.Linq.Expressions", "System.Runtime"]);
    }
}

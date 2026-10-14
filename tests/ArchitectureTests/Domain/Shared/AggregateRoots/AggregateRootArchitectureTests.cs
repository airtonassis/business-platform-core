using System.Reflection;
using Business.Platform.Core.Domain.Shared;
using NetArchTest.Rules;

namespace Business.Platform.Core.ArchitectureTests.Domain.Shared.AggregateRoots;

public sealed class AggregateRootArchitectureTests
{
    private const string DomainAssemblyName = "Business.Platform.Core.Domain";
    private const string ComponentNamespace = "Business.Platform.Core.Domain.Shared";
    private const string ComponentNamePattern = "^AggregateRoot(`1)?$";

    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Type AggregateRootType = typeof(AggregateRoot<>);
    private static readonly Type ClosedType = typeof(AggregateRoot<Guid>);

    private static readonly string[] ForbiddenDependencies =
    [
        "Business.Platform.Core.Application",
        "Business.Platform.Core.Infrastructure",
        "Business.Platform.Core.Api",
        "Business.Platform.Core.Contracts",
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.Extensions.Logging",
        "System.Net.Http",
        "System.Text.Json",
        "System.Data"
    ];

    private static readonly string[] ForbiddenMemberKeywords = ["Event", "Tenant", "Repository", "Persist", "Audit", "Created", "Updated", "Deleted"];

    [Fact]
    public void AggregateRoot_ShouldResideInDomainAssembly()
    {
        AggregateRootType.Assembly.GetName().Name.ShouldBe(DomainAssemblyName);
    }

    [Fact]
    public void AggregateRoot_ShouldResideInSharedNamespace()
    {
        AggregateRootType.Namespace.ShouldBe(ComponentNamespace);
    }

    // Garante que as regras de dependência não passam de forma vazia
    [Fact]
    public void ComponentFilter_ShouldSelectOnlyAggregateRoot()
    {
        var selected = Types.InAssembly(AggregateRootType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .GetTypes();

        selected.ShouldHaveSingleItem().ShouldBe(AggregateRootType);
    }

    [Fact]
    public void AggregateRoot_ShouldBePublicAbstractAndNotSealed()
    {
        AggregateRootType.IsClass.ShouldBeTrue();
        AggregateRootType.IsPublic.ShouldBeTrue();
        AggregateRootType.IsAbstract.ShouldBeTrue();
        AggregateRootType.IsSealed.ShouldBeFalse();
    }

    [Fact]
    public void AggregateRoot_ShouldInheritDirectlyFromEntityWithSameIdentifier()
    {
        var baseType = AggregateRootType.BaseType.ShouldNotBeNull();

        baseType.IsGenericType.ShouldBeTrue();
        baseType.GetGenericTypeDefinition().ShouldBe(typeof(Entity<>));
        baseType.GetGenericArguments().ShouldBe(AggregateRootType.GetGenericArguments());
        typeof(Entity<Guid>).IsAssignableFrom(ClosedType).ShouldBeTrue();
        ClosedType.BaseType.ShouldBe(typeof(Entity<Guid>));
    }

    [Fact]
    public void AggregateRoot_ShouldRequireNotNullIdentifier()
    {
        var parameter = AggregateRootType.GetGenericArguments().ShouldHaveSingleItem();

        // `notnull` é emitido como [Nullable(1)] no parâmetro ou herdado de [NullableContext(1)] no tipo
        var annotations = parameter.GetCustomAttributesData()
            .Concat(AggregateRootType.GetCustomAttributesData())
            .Where(data => data.AttributeType.Name is "NullableAttribute" or "NullableContextAttribute")
            .Select(data => data.ConstructorArguments[0].Value)
            .ToArray();

        annotations.ShouldContain((byte)1);
        annotations.ShouldNotContain((byte)2);
    }

    [Fact]
    public void AggregateRoot_ShouldExposeOnlyProtectedConstructorWithIdentifier()
    {
        ClosedType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).ShouldBeEmpty();

        var constructor = ClosedType
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .ShouldHaveSingleItem();

        constructor.IsFamily.ShouldBeTrue();
        constructor.GetParameters().Select(p => p.ParameterType).ShouldBe([typeof(Guid)]);
    }

    // Reflexão: o único membro declarado é o construtor; não há membros sintetizados pelo compilador
    [Fact]
    public void AggregateRoot_ShouldDeclareOnlyTheConstructor()
    {
        var declared = ClosedType.GetMembers(AllDeclared);

        declared.ShouldHaveSingleItem().MemberType.ShouldBe(MemberTypes.Constructor);
    }

    [Fact]
    public void AggregateRoot_ShouldNotDeclareProperties()
    {
        ClosedType.GetProperties(AllDeclared).ShouldBeEmpty();
    }

    [Fact]
    public void AggregateRoot_ShouldNotDeclareFields()
    {
        ClosedType.GetFields(AllDeclared).ShouldBeEmpty();
    }

    [Fact]
    public void AggregateRoot_ShouldNotDeclareMethodsOrEvents()
    {
        ClosedType.GetMethods(AllDeclared).ShouldBeEmpty();
        ClosedType.GetEvents(AllDeclared).ShouldBeEmpty();
        ClosedType.GetNestedTypes(AllDeclared).ShouldBeEmpty();
    }

    [Fact]
    public void AggregateRoot_ShouldNotImplementAdditionalInterfaces()
    {
        var baseInterfaces = typeof(Entity<Guid>).GetInterfaces();

        ClosedType.GetInterfaces().ShouldBe(baseInterfaces, ignoreOrder: true);
    }

    [Theory]
    [InlineData("Equals", typeof(object))]
    [InlineData("Equals", typeof(Entity<Guid>))]
    [InlineData("Equals", typeof(AggregateRoot<Guid>))]
    public void AggregateRoot_ShouldNotRedefineEquals(string name, Type parameterType)
    {
        ClosedType.GetMethod(name, AllDeclared, [parameterType]).ShouldBeNull();
    }

    [Fact]
    public void AggregateRoot_ShouldNotRedefineHashOrOperators()
    {
        ClosedType.GetMethod(nameof(GetHashCode), AllDeclared, Type.EmptyTypes).ShouldBeNull();
        ClosedType.GetMethod("op_Equality", AllDeclared).ShouldBeNull();
        ClosedType.GetMethod("op_Inequality", AllDeclared).ShouldBeNull();
    }

    // Os membros herdados continuam sendo os de Entity
    [Fact]
    public void AggregateRoot_ShouldResolveEqualityMembersFromEntity()
    {
        ClosedType.GetMethod(nameof(Equals), [typeof(object)])!.DeclaringType.ShouldBe(typeof(Entity<Guid>));
        ClosedType.GetMethod(nameof(GetHashCode), Type.EmptyTypes)!.DeclaringType.ShouldBe(typeof(Entity<Guid>));
        ClosedType.GetProperty(nameof(Entity<Guid>.Id))!.DeclaringType.ShouldBe(typeof(Entity<Guid>));
    }

    [Fact]
    public void AggregateRoot_ShouldNotDependOnForbiddenLayersOrFrameworks()
    {
        var result = Types.InAssembly(AggregateRootType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenDependencies)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências proibidas: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void AggregateRoot_ShouldOnlyDependOnSystemAndSharedNamespace()
    {
        var result = Types.InAssembly(AggregateRootType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .Should()
            .OnlyHaveDependenciesOn("System", ComponentNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências fora de System: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Theory]
    [InlineData("Business.Platform.Core.Domain.Shared.Error")]
    [InlineData("Business.Platform.Core.Domain.Shared.Result")]
    public void AggregateRoot_ShouldNotDependOnErrorOrResult(string dependency)
    {
        var result = Types.InAssembly(AggregateRootType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .ShouldNot()
            .HaveDependencyOn(dependency)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    // Sem eventos, persistência, auditoria ou multitenancy: nenhum membro declarado com esses nomes
    [Fact]
    public void AggregateRoot_ShouldNotDeclareEventPersistenceAuditOrTenantMembers()
    {
        var names = ClosedType.GetMembers(AllDeclared).Select(member => member.Name);

        names.Where(name => ForbiddenMemberKeywords.Any(keyword => name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ShouldBeEmpty();
    }
}

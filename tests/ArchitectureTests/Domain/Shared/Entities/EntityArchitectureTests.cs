using System.Reflection;
using Business.Platform.Core.Domain.Shared;
using NetArchTest.Rules;

namespace Business.Platform.Core.ArchitectureTests.Domain.Shared.Entities;

public sealed class EntityArchitectureTests
{
    private const string DomainAssemblyName = "Business.Platform.Core.Domain";
    private const string EntityNamespace = "Business.Platform.Core.Domain.Shared";
    private const string ComponentNamePattern = "^Entity(`1)?$";

    private const BindingFlags AllInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly Type EntityType = typeof(Entity<>);
    private static readonly Type ClosedType = typeof(Entity<Guid>);

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

    [Fact]
    public void Entity_ShouldResideInDomainAssembly()
    {
        EntityType.Assembly.GetName().Name.ShouldBe(DomainAssemblyName);
    }

    [Fact]
    public void Entity_ShouldResideInSharedNamespace()
    {
        EntityType.Namespace.ShouldBe(EntityNamespace);
    }

    // Garante que as regras de dependência não passam de forma vazia
    [Fact]
    public void ComponentFilter_ShouldSelectOnlyEntity()
    {
        var selected = Types.InAssembly(EntityType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .GetTypes();

        selected.ShouldHaveSingleItem().ShouldBe(EntityType);
    }

    [Fact]
    public void Entity_ShouldNotDependOnForbiddenLayersOrFrameworks()
    {
        var result = Types.InAssembly(EntityType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenDependencies)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências proibidas: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    // L4 — Entity depende apenas da BCL (System.*) e de si mesma
    [Fact]
    public void Entity_ShouldOnlyDependOnSystemNamespaces()
    {
        var result = Types.InAssembly(EntityType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .Should()
            .OnlyHaveDependenciesOn("System", EntityNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências fora de System: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Theory]
    [InlineData("Business.Platform.Core.Domain.Shared.Error")]
    [InlineData("Business.Platform.Core.Domain.Shared.Result")]
    public void Entity_ShouldNotDependOnErrorOrResult(string dependency)
    {
        var result = Types.InAssembly(EntityType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .ShouldNot()
            .HaveDependencyOn(dependency)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void Entity_ShouldBeAbstractAndNotSealed()
    {
        EntityType.IsClass.ShouldBeTrue();
        EntityType.IsAbstract.ShouldBeTrue();
        EntityType.IsSealed.ShouldBeFalse();
    }

    [Fact]
    public void Entity_ShouldRequireNotNullIdentifier()
    {
        var parameter = EntityType.GetGenericArguments().ShouldHaveSingleItem();

        // `notnull` é emitido como [Nullable(1)] no parâmetro ou herdado de [NullableContext(1)] no tipo
        var annotations = parameter.GetCustomAttributesData()
            .Concat(EntityType.GetCustomAttributesData())
            .Where(data => data.AttributeType.Name is "NullableAttribute" or "NullableContextAttribute")
            .Select(data => data.ConstructorArguments[0].Value)
            .ToArray();

        annotations.ShouldContain((byte)1);
        annotations.ShouldNotContain((byte)2);
    }

    [Fact]
    public void Entity_ShouldImplementIEquatable()
    {
        typeof(IEquatable<Entity<Guid>>).IsAssignableFrom(ClosedType).ShouldBeTrue();
    }

    [Fact]
    public void Entity_ShouldExposeOnlyProtectedConstructor()
    {
        ClosedType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).ShouldBeEmpty();

        var constructor = ClosedType.GetConstructors(AllInstance).ShouldHaveSingleItem();
        constructor.IsFamily.ShouldBeTrue();
        constructor.GetParameters().Select(p => p.ParameterType).ShouldBe([typeof(Guid)]);
    }

    [Fact]
    public void Id_ShouldHavePublicGetterAndNoSetterOfAnyVisibility()
    {
        var property = ClosedType.GetProperty(nameof(Entity<Guid>.Id), AllInstance).ShouldNotBeNull();

        property.GetGetMethod(nonPublic: false).ShouldNotBeNull();
        property.GetSetMethod(nonPublic: true).ShouldBeNull();
        property.CanWrite.ShouldBeFalse();
    }

    [Fact]
    public void Entity_ShouldNotExposeRelationshipsOrExtraPublicMembers()
    {
        ClosedType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Select(p => p.Name)
            .ShouldBe([nameof(Entity<Guid>.Id)]);

        ClosedType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Where(f => f.Name != "<Id>k__BackingField")
            .ShouldBeEmpty();
    }

    [Fact]
    public void Equals_Object_ShouldBeSealedOverride()
    {
        var method = ClosedType.GetMethod(nameof(Equals), [typeof(object)]).ShouldNotBeNull();

        method.DeclaringType.ShouldBe(ClosedType);
        method.IsVirtual.ShouldBeTrue();
        method.IsFinal.ShouldBeTrue();
        method.GetBaseDefinition().DeclaringType.ShouldBe(typeof(object));
    }

    [Fact]
    public void GetHashCode_ShouldBeSealedOverride()
    {
        var method = ClosedType.GetMethod(nameof(GetHashCode), Type.EmptyTypes).ShouldNotBeNull();

        method.DeclaringType.ShouldBe(ClosedType);
        method.IsVirtual.ShouldBeTrue();
        method.IsFinal.ShouldBeTrue();
        method.GetBaseDefinition().DeclaringType.ShouldBe(typeof(object));
    }

    [Fact]
    public void Equals_Typed_ShouldNotBeVirtual()
    {
        var method = ClosedType.GetMethod(nameof(Equals), [typeof(Entity<Guid>)]).ShouldNotBeNull();

        method.DeclaringType.ShouldBe(ClosedType);

        // A implementação implícita de IEquatable<T> é emitida como virtual final: não pode ser sobrescrita
        (!method.IsVirtual || method.IsFinal).ShouldBeTrue();
    }

    [Fact]
    public void Entity_ShouldDeclareEqualityOperators()
    {
        var equality = ClosedType.GetMethod("op_Equality", BindingFlags.Public | BindingFlags.Static).ShouldNotBeNull();
        var inequality = ClosedType.GetMethod("op_Inequality", BindingFlags.Public | BindingFlags.Static).ShouldNotBeNull();

        equality.ReturnType.ShouldBe(typeof(bool));
        inequality.ReturnType.ShouldBe(typeof(bool));
        equality.GetParameters().Select(p => p.ParameterType).ShouldBe([ClosedType, ClosedType]);
        inequality.GetParameters().Select(p => p.ParameterType).ShouldBe([ClosedType, ClosedType]);
    }
}

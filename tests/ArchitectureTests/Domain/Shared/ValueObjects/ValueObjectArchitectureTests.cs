using System.Reflection;
using Business.Platform.Core.Domain.Shared;
using NetArchTest.Rules;

namespace Business.Platform.Core.ArchitectureTests.Domain.Shared.ValueObjects;

public sealed class ValueObjectArchitectureTests
{
    private const string DomainAssemblyName = "Business.Platform.Core.Domain";
    private const string ValueObjectNamespace = "Business.Platform.Core.Domain.Shared";
    private const string ComponentNamePattern = "^ValueObject$";

    private const BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static readonly Type ValueObjectType = typeof(ValueObject);

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
    public void ValueObject_ShouldResideInDomainAssembly()
    {
        ValueObjectType.Assembly.GetName().Name.ShouldBe(DomainAssemblyName);
    }

    [Fact]
    public void ValueObject_ShouldResideInSharedNamespace()
    {
        ValueObjectType.Namespace.ShouldBe(ValueObjectNamespace);
    }

    // Garante que as regras de dependência não passam de forma vazia
    [Fact]
    public void ComponentFilter_ShouldSelectOnlyValueObject()
    {
        var selected = Types.InAssembly(ValueObjectType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .GetTypes();

        selected.ShouldHaveSingleItem().ShouldBe(ValueObjectType);
    }

    [Fact]
    public void ValueObject_ShouldBePublicAbstractAndNotSealed()
    {
        ValueObjectType.IsClass.ShouldBeTrue();
        ValueObjectType.IsPublic.ShouldBeTrue();
        ValueObjectType.IsAbstract.ShouldBeTrue();
        ValueObjectType.IsSealed.ShouldBeFalse();
        ValueObjectType.IsGenericType.ShouldBeFalse();
    }

    [Fact]
    public void ValueObject_ShouldImplementIEquatableOfValueObject()
    {
        typeof(IEquatable<ValueObject>).IsAssignableFrom(ValueObjectType).ShouldBeTrue();
    }

    [Fact]
    public void ValueObject_ShouldDeriveDirectlyFromObject()
    {
        ValueObjectType.BaseType.ShouldBe(typeof(object));
    }

    [Fact]
    public void GetEqualityComponents_ShouldBeProtectedAbstract()
    {
        var method = ValueObjectType.GetMethod("GetEqualityComponents", AllMembers).ShouldNotBeNull();

        method.IsFamily.ShouldBeTrue();
        method.IsAbstract.ShouldBeTrue();
        method.IsStatic.ShouldBeFalse();
        method.GetParameters().ShouldBeEmpty();
        method.ReturnType.ShouldBe(typeof(IEnumerable<object?>));
    }

    [Fact]
    public void Equals_Object_ShouldBeSealedOverride()
    {
        var method = ValueObjectType.GetMethod(nameof(Equals), [typeof(object)]).ShouldNotBeNull();

        method.DeclaringType.ShouldBe(ValueObjectType);
        method.IsVirtual.ShouldBeTrue();
        method.IsFinal.ShouldBeTrue();
        method.GetBaseDefinition().DeclaringType.ShouldBe(typeof(object));
    }

    [Fact]
    public void GetHashCode_ShouldBeSealedOverride()
    {
        var method = ValueObjectType.GetMethod(nameof(GetHashCode), Type.EmptyTypes).ShouldNotBeNull();

        method.DeclaringType.ShouldBe(ValueObjectType);
        method.IsVirtual.ShouldBeTrue();
        method.IsFinal.ShouldBeTrue();
        method.GetBaseDefinition().DeclaringType.ShouldBe(typeof(object));
    }

    [Fact]
    public void Equals_Typed_ShouldNotBeOverridable()
    {
        var method = ValueObjectType.GetMethod(nameof(Equals), [ValueObjectType]).ShouldNotBeNull();

        method.DeclaringType.ShouldBe(ValueObjectType);
        method.IsPublic.ShouldBeTrue();
        method.ReturnType.ShouldBe(typeof(bool));

        // A implementação implícita de IEquatable<T> é emitida como virtual final: não pode ser sobrescrita
        (!method.IsVirtual || method.IsFinal).ShouldBeTrue();
    }

    [Fact]
    public void ValueObject_ShouldDeclareEqualityOperators()
    {
        var equality = ValueObjectType.GetMethod("op_Equality", BindingFlags.Public | BindingFlags.Static).ShouldNotBeNull();
        var inequality = ValueObjectType.GetMethod("op_Inequality", BindingFlags.Public | BindingFlags.Static).ShouldNotBeNull();

        equality.ReturnType.ShouldBe(typeof(bool));
        inequality.ReturnType.ShouldBe(typeof(bool));
        equality.GetParameters().Select(p => p.ParameterType).ShouldBe([ValueObjectType, ValueObjectType]);
        inequality.GetParameters().Select(p => p.ParameterType).ShouldBe([ValueObjectType, ValueObjectType]);
    }

    [Fact]
    public void ValueObject_ShouldNotCacheHashOrDeclareState()
    {
        ValueObjectType.GetFields(AllMembers).ShouldBeEmpty();
        ValueObjectType.GetProperties(AllMembers).ShouldBeEmpty();
    }

    [Fact]
    public void ValueObject_ShouldExposeOnlyContractMethods()
    {
        ValueObjectType.GetMethods(AllMembers | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Order()
            .ShouldBe(
            [
                "Equals",
                "Equals",
                "GetEqualityComponents",
                "GetHashCode",
                "op_Equality",
                "op_Inequality"
            ]);
    }

    [Fact]
    public void ValueObject_ShouldNotDependOnForbiddenLayersOrFrameworks()
    {
        var result = Types.InAssembly(ValueObjectType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenDependencies)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências proibidas: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void ValueObject_ShouldOnlyDependOnSystemNamespaces()
    {
        var result = Types.InAssembly(ValueObjectType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .Should()
            .OnlyHaveDependenciesOn("System", ValueObjectNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências fora de System: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Theory]
    [InlineData("Business.Platform.Core.Domain.Shared.Error")]
    [InlineData("Business.Platform.Core.Domain.Shared.Result")]
    [InlineData("Business.Platform.Core.Domain.Shared.Entity")]
    [InlineData("Business.Platform.Core.Domain.Shared.AggregateRoot")]
    [InlineData("Business.Platform.Core.Domain.Shared.IDomainEvent")]
    public void ValueObject_ShouldNotDependOnOtherFoundations(string dependency)
    {
        var result = Types.InAssembly(ValueObjectType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .ShouldNot()
            .HaveDependencyOn(dependency)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void OtherFoundations_ShouldNotDependOnValueObject()
    {
        var result = Types.InAssembly(ValueObjectType.Assembly)
            .That()
            .DoNotHaveNameMatching(ComponentNamePattern)
            .ShouldNot()
            .HaveDependencyOn(ValueObjectType.FullName!)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos que dependem de ValueObject: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void PreviousFoundations_ShouldRemainInPlace()
    {
        typeof(Entity<>).Namespace.ShouldBe(ValueObjectNamespace);
        typeof(AggregateRoot<>).Namespace.ShouldBe(ValueObjectNamespace);
        typeof(IDomainEvent).Namespace.ShouldBe(ValueObjectNamespace);
        typeof(Error).Namespace.ShouldBe(ValueObjectNamespace);
        typeof(Result).Namespace.ShouldBe(ValueObjectNamespace);
    }
}

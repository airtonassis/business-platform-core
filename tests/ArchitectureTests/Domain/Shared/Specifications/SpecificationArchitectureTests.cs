using Specs = Business.Platform.Core.Domain.Shared.Specifications;
using System.Linq.Expressions;
using System.Reflection;
using Business.Platform.Core.Domain.Shared;
using NetArchTest.Rules;

namespace Business.Platform.Core.ArchitectureTests.Domain.Shared.Specifications;

public sealed class SpecificationArchitectureTests
{
    private const string DomainAssemblyName = "Business.Platform.Core.Domain";
    private const string ComponentNamespace = "Business.Platform.Core.Domain.Shared";
    private const string ComponentNamePattern = "^(ISpecification(`1)?|Specification(`1)?|Specifications|SpecificationExtensions)$";

    private static readonly Type InterfaceType = typeof(ISpecification<>);
    private static readonly Type ImplementationType = typeof(Specification<>);
    private static readonly Type FactoriesType = typeof(Specs);
    private static readonly Type ExtensionsType = typeof(SpecificationExtensions);

    private static readonly Type[] ComponentTypes = [InterfaceType, ImplementationType, FactoriesType, ExtensionsType];

    private static readonly string[] ForbiddenDependencies =
    [
        "Business.Platform.Core.Application",
        "Business.Platform.Core.Infrastructure",
        "Business.Platform.Core.Api",
        "Business.Platform.Core.Contracts",
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.Extensions.Logging",
        "Dapper",
        "NHibernate",
        "System.Data",
        "System.Net.Http",
        "System.Text.Json"
    ];

    public static TheoryData<Type> Components => new(ComponentTypes);

    [Theory]
    [MemberData(nameof(Components))]
    public void Component_ShouldResideInDomainAssemblyAndSharedNamespace(Type type)
    {
        type.Assembly.GetName().Name.ShouldBe(DomainAssemblyName);
        type.Namespace.ShouldBe(ComponentNamespace);
        type.IsPublic.ShouldBeTrue();
    }

    // Garante que as regras de dependência não passam de forma vazia
    [Fact]
    public void ComponentFilter_ShouldSelectExactlyTheFourComponents()
    {
        var selected = Types.InAssembly(InterfaceType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .GetTypes();

        selected.OrderBy(type => type.Name).ShouldBe(ComponentTypes.OrderBy(type => type.Name));
    }

    [Fact]
    public void Specification_ShouldBePublicGenericInterfaceWithSingleParameter()
    {
        InterfaceType.IsInterface.ShouldBeTrue();
        InterfaceType.IsPublic.ShouldBeTrue();
        InterfaceType.IsGenericTypeDefinition.ShouldBeTrue();
        InterfaceType.GetGenericArguments().Select(a => a.Name).ShouldBe(["T"]);
        InterfaceType.GetGenericArguments()[0].GetGenericParameterConstraints().ShouldBeEmpty();
        InterfaceType.GetGenericArguments()[0].GenericParameterAttributes.ShouldBe(GenericParameterAttributes.None);
    }

    [Fact]
    public void Specification_ShouldDeclareSingleReadOnlyCriteriaProperty()
    {
        var property = InterfaceType.GetProperties().ShouldHaveSingleItem();

        property.Name.ShouldBe("Criteria");
        property.CanRead.ShouldBeTrue();
        property.CanWrite.ShouldBeFalse();
        property.PropertyType.ShouldBe(typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(InterfaceType.GetGenericArguments()[0], typeof(bool))));
    }

    [Fact]
    public void Specification_ShouldNotDeclareExtraMembersOrBaseInterfaces()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        InterfaceType.GetMethods(all).Select(m => m.Name).ShouldBe(["get_Criteria"]);
        InterfaceType.GetMethods(all).ShouldAllBe(m => m.IsAbstract && m.GetMethodBody() == null);
        InterfaceType.GetEvents(all).ShouldBeEmpty();
        InterfaceType.GetFields(all).ShouldBeEmpty();
        InterfaceType.GetNestedTypes(all).ShouldBeEmpty();
        InterfaceType.GetInterfaces().ShouldBeEmpty();
        InterfaceType.GetMembers(all).Length.ShouldBe(2);
    }

    [Fact]
    public void Implementation_ShouldBePublicSealedClassImplementingInterface()
    {
        ImplementationType.IsClass.ShouldBeTrue();
        ImplementationType.IsPublic.ShouldBeTrue();
        ImplementationType.IsSealed.ShouldBeTrue();
        ImplementationType.IsAbstract.ShouldBeFalse();
        ImplementationType.GetInterfaces().ShouldHaveSingleItem().GetGenericTypeDefinition().ShouldBe(InterfaceType);
        ImplementationType.BaseType.ShouldBe(typeof(object));
    }

    [Fact]
    public void Implementation_ShouldHaveSinglePublicConstructorReceivingCriteria()
    {
        var constructor = ImplementationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).ShouldHaveSingleItem();
        var parameter = constructor.GetParameters().ShouldHaveSingleItem();
        var argument = ImplementationType.GetGenericArguments()[0];

        parameter.Name.ShouldBe("criteria");
        parameter.ParameterType.ShouldBe(typeof(Expression<>).MakeGenericType(typeof(Func<,>).MakeGenericType(argument, typeof(bool))));
        ImplementationType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).ShouldBeEmpty();
    }

    [Fact]
    public void Implementation_ShouldNotExposeSetterOrMutableState()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var property = ImplementationType.GetProperty("Criteria").ShouldNotBeNull();

        property.SetMethod.ShouldBeNull();
        ImplementationType.GetProperties(all).Where(p => p.SetMethod is not null).ShouldBeEmpty();
        ImplementationType.GetFields(all).Where(f => !f.IsInitOnly).ShouldBeEmpty();
        ImplementationType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .ShouldBeEmpty();
    }

    [Fact]
    public void Extensions_ShouldBePublicStaticClass()
    {
        ExtensionsType.IsClass.ShouldBeTrue();
        ExtensionsType.IsAbstract.ShouldBeTrue();
        ExtensionsType.IsSealed.ShouldBeTrue();
        ExtensionsType.IsPublic.ShouldBeTrue();
    }

    [Theory]
    [InlineData("And", new[] { "left", "right" })]
    [InlineData("Or", new[] { "left", "right" })]
    [InlineData("Not", new[] { "specification" })]
    public void Extensions_ShouldExposeApprovedCompositionSignatures(string name, string[] parameterNames)
    {
        var method = ExtensionsType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m.Name == name)
            .ShouldHaveSingleItem();
        var typeArgument = method.GetGenericArguments().ShouldHaveSingleItem();
        var specification = InterfaceType.MakeGenericType(typeArgument);

        method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false).ShouldBeTrue();
        method.ReturnType.ShouldBe(specification);
        method.GetParameters().Select(p => p.Name).ShouldBe(parameterNames);
        method.GetParameters().ShouldAllBe(p => p.ParameterType == specification && !p.IsOptional);
    }

    [Fact]
    public void Extensions_ShouldExposeOnlyApprovedPublicMethods()
    {
        ExtensionsType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name).Order().ShouldBe(["And", "Not", "Or"]);
        ExtensionsType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).ShouldBeEmpty();
        ExtensionsType.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).ShouldBeEmpty();
    }

    [Fact]
    public void Factories_ShouldBePublicStaticClassWithTrueAndFalse()
    {
        FactoriesType.IsAbstract.ShouldBeTrue();
        FactoriesType.IsSealed.ShouldBeTrue();
        FactoriesType.IsPublic.ShouldBeTrue();

        var methods = FactoriesType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        methods.Select(m => m.Name).Order().ShouldBe(["False", "True"]);

        foreach (var method in methods)
        {
            var typeArgument = method.GetGenericArguments().ShouldHaveSingleItem();

            method.GetParameters().ShouldBeEmpty();
            method.ReturnType.ShouldBe(InterfaceType.MakeGenericType(typeArgument));
        }
    }

    [Fact]
    public void PublicSurface_ShouldNotUseFuncOrQueryableAsContract()
    {
        var signatures = ComponentTypes
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType))
            .Concat(ComponentTypes.SelectMany(t => t.GetProperties().Select(p => p.PropertyType)))
            .ToArray();

        signatures.ShouldNotContain(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Func<,>));
        signatures.ShouldNotContain(t => typeof(System.Collections.IEnumerable).IsAssignableFrom(t) && t != typeof(string));
    }

    [Fact]
    public void Component_ShouldNotDependOnForbiddenLayersOrFrameworks()
    {
        var result = Types.InAssembly(InterfaceType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenDependencies)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências proibidas: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Component_ShouldNotDependOnQueryableOrOrmNamespaces()
    {
        var result = Types.InAssembly(InterfaceType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .ShouldNot()
            .HaveDependencyOnAny("System.Linq.IQueryable", "System.Linq.Queryable", "System.Data", "Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências proibidas: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Component_ShouldOnlyDependOnSystemAndSharedNamespace()
    {
        var result = Types.InAssembly(InterfaceType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .Should()
            .OnlyHaveDependenciesOn("System", ComponentNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências fora de System: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    // ADR-0003: a allowlist passa a conter exatamente System.Runtime, System.Collections e System.Linq.Expressions
    [Fact]
    public void DomainAssembly_ShouldReferenceExactlyTheAssembliesAuthorizedByAdrs()
    {
        var references = InterfaceType.Assembly.GetReferencedAssemblies();

        references.Select(r => r.Name).Order().ShouldBe(["System.Collections", "System.Linq.Expressions", "System.Runtime"]);
        references.ShouldAllBe(r => (r.GetPublicKeyToken() ?? Array.Empty<byte>()).SequenceEqual(Convert.FromHexString("b03f5f7f11d50a3a")));
    }

    // Repository 1.0 permanece inalterado: Specification não é incorporada ao contrato
    [Fact]
    public void Repository_ShouldRemainUnchanged()
    {
        var repository = typeof(IRepository<,>);

        repository.GetMethods().Select(m => m.Name).Order().ShouldBe(["AddAsync", "GetByIdAsync", "RemoveAsync", "UpdateAsync"]);
        repository.GetMethods()
            .SelectMany(m => m.GetParameters())
            .ShouldNotContain(p => p.ParameterType.IsGenericType && p.ParameterType.GetGenericTypeDefinition() == InterfaceType);
        repository.GetProperties().ShouldBeEmpty();
        repository.GetInterfaces().ShouldBeEmpty();
    }
}

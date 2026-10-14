using System.Reflection;
using Business.Platform.Core.Domain.Shared;
using NetArchTest.Rules;

namespace Business.Platform.Core.ArchitectureTests.Domain.Shared.Repositories;

public sealed class RepositoryArchitectureTests
{
    private const string DomainAssemblyName = "Business.Platform.Core.Domain";
    private const string ComponentNamespace = "Business.Platform.Core.Domain.Shared";
    private const string ComponentNamePattern = "^IRepository(`2)?$";

    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Type RepositoryType = typeof(IRepository<,>);

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
        "System.Net.Http",
        "System.Text.Json",
        "System.Data"
    ];

    private static readonly string[] ExpectedMethods = ["AddAsync", "GetByIdAsync", "RemoveAsync", "UpdateAsync"];

    [Fact]
    public void Repository_ShouldResideInDomainAssemblyAndSharedNamespace()
    {
        RepositoryType.Assembly.GetName().Name.ShouldBe(DomainAssemblyName);
        RepositoryType.Namespace.ShouldBe(ComponentNamespace);
    }

    // Garante que as regras de dependência não passam de forma vazia
    [Fact]
    public void ComponentFilter_ShouldSelectOnlyRepository()
    {
        var selected = Types.InAssembly(RepositoryType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .GetTypes();

        selected.ShouldHaveSingleItem().ShouldBe(RepositoryType);
    }

    [Fact]
    public void Repository_ShouldBePublicInterface()
    {
        RepositoryType.IsInterface.ShouldBeTrue();
        RepositoryType.IsPublic.ShouldBeTrue();
    }

    [Fact]
    public void Repository_ShouldHaveAggregateAndIdParameters()
    {
        RepositoryType.GetGenericArguments().Select(a => a.Name).ShouldBe(["TAggregate", "TId"]);
    }

    [Fact]
    public void Repository_ShouldConstrainAggregateToAggregateRootOfSameId()
    {
        var arguments = RepositoryType.GetGenericArguments();
        var constraint = arguments[0].GetGenericParameterConstraints().ShouldHaveSingleItem();

        constraint.IsGenericType.ShouldBeTrue();
        constraint.GetGenericTypeDefinition().ShouldBe(typeof(AggregateRoot<>));
        constraint.GetGenericArguments().ShouldHaveSingleItem().ShouldBe(arguments[1]);
        arguments[0].GenericParameterAttributes.ShouldBe(GenericParameterAttributes.None);
    }

    // Limite de reflexão: `notnull` do C# não é a constraint CLR `struct`/`class`/`new()`.
    // O compilador não emite GenericParameterAttributes para ele; emite apenas anotação de nulabilidade.
    // Por isso verificamos (1) a ausência de qualquer constraint CLR em TId e (2) a anotação de nulabilidade.
    [Fact]
    public void Repository_IdentifierShouldHaveNoClrConstraintsBeyondNotNullAnnotation()
    {
        var parameter = RepositoryType.GetGenericArguments()[1];

        parameter.GenericParameterAttributes.ShouldBe(GenericParameterAttributes.None);
        parameter.GetGenericParameterConstraints().ShouldBeEmpty();
    }

    [Fact]
    public void Repository_ShouldRequireNotNullIdentifier()
    {
        var parameter = RepositoryType.GetGenericArguments()[1];

        // `notnull` é emitido como [Nullable(1)] no parâmetro ou herdado de [NullableContext(1)] no tipo
        var annotations = parameter.GetCustomAttributesData()
            .Concat(RepositoryType.GetCustomAttributesData())
            .Where(data => data.AttributeType.Name is "NullableAttribute" or "NullableContextAttribute")
            .Select(data => data.ConstructorArguments[0].Value)
            .ToArray();

        annotations.ShouldContain((byte)1);
        annotations.ShouldNotContain((byte)2);
    }

    [Fact]
    public void Repository_ShouldDeclareExactlyFourMethods()
    {
        RepositoryType.GetMembers().ShouldAllBe(m => m.MemberType == MemberTypes.Method);
        RepositoryType.GetMethods().Select(m => m.Name).Order().ShouldBe(ExpectedMethods);
    }

    // Todos os métodos são públicos, de instância e abstratos (sem implementação default nem métodos estáticos)
    [Fact]
    public void Repository_MethodsShouldBePublicInstanceAbstractWithoutDefaultImplementation()
    {
        var methods = RepositoryType.GetMethods(AllDeclared);

        methods.ShouldAllBe(m => m.IsPublic && !m.IsStatic && m.IsAbstract && m.IsVirtual && !m.IsFinal);
        methods.ShouldAllBe(m => m.GetMethodBody() == null);
    }

    // Sem métodos privados/internos/protegidos/estáticos extras: o conjunto de todos os métodos declarados é exatamente o aprovado
    [Fact]
    public void Repository_ShouldNotDeclareNonPublicOrStaticMethods()
    {
        RepositoryType.GetMethods(AllDeclared).Select(m => m.Name).Order().ShouldBe(ExpectedMethods);
        RepositoryType.GetMethods(AllDeclared & ~BindingFlags.Public).ShouldBeEmpty();
        RepositoryType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).ShouldBeEmpty();
        RepositoryType.GetConstructors(AllDeclared).ShouldBeEmpty();
    }

    [Fact]
    public void Repository_ShouldNotDeclarePropertiesEventsFieldsNestedTypesOrBaseInterfaces()
    {
        const BindingFlags all = AllDeclared;

        RepositoryType.GetProperties(all).ShouldBeEmpty();
        RepositoryType.GetEvents(all).ShouldBeEmpty();
        RepositoryType.GetFields(all).ShouldBeEmpty();
        RepositoryType.GetNestedTypes(all).ShouldBeEmpty();
        RepositoryType.GetInterfaces().ShouldBeEmpty();
    }

    [Fact]
    public void GetByIdAsync_ShouldReturnTaskOfResultOfAggregate()
    {
        var method = RepositoryType.GetMethod("GetByIdAsync").ShouldNotBeNull();
        var aggregate = RepositoryType.GetGenericArguments()[0];
        var id = RepositoryType.GetGenericArguments()[1];

        method.ReturnType.ShouldBe(typeof(Task<>).MakeGenericType(typeof(Result<>).MakeGenericType(aggregate)));
        method.GetParameters().Select(p => (p.Name, p.ParameterType))
            .ShouldBe([("id", id), ("cancellationToken", typeof(CancellationToken))]);
    }

    [Theory]
    [InlineData("AddAsync")]
    [InlineData("UpdateAsync")]
    [InlineData("RemoveAsync")]
    public void WriteMethods_ShouldReturnTaskOfResultAndAcceptAggregate(string name)
    {
        var method = RepositoryType.GetMethod(name).ShouldNotBeNull();
        var aggregate = RepositoryType.GetGenericArguments()[0];

        method.ReturnType.ShouldBe(typeof(Task<Result>));
        method.GetParameters().Select(p => (p.Name, p.ParameterType))
            .ShouldBe([("aggregate", aggregate), ("cancellationToken", typeof(CancellationToken))]);
    }

    [Theory]
    [InlineData("GetByIdAsync")]
    [InlineData("AddAsync")]
    [InlineData("UpdateAsync")]
    [InlineData("RemoveAsync")]
    public void AllMethods_ShouldHaveOptionalCancellationTokenWithDefault(string name)
    {
        var token = RepositoryType.GetMethod(name)!.GetParameters().Last();

        token.ParameterType.ShouldBe(typeof(CancellationToken));
        token.IsOptional.ShouldBeTrue();
        token.HasDefaultValue.ShouldBeTrue();

        // `= default` para struct pode ser refletido como null; ambos equivalem a default(CancellationToken)
        var effective = token.DefaultValue is null ? default : (CancellationToken)token.DefaultValue;
        effective.ShouldBe(default(CancellationToken));
        effective.CanBeCanceled.ShouldBeFalse();
    }

    [Fact]
    public void Repository_ShouldNotDependOnForbiddenLayersOrFrameworks()
    {
        var result = Types.InAssembly(RepositoryType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenDependencies)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências proibidas: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Repository_ShouldOnlyDependOnSystemAndSharedNamespace()
    {
        var result = Types.InAssembly(RepositoryType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .Should()
            .OnlyHaveDependenciesOn("System", ComponentNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências fora de System: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    // Esta feature entrega apenas o contrato; implementações pertencem a features futuras
    [Fact]
    public void Repository_ShouldHaveNoImplementationInDomainAssembly()
    {
        var implementations = RepositoryType.Assembly.GetTypes()
            .Where(t => t.IsClass && t.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == RepositoryType))
            .Select(t => t.FullName);

        implementations.ShouldBeEmpty();
    }
}

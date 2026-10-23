using System.Reflection;
using Business.Platform.Core.Domain.Shared;
using NetArchTest.Rules;

namespace Business.Platform.Core.ArchitectureTests.Domain.Shared.Repositories;

public sealed class SpecificationRepositoryArchitectureTests
{
    private const string ComponentNamespace = "Business.Platform.Core.Domain.Shared";
    private const string ComponentNamePattern = "^ISpecificationRepository(`2)?$";

    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Type ContractType = typeof(ISpecificationRepository<,>);

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
        "System.Text.Json"
    ];

    [Fact]
    public void Contract_ShouldBePublicInterfaceInDomainSharedNamespace()
    {
        ContractType.IsInterface.ShouldBeTrue();
        ContractType.IsPublic.ShouldBeTrue();
        ContractType.Namespace.ShouldBe(ComponentNamespace);
        ContractType.Assembly.GetName().Name.ShouldBe("Business.Platform.Core.Domain");
    }

    [Fact]
    public void ComponentFilter_ShouldSelectOnlyContract()
    {
        Types.InAssembly(ContractType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .GetTypes()
            .ShouldHaveSingleItem()
            .ShouldBe(ContractType);
    }

    [Fact]
    public void Contract_ShouldHaveAggregateAndIdParametersWithApprovedConstraints()
    {
        var arguments = ContractType.GetGenericArguments();
        var constraint = arguments[0].GetGenericParameterConstraints().ShouldHaveSingleItem();

        arguments.Select(a => a.Name).ShouldBe(["TAggregate", "TId"]);
        constraint.GetGenericTypeDefinition().ShouldBe(typeof(AggregateRoot<>));
        constraint.GetGenericArguments().ShouldHaveSingleItem().ShouldBe(arguments[1]);
        arguments[0].GenericParameterAttributes.ShouldBe(GenericParameterAttributes.None);
        arguments[1].GenericParameterAttributes.ShouldBe(GenericParameterAttributes.None);
        arguments[1].GetGenericParameterConstraints().ShouldBeEmpty();
    }

    [Fact]
    public void Contract_ShouldRequireNotNullIdentifier()
    {
        var parameter = ContractType.GetGenericArguments()[1];

        var annotations = parameter.GetCustomAttributesData()
            .Concat(ContractType.GetCustomAttributesData())
            .Where(data => data.AttributeType.Name is "NullableAttribute" or "NullableContextAttribute")
            .Select(data => data.ConstructorArguments[0].Value)
            .ToArray();

        annotations.ShouldContain((byte)1);
        annotations.ShouldNotContain((byte)2);
    }

    // ADR-0004: contrato aditivo; não herda IRepository 1.0 nem qualquer outra interface
    [Fact]
    public void Contract_ShouldNotInheritFromAnyInterface()
    {
        ContractType.GetInterfaces().ShouldBeEmpty();
        ContractType.IsAssignableTo(typeof(IRepository<,>)).ShouldBeFalse();
    }

    [Fact]
    public void Contract_ShouldDeclareExactlyOneMethodAndNothingElse()
    {
        ContractType.GetMembers(AllDeclared).ShouldAllBe(m => m.MemberType == MemberTypes.Method);
        ContractType.GetMethods(AllDeclared).Select(m => m.Name).ShouldBe(["FindPagedAsync"]);
        ContractType.GetConstructors(AllDeclared).ShouldBeEmpty();
        ContractType.GetNestedTypes(AllDeclared).ShouldBeEmpty();
    }

    // Sem FindAsync ilimitado: nenhum método de consulta além de FindPagedAsync
    [Fact]
    public void Contract_ShouldNotDeclareUnboundedFindMethods()
    {
        ContractType.GetMethods(AllDeclared).Select(m => m.Name).ShouldNotContain("FindAsync");
        ContractType.GetMethods(AllDeclared).ShouldAllBe(m => m.Name == "FindPagedAsync");
    }

    [Fact]
    public void FindPagedAsync_ShouldHaveExactApprovedSignature()
    {
        var method = ContractType.GetMethod("FindPagedAsync").ShouldNotBeNull();
        var aggregate = ContractType.GetGenericArguments()[0];
        var id = ContractType.GetGenericArguments()[1];

        method.IsPublic.ShouldBeTrue();
        method.IsStatic.ShouldBeFalse();
        method.IsAbstract.ShouldBeTrue();
        method.GetMethodBody().ShouldBeNull();
        method.IsGenericMethod.ShouldBeFalse();
        method.ReturnType.ShouldBe(
            typeof(Task<>).MakeGenericType(typeof(Result<>).MakeGenericType(typeof(PagedResult<>).MakeGenericType(aggregate))));
        method.GetParameters().Select(p => (p.Name, p.ParameterType)).ShouldBe(
        [
            ("specification", typeof(ISpecification<>).MakeGenericType(aggregate)),
            ("sort", typeof(SortSpecification<,>).MakeGenericType(aggregate, id)),
            ("pagination", typeof(PaginationRequest)),
            ("cancellationToken", typeof(CancellationToken))
        ]);
    }

    [Fact]
    public void FindPagedAsync_ShouldHaveOptionalCancellationTokenWithDefault()
    {
        var token = ContractType.GetMethod("FindPagedAsync")!.GetParameters().Last();

        token.IsOptional.ShouldBeTrue();
        token.HasDefaultValue.ShouldBeTrue();

        var effective = token.DefaultValue is null ? default : (CancellationToken)token.DefaultValue;
        effective.ShouldBe(default(CancellationToken));
        effective.CanBeCanceled.ShouldBeFalse();
    }

    [Fact]
    public void FindPagedAsync_ShouldOnlyRequireNonOptionalParametersBeforeToken()
    {
        ContractType.GetMethod("FindPagedAsync")!.GetParameters()
            .Take(3).ShouldAllBe(p => !p.IsOptional);
    }

    // Preservação de IRepository 1.0 (contrato CLOSED) após a introdução do contrato 2.x
    [Fact]
    public void RepositoryContract1_ShouldRemainUnchanged()
    {
        var repository = typeof(IRepository<,>);

        repository.GetMethods().Select(m => m.Name).Order().ShouldBe(["AddAsync", "GetByIdAsync", "RemoveAsync", "UpdateAsync"]);
        repository.GetInterfaces().ShouldBeEmpty();
    }

    [Fact]
    public void Contract_ShouldNotDependOnForbiddenLayersOrFrameworks()
    {
        var result = Types.InAssembly(ContractType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenDependencies)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências proibidas: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Contract_ShouldOnlyDependOnSystemAndSharedNamespace()
    {
        var result = Types.InAssembly(ContractType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .Should()
            .OnlyHaveDependenciesOn("System", ComponentNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências fora de System: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    // O Domain entrega apenas o contrato; implementações pertencem à Infrastructure
    [Fact]
    public void Contract_ShouldHaveNoImplementationInDomainAssembly()
    {
        var implementations = ContractType.Assembly.GetTypes()
            .Where(t => t.IsClass && t.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == ContractType))
            .Select(t => t.FullName);

        implementations.ShouldBeEmpty();
    }
}

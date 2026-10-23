using System.Reflection;
using Business.Platform.Core.Domain.Shared;
using NetArchTest.Rules;

namespace Business.Platform.Core.ArchitectureTests.Domain.Shared.Pagination;

public sealed class PaginationArchitectureTests
{
    private const string DomainAssemblyName = "Business.Platform.Core.Domain";
    private const string ComponentNamespace = "Business.Platform.Core.Domain.Shared";
    private const string ComponentNamePattern = "^(PaginationRequest|PagedResult(`1)?)$";

    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Type RequestType = typeof(PaginationRequest);
    private static readonly Type ResultType = typeof(PagedResult<>);

    private static readonly Type[] ComponentTypes = [RequestType, ResultType];

    private static readonly string[] ForbiddenDependencies =
    [
        "Business.Platform.Core.Application",
        "Business.Platform.Core.Infrastructure",
        "Business.Platform.Core.Api",
        "Business.Platform.Core.Contracts",
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.Extensions",
        "Dapper",
        "NHibernate",
        "System.Data",
        "System.Net.Http",
        "System.Text.Json",
        "System.Linq.Queryable",
        "System.Linq.Enumerable",
        "System.Collections.ObjectModel.ObservableCollection"
    ];

    public static TheoryData<Type> Components => new(ComponentTypes);

    [Theory]
    [MemberData(nameof(Components))]
    public void Component_ShouldResideInDomainAssemblyAndSharedNamespace(Type type)
    {
        type.Assembly.GetName().Name.ShouldBe(DomainAssemblyName);
        type.Namespace.ShouldBe(ComponentNamespace);
        type.IsPublic.ShouldBeTrue();
        type.IsClass.ShouldBeTrue();
        type.IsSealed.ShouldBeTrue();
        type.IsAbstract.ShouldBeFalse();
    }

    [Fact]
    public void ComponentFilter_ShouldSelectExactlyTheTwoComponents()
    {
        var selected = Types.InAssembly(RequestType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .GetTypes();

        selected.OrderBy(type => type.Name).ShouldBe(ComponentTypes.OrderBy(type => type.Name));
    }

    [Theory]
    [MemberData(nameof(Components))]
    public void Component_ShouldHaveOnlyPrivateConstructor(Type type)
    {
        type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).ShouldBeEmpty();
        type.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).ShouldHaveSingleItem().IsPrivate.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(Components))]
    public void Component_ShouldNotExposePublicSettersOrMutableFields(Type type)
    {
        type.GetProperties(AllDeclared).Where(p => p.SetMethod is not null).ShouldBeEmpty();
        type.GetFields(AllDeclared).Where(f => !f.IsInitOnly && !f.IsLiteral).ShouldBeEmpty();
        type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .ShouldAllBe(f => f.IsLiteral || f.IsInitOnly);
    }

    [Fact]
    public void Request_ShouldExposeExactPublicSignatures()
    {
        var members = RequestType.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Order()
            .ToArray();

        members.ShouldBe(new[]
        {
            "Create", "Default", "DefaultPageSize", "Equals", "Equals", "GetHashCode", "MaxPageSize", "Offset", "PageNumber",
            "PageSize", "get_Default", "get_Offset", "get_PageNumber", "get_PageSize", "op_Equality", "op_Inequality"
        }.Order());

        RequestType.GetInterfaces().ShouldBe([typeof(IEquatable<PaginationRequest>)]);

        var create = RequestType.GetMethod("Create").ShouldNotBeNull();
        create.IsStatic.ShouldBeTrue();
        create.ReturnType.ShouldBe(typeof(Result<PaginationRequest>));
        var parameters = create.GetParameters();
        parameters.Select(p => (p.Name, p.ParameterType, p.IsOptional)).ShouldBe(
            [("pageNumber", typeof(int), false), ("pageSize", typeof(int), true)]);
        parameters[1].DefaultValue.ShouldBe(20);

        RequestType.GetField("DefaultPageSize")!.IsLiteral.ShouldBeTrue();
        RequestType.GetField("DefaultPageSize")!.GetRawConstantValue().ShouldBe(20);
        RequestType.GetField("MaxPageSize")!.GetRawConstantValue().ShouldBe(100);

        RequestType.GetProperty("Default")!.PropertyType.ShouldBe(RequestType);
        RequestType.GetProperty("Default")!.GetMethod!.IsStatic.ShouldBeTrue();
        RequestType.GetProperty("PageNumber")!.PropertyType.ShouldBe(typeof(int));
        RequestType.GetProperty("PageSize")!.PropertyType.ShouldBe(typeof(int));
        RequestType.GetProperty("Offset")!.PropertyType.ShouldBe(typeof(long));

        foreach (var name in new[] { "op_Equality", "op_Inequality" })
        {
            var op = RequestType.GetMethod(name).ShouldNotBeNull();
            op.ReturnType.ShouldBe(typeof(bool));
            op.GetParameters().Select(p => p.ParameterType).ShouldBe([RequestType, RequestType]);
        }
    }

    [Fact]
    public void PagedResult_ShouldExposeExactPublicSignatures()
    {
        ResultType.IsGenericTypeDefinition.ShouldBeTrue();
        ResultType.GetGenericArguments().ShouldHaveSingleItem().GetGenericParameterConstraints().ShouldBeEmpty();
        ResultType.GetInterfaces().ShouldBeEmpty();

        var argument = ResultType.GetGenericArguments()[0];

        ResultType.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Order()
            .ShouldBe(new[]
            {
                "Create", "HasNextPage", "HasPreviousPage", "Items", "PageNumber", "PageSize", "TotalCount", "TotalPages",
                "get_HasNextPage", "get_HasPreviousPage", "get_Items", "get_PageNumber", "get_PageSize", "get_TotalCount",
                "get_TotalPages"
            }.Order());

        ResultType.GetProperty("Items")!.PropertyType.ShouldBe(typeof(IReadOnlyList<>).MakeGenericType(argument));
        ResultType.GetProperty("PageNumber")!.PropertyType.ShouldBe(typeof(int));
        ResultType.GetProperty("PageSize")!.PropertyType.ShouldBe(typeof(int));
        ResultType.GetProperty("TotalCount")!.PropertyType.ShouldBe(typeof(long));
        ResultType.GetProperty("TotalPages")!.PropertyType.ShouldBe(typeof(long));
        ResultType.GetProperty("HasPreviousPage")!.PropertyType.ShouldBe(typeof(bool));
        ResultType.GetProperty("HasNextPage")!.PropertyType.ShouldBe(typeof(bool));

        var create = ResultType.GetMethod("Create").ShouldNotBeNull();
        create.IsStatic.ShouldBeTrue();
        create.ReturnType.ShouldBe(ResultType.MakeGenericType(argument));
        create.GetParameters().Select(p => (p.Name, p.ParameterType))
            .ShouldBe(
            [
                ("items", typeof(IReadOnlyCollection<>).MakeGenericType(argument)),
                ("request", RequestType),
                ("totalCount", typeof(long))
            ]);
    }

    [Fact]
    public void PagedResult_ShouldNotExposeArrayOrMutableCollectionTypes()
    {
        var exposed = ResultType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Select(p => p.PropertyType);

        exposed.ShouldNotContain(t => t.IsArray);
        exposed.ShouldNotContain(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>));
    }

    [Fact]
    public void Component_ShouldNotAddPublicInterfacesToDomain()
    {
        const BindingFlags members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var componentGraph = ComponentTypes
            .SelectMany(t => new[] { t }.Concat(t.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)))
            .ToArray();
        var domainInterfaces = RequestType.Assembly.GetTypes().Where(t => t.IsInterface).ToArray();

        // Evita aprovação por conjunto vazio: o Domain declara interfaces (de outras Foundations)
        domainInterfaces.ShouldNotBeEmpty();

        // Nenhuma interface declarada dentro dos componentes (independente de nome)
        componentGraph.Where(t => t.IsInterface).ShouldBeEmpty();

        // Somente o contrato Repository 2.x (ADR-0004) referencia os tipos de Pagination em seus membros
        domainInterfaces.Where(i => i.GetMethods(members)
                .SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType))
                .Any(ReferencesComponent))
            .Select(i => i.Name)
            .ShouldBe(["ISpecificationRepository`2"]);

        // Os componentes só implementam IEquatable<PaginationRequest> (Request) e nada (PagedResult)
        RequestType.GetInterfaces().ShouldBe([typeof(IEquatable<PaginationRequest>)]);
        ResultType.GetInterfaces().ShouldBeEmpty();

        // Conjunto de interfaces do Domain inalterado pela feature (baseline das demais Foundations)
        domainInterfaces.Select(i => i.Name).Order().ShouldBe(["IDomainEvent", "IRepository`2", "ISpecification`1", "ISpecificationRepository`2"]);

        static bool ReferencesComponent(Type type) =>
            type == RequestType
            || (type.IsGenericType && type.GetGenericTypeDefinition() == ResultType)
            || (type.HasElementType && ReferencesComponent(type.GetElementType()!))
            || (type.IsGenericType && type.GetGenericArguments().Any(ReferencesComponent));
    }

    [Fact]
    public void Component_ShouldNotDependOnForbiddenLayersOrFrameworks()
    {
        var result = Types.InAssembly(RequestType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenDependencies)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências proibidas: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Component_ShouldOnlyDependOnSystemAndSharedNamespace()
    {
        var result = Types.InAssembly(RequestType.Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .Should()
            .OnlyHaveDependenciesOn("System", ComponentNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências fora de System: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void DomainAssembly_ShouldNotReferenceObjectModelOrOtherUnauthorizedAssemblies()
    {
        var names = RequestType.Assembly.GetReferencedAssemblies().Select(r => r.Name).ToArray();

        names.ShouldNotContain("System.ObjectModel");
        names.ShouldNotContain("System.Linq");
        names.ShouldNotContain("System.Text.Json");
        names.Order().ShouldBe(["System.Collections", "System.Linq.Expressions", "System.Runtime"]);
        RequestType.Assembly.GetReferencedAssemblies()
            .ShouldAllBe(r => (r.GetPublicKeyToken() ?? Array.Empty<byte>()).SequenceEqual(Convert.FromHexString("b03f5f7f11d50a3a")));
    }

    [Fact]
    public void Repository_ShouldRemainUnchanged()
    {
        var repository = typeof(IRepository<,>);

        repository.GetMethods().Select(m => m.Name).Order().ShouldBe(["AddAsync", "GetByIdAsync", "RemoveAsync", "UpdateAsync"]);
        repository.GetMethods().SelectMany(m => m.GetParameters()).ShouldNotContain(p => p.ParameterType.Name.Contains("Pag"));
        repository.GetMethods().Select(m => m.ReturnType).ShouldNotContain(t => t.ToString()!.Contains("Pag"));
        repository.GetProperties().ShouldBeEmpty();
        repository.GetInterfaces().ShouldBeEmpty();
    }

    [Fact]
    public void Specification_ShouldRemainUnchanged()
    {
        var specification = typeof(ISpecification<>);

        specification.GetProperties().ShouldHaveSingleItem().Name.ShouldBe("Criteria");
        specification.GetMethods().Select(m => m.Name).ShouldBe(["get_Criteria"]);
        specification.GetInterfaces().ShouldBeEmpty();
    }
}

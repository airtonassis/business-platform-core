using System.Reflection;
using Business.Platform.Core.Domain.Shared;
using NetArchTest.Rules;

namespace Business.Platform.Core.ArchitectureTests.Domain.Shared.Errors;

public sealed class ErrorArchitectureTests
{
    private const string DomainAssemblyName = "Business.Platform.Core.Domain";
    private const string ErrorNamespace = "Business.Platform.Core.Domain.Shared";
    private const string ComponentNamePattern = "^(Error|ErrorType)$";

    private static readonly Type[] ComponentTypes = [typeof(Error), typeof(ErrorType)];

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

    public static TheoryData<Type> Components => new(ComponentTypes);

    // Localização arquitetural
    [Theory]
    [MemberData(nameof(Components))]
    public void Component_ShouldResideInDomainAssembly(Type type)
    {
        type.Assembly.GetName().Name.ShouldBe(DomainAssemblyName);
    }

    [Theory]
    [MemberData(nameof(Components))]
    public void Component_ShouldResideInSharedNamespace(Type type)
    {
        type.Namespace.ShouldBe(ErrorNamespace);
    }

    // Garante que as regras abaixo não passam de forma vazia
    [Fact]
    public void ComponentFilter_ShouldSelectErrorAndErrorType()
    {
        var selected = Types.InAssembly(typeof(Error).Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .GetTypes();

        selected.OrderBy(type => type.Name).ShouldBe(ComponentTypes.OrderBy(type => type.Name));
    }

    // Dependências proibidas (Application, Infrastructure, API, ASP.NET Core, EF Core, logging, ...)
    [Fact]
    public void Component_ShouldNotDependOnForbiddenLayersOrFrameworks()
    {
        var result = Types.InAssembly(typeof(Error).Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenDependencies)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailureMessage(result));
    }

    // Error não conhece Result (Error ← Result)
    [Fact]
    public void Component_ShouldNotDependOnResult()
    {
        var result = Types.InAssembly(typeof(Error).Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .ShouldNot()
            .HaveDependencyOnAny($"{ErrorNamespace}.Result")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailureMessage(result));
    }

    // Somente a BCL é permitida
    [Fact]
    public void DomainAssembly_ShouldReferenceOnlyBaseClassLibrary()
    {
        var nonBclReferences = typeof(Error).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name != "netstandard" && name != "mscorlib" && !name.StartsWith("System", StringComparison.Ordinal))
            .ToArray();

        nonBclReferences.ShouldBeEmpty();
    }

    // Error implementado como sealed record
    [Fact]
    public void Error_ShouldBeSealedRecord()
    {
        var type = typeof(Error);

        type.IsSealed.ShouldBeTrue();
        type.GetMethod("<Clone>$").ShouldNotBeNull();
    }

    // ERR-TST-019
    [Fact]
    public void Error_ShouldExposeReadOnlyProperties()
    {
        var mutableProperties = typeof(Error)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(property => property.SetMethod is not null)
            .Select(property => property.Name)
            .ToArray();

        mutableProperties.ShouldBeEmpty();
    }

    // O construtor usado por Error.None não pode ser exposto
    [Fact]
    public void Error_ShouldExposeOnlyValidatingConstructor()
    {
        var publicConstructors = typeof(Error).GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        var constructor = publicConstructors.ShouldHaveSingleItem();
        constructor.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ShouldBe([typeof(string), typeof(string), typeof(ErrorType)]);
    }

    private static string FailureMessage(TestResult result) =>
        "Tipos com dependências proibidas: " + string.Join(", ", result.FailingTypeNames ?? []);
}

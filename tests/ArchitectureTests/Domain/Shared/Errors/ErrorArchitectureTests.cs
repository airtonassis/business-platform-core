using System.Reflection;
using System.Xml.Linq;
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

    // Somente a BCL é permitida: lista explícita de assemblies (nome + chave pública da Microsoft)
    [Fact]
    public void DomainAssembly_ShouldReferenceOnlyAllowedAssemblies()
    {
        var notAllowed = typeof(Error).Assembly
            .GetReferencedAssemblies()
            .Where(reference => !IsAllowedAssembly(reference))
            .Select(reference => reference.FullName)
            .ToArray();

        notAllowed.ShouldBeEmpty();
    }

    // O projeto Domain não declara referências de pacote, projeto, assembly ou framework
    [Fact]
    public void DomainProject_ShouldNotDeclareExternalReferences()
    {
        var references = ReferenceElements(Path.Combine(RepositoryRoot, "src", DomainAssemblyName, $"{DomainAssemblyName}.csproj"))
            .ToArray();

        references.ShouldBeEmpty();
    }

    // Nenhuma referência é injetada globalmente em todos os projetos (incluindo Domain)
    [Theory]
    [InlineData("Directory.Build.props")]
    [InlineData("Directory.Packages.props")]
    public void SharedBuildFiles_ShouldNotInjectReferences(string fileName)
    {
        var references = ReferenceElements(Path.Combine(RepositoryRoot, fileName))
            .ToArray();

        references.ShouldBeEmpty();
    }

    // ERR-TST-035 — H01: Error é uma classe selada, não um record
    [Fact]
    public void Error_ShouldBeSealedClassAndNotRecord()
    {
        var type = typeof(Error);

        type.IsClass.ShouldBeTrue();
        type.IsSealed.ShouldBeTrue();
        type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).ShouldBeNull();
        type.GetProperty("EqualityContract", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).ShouldBeNull();
    }

    // ERR-TST-036 — H01: igualdade por valor explícita
    [Fact]
    public void Error_ShouldDeclareExplicitValueEquality()
    {
        var type = typeof(Error);

        typeof(IEquatable<Error>).IsAssignableFrom(type).ShouldBeTrue();
        type.GetMethod(nameof(Equals), [typeof(Error)])!.DeclaringType.ShouldBe(type);
        type.GetMethod(nameof(Equals), [typeof(object)])!.DeclaringType.ShouldBe(type);
        type.GetMethod(nameof(GetHashCode), Type.EmptyTypes)!.DeclaringType.ShouldBe(type);
        type.GetMethod("op_Equality", BindingFlags.Public | BindingFlags.Static).ShouldNotBeNull();
        type.GetMethod("op_Inequality", BindingFlags.Public | BindingFlags.Static).ShouldNotBeNull();
    }

    // ERR-TST-037 — H01: não existe mecanismo de clonagem equivalente a `with`
    [Fact]
    public void Error_ShouldNotExposeCloningMechanism()
    {
        var type = typeof(Error);

        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        constructors.Length.ShouldBe(2);
        constructors.ShouldNotContain(constructor => constructor.GetParameters().Any(parameter => parameter.ParameterType == type));

        var publicMethodsReturningError = type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName && method.ReturnType == type)
            .Select(method => method.Name)
            .ToArray();

        publicMethodsReturningError.ShouldBeEmpty();
        typeof(ICloneable).IsAssignableFrom(type).ShouldBeFalse();
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

    private static readonly byte[] MicrosoftPublicKeyToken = Convert.FromHexString("b03f5f7f11d50a3a");

    private static readonly string[] AllowedReferencedAssemblies = ["System.Runtime"];

    private static readonly string[] ReferenceItemNames =
        ["PackageReference", "GlobalPackageReference", "ProjectReference", "Reference", "FrameworkReference"];

    private static string RepositoryRoot { get; } = FindRepositoryRoot();

    private static bool IsAllowedAssembly(AssemblyName reference) =>
        AllowedReferencedAssemblies.Contains(reference.Name, StringComparer.Ordinal)
        && (reference.GetPublicKeyToken() ?? []).SequenceEqual(MicrosoftPublicKeyToken);

    private static IEnumerable<string> ReferenceElements(string projectFile) =>
        XDocument.Load(projectFile)
            .Descendants()
            .Where(element => ReferenceItemNames.Contains(element.Name.LocalName, StringComparer.Ordinal))
            .Select(element => element.ToString(SaveOptions.DisableFormatting));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Business.Platform.Core.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Raiz do repositório (Business.Platform.Core.sln) não encontrada.");
    }

    private static string FailureMessage(TestResult result) =>
        "Tipos com dependências proibidas: " + string.Join(", ", result.FailingTypeNames ?? []);
}

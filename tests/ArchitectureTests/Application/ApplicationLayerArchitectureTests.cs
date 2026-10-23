using System.Net.Http;
using System.Reflection;
using System.Xml.Linq;
using Business.Platform.Core.Domain.Shared;

namespace Business.Platform.Core.ArchitectureTests.Application;

/// <summary>
/// Verifica a camada Application sem depender de tipos de produção.
/// Metadados de projeto (.csproj) são o controle principal; metadados de assembly em runtime são complementares.
/// </summary>
public sealed class ApplicationLayerArchitectureTests
{
    private const string SolutionFileName = "Business.Platform.Core.sln";
    private const string ApplicationAssemblyName = "Business.Platform.Core.Application";
    private const string DomainAssemblyName = "Business.Platform.Core.Domain";
    private const string ApplicationProjectPath = "src/Business.Platform.Core.Application/Business.Platform.Core.Application.csproj";
    private const string DomainProjectPath = "src/Business.Platform.Core.Domain/Business.Platform.Core.Domain.csproj";

    private static readonly string FrameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

    // Denylist do escopo inicial da Foundation Application (Implementation Plan R2).
    // Não é proibição arquitetural permanente: qualquer ampliação ou revisão exige decisão própria.
    private static readonly string[] ForbiddenAssemblyPrefixes =
    [
        // HTTP
        "System.Net.",
        // ORM e dados
        "System.Data",
        "Microsoft.EntityFrameworkCore",
        // Serialização, mesmo critério aplicado ao Domain
        "System.Text.Json",
        // ASP.NET e Extensions
        "Microsoft.AspNetCore",
        "Microsoft.Extensions.",
        // SDKs de IA
        "OpenAI",
        "Anthropic",
        "Azure.AI",
        "Google.",
        "Mistral",
        "Cohere",
        "LangChain",
        "OllamaSharp",
    ];

    [Fact]
    public void Application_Assembly_ShouldLoadFromTestOutput()
    {
        var assembly = Assembly.Load(ApplicationAssemblyName);

        assembly.GetName().Name.ShouldBe(ApplicationAssemblyName);
    }

    [Fact]
    public void Application_ProjectReferences_ShouldBeExactlyDomain()
    {
        var application = LoadProject(ApplicationProjectPath);

        ProjectReferenceIncludes(application).ShouldHaveSingleItem();
        ProjectReferenceViolations(application, ApplicationDirectory()).ShouldBeEmpty();
    }

    [Fact]
    public void Application_ShouldHaveNoDirectPackageOrAssemblyReferences()
    {
        var application = LoadProject(ApplicationProjectPath);

        ItemNames(application, "PackageReference").ShouldBeEmpty();
        ItemNames(application, "Reference").ShouldBeEmpty();
        ItemNames(application, "FrameworkReference").ShouldBeEmpty();
    }

    [Fact]
    public void Domain_ProjectReferences_ShouldBeEmpty()
    {
        var domain = LoadProject(DomainProjectPath);

        ProjectReferenceIncludes(domain).ShouldBeEmpty();
    }

    [Fact]
    public void Application_RuntimeReferences_ShouldBeDomainOrFrameworkOnly()
    {
        var references = Assembly.Load(ApplicationAssemblyName).GetReferencedAssemblies();

        RuntimeViolations(references).ShouldBeEmpty();
    }

    [Fact]
    public void Application_RuntimeReferences_ShouldNotIncludeDenylistedFrameworkAssemblies()
    {
        var names = Assembly.Load(ApplicationAssemblyName)
            .GetReferencedAssemblies()
            .Select(n => n.Name ?? string.Empty);

        names.Where(IsDenylisted).ShouldBeEmpty();

        // Controle da denylist com assembly BCL real e proibido: System.Net.Http.
        IsDenylisted(typeof(HttpClient).Assembly.GetName().Name ?? string.Empty).ShouldBeTrue();
    }

    [Fact]
    public void Control_NegativeReferences_ShouldBeReportedByClassifier()
    {
        // Este assembly de teste referencia Shouldly, que é NuGet e não é Domain: deve ser reportado.
        var violations = RuntimeViolations(typeof(ApplicationLayerArchitectureTests).Assembly.GetReferencedAssemblies());

        violations.Any(v => v.StartsWith("Shouldly ", StringComparison.Ordinal)).ShouldBeTrue();
    }

    [Fact]
    public void Control_PositiveReferences_ShouldBeAcceptedByClassifier()
    {
        var domain = typeof(Result).Assembly;

        domain.GetName().Name.ShouldBe(DomainAssemblyName);
        RuntimeViolations(domain.GetReferencedAssemblies()).ShouldBeEmpty();
    }

    [Fact]
    public void Control_SyntheticProjectXml_ShouldDetectForbiddenDeclarations()
    {
        var synthetic = XDocument.Parse("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Business.Platform.Core.Infrastructure\Business.Platform.Core.Infrastructure.csproj" />
                <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
                <Reference Include="Legacy.Lib">
                  <HintPath>..\lib\Legacy.Lib.dll</HintPath>
                </Reference>
              </ItemGroup>
            </Project>
            """);

        DeclarationViolations(synthetic, ApplicationDirectory()).ShouldBe(new[]
        {
            @"ProjectReference:..\Business.Platform.Core.Infrastructure\Business.Platform.Core.Infrastructure.csproj",
            "PackageReference:Newtonsoft.Json",
            "Reference:Legacy.Lib",
        });
    }

    [Fact]
    public void Control_SyntheticFrameworkReference_ShouldBeDetected()
    {
        // AUD-01: FrameworkReference explícita não autorizada deve ser reportada mesmo sem tipos de produção.
        var synthetic = XDocument.Parse("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <FrameworkReference Include="Microsoft.AspNetCore.App" />
              </ItemGroup>
            </Project>
            """);

        DeclarationViolations(synthetic, ApplicationDirectory()).ShouldBe(new[]
        {
            "FrameworkReference:Microsoft.AspNetCore.App",
        });
    }

    [Fact]
    public void Control_SyntheticHomonymProjectReference_ShouldBeRejected()
    {
        // AUD-02: mesmo nome de arquivo em outro diretório não é o Domain canônico.
        var homonym = XDocument.Parse("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Impostor\Business.Platform.Core.Domain.csproj" />
              </ItemGroup>
            </Project>
            """);
        var canonical = XDocument.Parse("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Business.Platform.Core.Domain\Business.Platform.Core.Domain.csproj" />
              </ItemGroup>
            </Project>
            """);

        ProjectReferenceViolations(homonym, ApplicationDirectory()).ShouldBe(new[]
        {
            @"ProjectReference:..\Impostor\Business.Platform.Core.Domain.csproj",
        });
        ProjectReferenceViolations(canonical, ApplicationDirectory()).ShouldBeEmpty();
    }

    [Fact]
    public void Domain_RuntimeReferences_ShouldNotIncludeApplication()
    {
        typeof(Result).Assembly.GetReferencedAssemblies()
            .Select(n => n.Name ?? string.Empty)
            .ShouldNotContain(ApplicationAssemblyName);
    }

    private static bool IsDenylisted(string assemblyName) =>
        ForbiddenAssemblyPrefixes.Any(prefix => assemblyName.StartsWith(prefix, StringComparison.Ordinal));

    private static bool IsFrameworkAssembly(Assembly assembly) =>
        string.Equals(Path.GetDirectoryName(assembly.Location), FrameworkDirectory, StringComparison.OrdinalIgnoreCase);

    private static List<string> RuntimeViolations(IEnumerable<AssemblyName> references)
    {
        var violations = new List<string>();

        foreach (var reference in references)
        {
            var name = reference.Name ?? string.Empty;

            if (name == DomainAssemblyName)
            {
                continue;
            }

            if (IsDenylisted(name))
            {
                violations.Add($"{name} (denylist)");
                continue;
            }

            if (!IsFrameworkAssembly(Assembly.Load(reference)))
            {
                violations.Add($"{name} (não BCL e não Domain)");
            }
        }

        return violations;
    }

    private static List<string> DeclarationViolations(XDocument project, string projectDirectory)
    {
        var violations = new List<string>();

        violations.AddRange(ProjectReferenceViolations(project, projectDirectory));
        violations.AddRange(ItemNames(project, "PackageReference").Select(name => $"PackageReference:{name}"));
        violations.AddRange(ItemNames(project, "Reference").Select(name => $"Reference:{name}"));
        violations.AddRange(ItemNames(project, "FrameworkReference").Select(name => $"FrameworkReference:{name}"));

        return violations;
    }

    // AUD-02: a referência só é aceita se o caminho resolvido for o do Domain canônico, não o nome do arquivo.
    private static List<string> ProjectReferenceViolations(XDocument project, string projectDirectory)
    {
        var canonicalDomain = Path.GetFullPath(ProjectPath(DomainProjectPath));

        return ProjectReferenceIncludes(project)
            .Where(include => !PathsEqual(ResolveReference(projectDirectory, include), canonicalDomain))
            .Select(include => $"ProjectReference:{include}")
            .ToList();
    }

    private static IEnumerable<string> ProjectReferenceIncludes(XDocument project) =>
        project.Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty);

    private static string ResolveReference(string projectDirectory, string include) =>
        Path.GetFullPath(Path.Combine(
            projectDirectory,
            include.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)));

    private static bool PathsEqual(string left, string right) =>
        string.Equals(left, right, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string ApplicationDirectory() =>
        Path.GetDirectoryName(ProjectPath(ApplicationProjectPath))!;

    private static string[] ItemNames(XDocument project, string elementName) =>
        project.Descendants(elementName)
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .Select(include => include.Split('\\', '/')[^1])
            .Select(name => name.EndsWith(".csproj", StringComparison.Ordinal) ? name[..^".csproj".Length] : name)
            .ToArray();

    private static string ProjectPath(string relativePath) =>
        Path.Combine(SolutionRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static XDocument LoadProject(string relativePath) =>
        XDocument.Load(ProjectPath(relativePath));

    private static string SolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException($"Solução '{SolutionFileName}' não encontrada a partir de {AppContext.BaseDirectory}.");
    }
}

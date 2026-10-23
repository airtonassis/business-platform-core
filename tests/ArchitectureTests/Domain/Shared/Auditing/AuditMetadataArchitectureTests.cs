using System.Reflection;
using Business.Platform.Core.Domain.Shared;
using NetArchTest.Rules;

namespace Business.Platform.Core.ArchitectureTests.Domain.Shared.Auditing;

public sealed class AuditMetadataArchitectureTests
{
    private const string DomainAssemblyName = "Business.Platform.Core.Domain";
    private const string ComponentNamespace = "Business.Platform.Core.Domain.Shared";

    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Type MetadataType = typeof(AuditMetadata);

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
        "System.Security.Claims",
        "System.TimeProvider"
    ];

    [Fact]
    public void Metadata_ShouldResideInDomainAssemblyAndSharedNamespace()
    {
        MetadataType.Assembly.GetName().Name.ShouldBe(DomainAssemblyName);
        MetadataType.Namespace.ShouldBe(ComponentNamespace);
        MetadataType.IsPublic.ShouldBeTrue();
        MetadataType.IsClass.ShouldBeTrue();
        MetadataType.IsSealed.ShouldBeTrue();
        MetadataType.IsAbstract.ShouldBeFalse();
    }

    [Fact]
    public void Metadata_ShouldHaveOnlyPrivateConstructor()
    {
        MetadataType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).ShouldBeEmpty();
        MetadataType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).ShouldHaveSingleItem().IsPrivate.ShouldBeTrue();
    }

    [Fact]
    public void Metadata_ShouldNotExposePublicSettersOrMutableFields()
    {
        MetadataType.GetProperties(AllDeclared).Where(p => p.SetMethod is not null).ShouldBeEmpty();
        MetadataType.GetFields(AllDeclared).Where(f => !f.IsInitOnly && !f.IsLiteral).ShouldBeEmpty();
    }

    [Fact]
    public void Metadata_ShouldNotInheritValueObjectOrImplementIAuditable()
    {
        MetadataType.BaseType.ShouldBe(typeof(object));
        MetadataType.IsSubclassOf(typeof(ValueObject)).ShouldBeFalse();
        MetadataType.GetInterfaces().ShouldBe([typeof(IEquatable<AuditMetadata>)]);
        MetadataType.Assembly.GetTypes().Where(t => t.Name == "IAuditable").ShouldBeEmpty();
    }

    [Fact]
    public void Metadata_ShouldExposeExactPublicSignatures()
    {
        MetadataType.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Order()
            .ShouldBe(new[]
            {
                "Create", "CreatedAt", "CreatedBy", "Equals", "Equals", "GetHashCode", "UpdatedAt", "UpdatedBy", "WithUpdate",
                "get_CreatedAt", "get_CreatedBy", "get_UpdatedAt", "get_UpdatedBy", "op_Equality", "op_Inequality"
            }.Order());

        MetadataType.GetProperty("CreatedBy")!.PropertyType.ShouldBe(typeof(AuditActor));
        MetadataType.GetProperty("CreatedAt")!.PropertyType.ShouldBe(typeof(DateTimeOffset));
        MetadataType.GetProperty("UpdatedBy")!.PropertyType.ShouldBe(typeof(AuditActor));
        MetadataType.GetProperty("UpdatedAt")!.PropertyType.ShouldBe(typeof(DateTimeOffset?));

        var create = MetadataType.GetMethod("Create").ShouldNotBeNull();
        create.IsStatic.ShouldBeTrue();
        create.ReturnType.ShouldBe(typeof(Result<AuditMetadata>));
        create.GetParameters().Select(p => (p.Name, p.ParameterType))
            .ShouldBe([("createdBy", typeof(AuditActor)), ("createdAt", typeof(DateTimeOffset))]);

        var withUpdate = MetadataType.GetMethod("WithUpdate").ShouldNotBeNull();
        withUpdate.IsStatic.ShouldBeFalse();
        withUpdate.ReturnType.ShouldBe(typeof(Result<AuditMetadata>));
        withUpdate.GetParameters().Select(p => (p.Name, p.ParameterType))
            .ShouldBe([("updatedBy", typeof(AuditActor)), ("updatedAt", typeof(DateTimeOffset))]);
    }

    [Fact]
    public void Metadata_ShouldNotDependOnForbiddenLayersOrFrameworks()
    {
        var result = Types.InAssembly(MetadataType.Assembly)
            .That()
            .HaveNameMatching("^AuditMetadata$")
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenDependencies)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências proibidas: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Metadata_ShouldOnlyDependOnSystemAndSharedNamespace()
    {
        var result = Types.InAssembly(MetadataType.Assembly)
            .That()
            .HaveNameMatching("^AuditMetadata$")
            .Should()
            .OnlyHaveDependenciesOn("System", ComponentNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências fora de System: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Metadata_ShouldNotAddPublicInterfacesToDomain()
    {
        var domainInterfaces = MetadataType.Assembly.GetTypes().Where(t => t.IsInterface).ToArray();

        domainInterfaces.ShouldNotBeEmpty();
        domainInterfaces.Select(i => i.Name).Order().ShouldBe(["IDomainEvent", "IRepository`2", "ISpecification`1", "ISpecificationRepository`2"]);
        MetadataType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Where(t => t.IsInterface).ShouldBeEmpty();
    }

    [Fact]
    public void DomainAssembly_ShouldPreserveAllowlist()
    {
        MetadataType.Assembly.GetReferencedAssemblies().Select(r => r.Name).Order()
            .ShouldBe(["System.Collections", "System.Linq.Expressions", "System.Runtime"]);
    }

    [Fact]
    public void PreviousFoundationContracts_ShouldRemainUnchanged()
    {
        typeof(IRepository<,>).GetMethods().Select(m => m.Name).Order()
            .ShouldBe(["AddAsync", "GetByIdAsync", "RemoveAsync", "UpdateAsync"]);
        typeof(ISpecification<>).GetProperties().ShouldHaveSingleItem().Name.ShouldBe("Criteria");
        typeof(PaginationRequest).GetMethod("Create").ShouldNotBeNull();
        typeof(IDomainEvent).IsInterface.ShouldBeTrue();
        typeof(Entity<>).IsAbstract.ShouldBeTrue();
        typeof(AggregateRoot<>).IsAbstract.ShouldBeTrue();
    }
}

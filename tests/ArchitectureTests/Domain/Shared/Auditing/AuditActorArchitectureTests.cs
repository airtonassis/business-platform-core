using System.Reflection;
using Business.Platform.Core.Domain.Shared;
using NetArchTest.Rules;

namespace Business.Platform.Core.ArchitectureTests.Domain.Shared.Auditing;

public sealed class AuditActorArchitectureTests
{
    private const string DomainAssemblyName = "Business.Platform.Core.Domain";
    private const string ComponentNamespace = "Business.Platform.Core.Domain.Shared";

    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Type ActorType = typeof(AuditActor);

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
        "System.Threading.Thread"
    ];

    [Fact]
    public void Actor_ShouldResideInDomainAssemblyAndSharedNamespace()
    {
        ActorType.Assembly.GetName().Name.ShouldBe(DomainAssemblyName);
        ActorType.Namespace.ShouldBe(ComponentNamespace);
        ActorType.IsPublic.ShouldBeTrue();
        ActorType.IsClass.ShouldBeTrue();
        ActorType.IsSealed.ShouldBeTrue();
        ActorType.IsAbstract.ShouldBeFalse();

        typeof(AuditActorKind).Namespace.ShouldBe(ComponentNamespace);
        typeof(AuditActorKind).IsEnum.ShouldBeTrue();
        typeof(AuditActorKind).IsPublic.ShouldBeTrue();
    }

    [Fact]
    public void Actor_ShouldHaveOnlyPrivateConstructor()
    {
        ActorType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).ShouldBeEmpty();
        ActorType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).ShouldHaveSingleItem().IsPrivate.ShouldBeTrue();
    }

    [Fact]
    public void Actor_ShouldNotExposePublicSettersOrMutableFields()
    {
        ActorType.GetProperties(AllDeclared).Where(p => p.SetMethod is not null).ShouldBeEmpty();
        ActorType.GetFields(AllDeclared).Where(f => !f.IsInitOnly && !f.IsLiteral).ShouldBeEmpty();
    }

    [Fact]
    public void Actor_ShouldNotInheritValueObject()
    {
        ActorType.BaseType.ShouldBe(typeof(object));
        ActorType.IsSubclassOf(typeof(ValueObject)).ShouldBeFalse();
    }

    [Fact]
    public void Actor_ShouldExposeExactPublicSignatures()
    {
        ActorType.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Order()
            .ShouldBe(new[]
            {
                "Create", "Equals", "Equals", "GetHashCode", "Id", "Kind", "MaxIdLength", "get_Id", "get_Kind",
                "op_Equality", "op_Inequality"
            }.Order());

        ActorType.GetInterfaces().ShouldBe([typeof(IEquatable<AuditActor>)]);

        ActorType.GetField("MaxIdLength")!.IsLiteral.ShouldBeTrue();
        ActorType.GetField("MaxIdLength")!.GetRawConstantValue().ShouldBe(256);
        ActorType.GetProperty("Kind")!.PropertyType.ShouldBe(typeof(AuditActorKind));
        ActorType.GetProperty("Id")!.PropertyType.ShouldBe(typeof(string));

        var create = ActorType.GetMethod("Create").ShouldNotBeNull();
        create.IsStatic.ShouldBeTrue();
        create.ReturnType.ShouldBe(typeof(Result<AuditActor>));
        create.GetParameters().Select(p => (p.Name, p.ParameterType))
            .ShouldBe([("kind", typeof(AuditActorKind)), ("id", typeof(string))]);

        Enum.GetNames<AuditActorKind>().Order().ShouldBe(["System", "User"]);
    }

    [Fact]
    public void Actor_ShouldNotDependOnForbiddenLayersOrFrameworks()
    {
        var result = Types.InAssembly(ActorType.Assembly)
            .That()
            .HaveNameMatching("^(AuditActor|AuditActorKind)$")
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenDependencies)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências proibidas: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Actor_ShouldOnlyDependOnSystemAndSharedNamespace()
    {
        var result = Types.InAssembly(ActorType.Assembly)
            .That()
            .HaveNameMatching("^AuditActor$")
            .Should()
            .OnlyHaveDependenciesOn("System", ComponentNamespace)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências fora de System: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Actor_ShouldNotAddPublicInterfacesToDomain()
    {
        var domainInterfaces = ActorType.Assembly.GetTypes().Where(t => t.IsInterface).ToArray();

        domainInterfaces.ShouldNotBeEmpty();
        domainInterfaces.Select(i => i.Name).Order().ShouldBe(["IDomainEvent", "IRepository`2", "ISpecification`1", "ISpecificationRepository`2"]);
        ActorType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).Where(t => t.IsInterface).ShouldBeEmpty();
    }

    [Fact]
    public void DomainAssembly_ShouldPreserveAllowlist()
    {
        ActorType.Assembly.GetReferencedAssemblies().Select(r => r.Name).Order()
            .ShouldBe(["System.Collections", "System.Linq.Expressions", "System.Runtime"]);
    }

    [Fact]
    public void PreviousFoundationContracts_ShouldRemainUnchanged()
    {
        typeof(IRepository<,>).GetMethods().Select(m => m.Name).Order()
            .ShouldBe(["AddAsync", "GetByIdAsync", "RemoveAsync", "UpdateAsync"]);
        typeof(ISpecification<>).GetProperties().ShouldHaveSingleItem().Name.ShouldBe("Criteria");
        typeof(PaginationRequest).GetMethod("Create").ShouldNotBeNull();
        typeof(Result).IsClass.ShouldBeTrue();
        typeof(Error).IsSealed.ShouldBeTrue();
        typeof(ValueObject).IsAbstract.ShouldBeTrue();
    }
}

using System.Reflection;
using Business.Platform.Core.Domain.Shared;
using NetArchTest.Rules;

namespace Business.Platform.Core.ArchitectureTests.Domain.Shared.DomainEvents;

public sealed class DomainEventArchitectureTests
{
    private const string DomainAssemblyName = "Business.Platform.Core.Domain";
    private const string ComponentNamespace = "Business.Platform.Core.Domain.Shared";
    private const string ComponentName = "IDomainEvent";

    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly Type EventType = typeof(IDomainEvent);

    private static readonly string[] ForbiddenDependencies =
    [
        "Business.Platform.Core.Application",
        "Business.Platform.Core.Infrastructure",
        "Business.Platform.Core.Api",
        "Business.Platform.Core.Contracts",
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore",
        "Microsoft.Extensions",
        "MediatR",
        "System.Net.Http",
        "System.Text.Json",
        "System.Data",
        "System.Transactions"
    ];

    // Atributos emitidos pelo compilador (namespace System.Runtime.CompilerServices, ex.: Nullable) não são explícitos
    private static string[] ExplicitAttributes(IEnumerable<CustomAttributeData> attributes) =>
        attributes
            .Where(data => data.AttributeType.Namespace != "System.Runtime.CompilerServices")
            .Select(data => data.AttributeType.FullName ?? data.AttributeType.Name)
            .ToArray();

    [Fact]
    public void DomainEvent_ShouldResideInDomainAssembly()
    {
        EventType.Assembly.GetName().Name.ShouldBe(DomainAssemblyName);
    }

    [Fact]
    public void DomainEvent_ShouldResideInSharedNamespace()
    {
        EventType.Namespace.ShouldBe(ComponentNamespace);
    }

    // Garante que as regras de dependência não passam de forma vazia
    [Fact]
    public void ComponentFilter_ShouldSelectOnlyIDomainEvent()
    {
        var selected = Types.InAssembly(EventType.Assembly)
            .That()
            .HaveName(ComponentName)
            .GetTypes();

        selected.ShouldHaveSingleItem().ShouldBe(EventType);
    }

    [Fact]
    public void DomainEvent_ShouldBePublicInterface()
    {
        EventType.IsInterface.ShouldBeTrue();
        EventType.IsPublic.ShouldBeTrue();
        EventType.IsGenericType.ShouldBeFalse();
    }

    [Fact]
    public void DomainEvent_ShouldDeclareExactlyEventIdAndOccurredOnUtc()
    {
        var properties = EventType.GetProperties(AllDeclared);

        properties.Select(p => p.Name).ShouldBe([nameof(IDomainEvent.EventId), nameof(IDomainEvent.OccurredOnUtc)], ignoreOrder: true);
    }

    [Fact]
    public void DomainEvent_ShouldUseGuidAndDateTimeOffset()
    {
        EventType.GetProperty(nameof(IDomainEvent.EventId))!.PropertyType.ShouldBe(typeof(Guid));
        EventType.GetProperty(nameof(IDomainEvent.OccurredOnUtc))!.PropertyType.ShouldBe(typeof(DateTimeOffset));
    }

    [Fact]
    public void DomainEvent_ShouldExposeOnlyGetters()
    {
        foreach (var property in EventType.GetProperties(AllDeclared))
        {
            property.CanRead.ShouldBeTrue(property.Name);
            property.CanWrite.ShouldBeFalse(property.Name);
            property.GetSetMethod(nonPublic: true).ShouldBeNull(property.Name);
        }
    }

    // Os únicos métodos declarados são os getters das propriedades; sem eventos, campos ou tipos aninhados
    [Fact]
    public void DomainEvent_ShouldNotDeclareAdditionalMembers()
    {
        var getters = EventType.GetProperties(AllDeclared).Select(p => p.GetMethod!.Name);

        EventType.GetMethods(AllDeclared).Select(m => m.Name).ShouldBe(getters, ignoreOrder: true);
        EventType.GetEvents(AllDeclared).ShouldBeEmpty();
        EventType.GetFields(AllDeclared).ShouldBeEmpty();
        EventType.GetNestedTypes(AllDeclared).ShouldBeEmpty();
        EventType.GetMembers(AllDeclared).Length.ShouldBe(4);
    }

    [Fact]
    public void DomainEvent_ShouldNotInheritFromOtherInterfaces()
    {
        EventType.GetInterfaces().ShouldBeEmpty();
    }

    [Fact]
    public void DomainEvent_ShouldNotHaveExplicitCustomAttributes()
    {
        ExplicitAttributes(EventType.GetCustomAttributesData()).ShouldBeEmpty();
    }

    [Fact]
    public void EventId_ShouldNotHaveExplicitCustomAttributes()
    {
        ExplicitAttributes(EventType.GetProperty(nameof(IDomainEvent.EventId))!.GetCustomAttributesData()).ShouldBeEmpty();
    }

    [Fact]
    public void OccurredOnUtc_ShouldNotHaveExplicitCustomAttributes()
    {
        ExplicitAttributes(EventType.GetProperty(nameof(IDomainEvent.OccurredOnUtc))!.GetCustomAttributesData()).ShouldBeEmpty();
    }

    [Fact]
    public void DomainEvent_ShouldNotDependOnForbiddenLayersOrFrameworks()
    {
        var result = Types.InAssembly(EventType.Assembly)
            .That()
            .HaveName(ComponentName)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenDependencies)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências proibidas: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void DomainEvent_ShouldOnlyDependOnSystemNamespace()
    {
        var result = Types.InAssembly(EventType.Assembly)
            .That()
            .HaveName(ComponentName)
            .Should()
            .OnlyHaveDependenciesOn("System")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue("Tipos com dependências fora de System: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Theory]
    [InlineData("Business.Platform.Core.Domain.Shared.Entity`1")]
    [InlineData("Business.Platform.Core.Domain.Shared.AggregateRoot`1")]
    [InlineData("Business.Platform.Core.Domain.Shared.Error")]
    [InlineData("Business.Platform.Core.Domain.Shared.Result")]
    public void DomainEvent_ShouldNotDependOnOtherFoundationComponents(string dependency)
    {
        EventType.GetProperties(AllDeclared)
            .Select(p => p.PropertyType.FullName)
            .ShouldNotContain(dependency);

        var result = Types.InAssembly(EventType.Assembly)
            .That()
            .HaveName(ComponentName)
            .ShouldNot()
            .HaveDependencyOn(dependency.Replace("`1", string.Empty, StringComparison.Ordinal))
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }

    // AggregateRoot permanece sem relação com eventos (a estrutura completa é coberta por AggregateRootArchitectureTests)
    [Fact]
    public void AggregateRoot_ShouldNotReferenceOrImplementDomainEvents()
    {
        typeof(AggregateRoot<Guid>).GetInterfaces().ShouldNotContain(EventType);

        var result = Types.InAssembly(EventType.Assembly)
            .That()
            .HaveNameMatching("^AggregateRoot(`1)?$")
            .ShouldNot()
            .HaveDependencyOn(EventType.FullName!)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue();
    }
}

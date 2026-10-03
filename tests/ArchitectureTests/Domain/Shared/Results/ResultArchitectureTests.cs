using System.Reflection;
using Business.Platform.Core.Domain.Shared;
using NetArchTest.Rules;

namespace Business.Platform.Core.ArchitectureTests.Domain.Shared.Results;

public sealed class ResultArchitectureTests
{
    private const string DomainAssemblyName = "Business.Platform.Core.Domain";
    private const string ResultNamespace = "Business.Platform.Core.Domain.Shared";
    private const string ComponentNamePattern = "^Result(`1)?$";

    private static readonly Type[] ComponentTypes = [typeof(Result), typeof(Result<>)];

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
        type.Namespace.ShouldBe(ResultNamespace);
    }

    // Garante que as regras abaixo não passam de forma vazia
    [Fact]
    public void ComponentFilter_ShouldSelectResultAndResultGeneric()
    {
        var selected = Types.InAssembly(typeof(Result).Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .GetTypes()
            .ToList();

        selected.Count.ShouldBe(2);
        selected.Any(t => t.Name == "Result").ShouldBeTrue();
        selected.Any(t => t.Name == "Result`1").ShouldBeTrue();
    }

    // Dependências proibidas
    [Fact]
    public void Component_ShouldNotDependOnForbiddenLayersOrFrameworks()
    {
        var result = Types.InAssembly(typeof(Result).Assembly)
            .That()
            .HaveNameMatching(ComponentNamePattern)
            .ShouldNot()
            .HaveDependencyOnAny(ForbiddenDependencies)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailureMessage(result));
    }

    // Result deve depender de Error
    [Fact]
    public void Result_Should_Depend_On_Error()
    {
        var result = Types.InAssembly(typeof(Result).Assembly)
            .That()
            .HaveNameMatching("^Result$")
            .Should()
            .HaveDependencyOn("Business.Platform.Core.Domain.Shared.Error")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(FailureMessage(result));
    }

    // Result<T> deve ser sealed
    [Fact]
    public void ResultGeneric_Should_Be_Sealed()
    {
        var resultGenericType = typeof(Result<>);
        resultGenericType.IsSealed.ShouldBeTrue();
    }

    // Result<T> deve herdar de Result (protege a superfície herdada: IsSuccess, IsFailure, Error, Match sem valor)
    [Fact]
    public void ResultGeneric_Should_Inherit_From_Result()
    {
        typeof(Result<>).BaseType.ShouldBe(typeof(Result));
    }

    // Result deve ter propriedades imutáveis
    [Fact]
    public void Result_Properties_Should_Be_Immutable()
    {
        var isSuccessProp = typeof(Result).GetProperty(nameof(Result.IsSuccess));
        var isFailureProp = typeof(Result).GetProperty(nameof(Result.IsFailure));
        var errorProp = typeof(Result).GetProperty(nameof(Result.Error));

        isSuccessProp!.CanWrite.ShouldBeFalse();
        isFailureProp!.CanWrite.ShouldBeFalse();
        errorProp!.CanWrite.ShouldBeFalse();
    }

    // Result<T> Value deve ser imutável
    [Fact]
    public void ResultGeneric_Value_Property_Should_Be_Immutable()
    {
        var concreteType = typeof(Result<int>);
        var valueProp = concreteType.GetProperty(nameof(Result<int>.Value));

        valueProp!.CanWrite.ShouldBeFalse();
    }

    // Métodos públicos de factory
    [Fact]
    public void Result_Should_Have_Success_Factory()
    {
        var successMethod = typeof(Result).GetMethod(
            nameof(Result.Success),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            [],
            null
        );

        successMethod.ShouldNotBeNull();
        successMethod!.ReturnType.ShouldBe(typeof(Result));
    }

    [Fact]
    public void Result_Should_Have_Failure_Factory()
    {
        var failureMethods = typeof(Result).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        ).Where(m => m.Name == nameof(Result.Failure) && !m.IsGenericMethod).ToList();

        failureMethods.Count.ShouldBe(1);
        failureMethods.First().ReturnType.ShouldBe(typeof(Result));
    }

    [Fact]
    public void Result_Should_Have_Generic_Success_Factory()
    {
        var successMethod = typeof(Result).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        ).FirstOrDefault(m =>
            m.Name == nameof(Result.Success) &&
            m.IsGenericMethod &&
            m.GetGenericArguments().Length == 1
        );

        successMethod.ShouldNotBeNull();
    }

    [Fact]
    public void Result_Should_Have_Generic_Failure_Factory()
    {
        var failureMethod = typeof(Result).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        ).FirstOrDefault(m =>
            m.Name == nameof(Result.Failure) &&
            m.IsGenericMethod &&
            m.GetGenericArguments().Length == 1
        );

        failureMethod.ShouldNotBeNull();
    }

    // Operadores de conversão implícita
    [Fact]
    public void ResultGeneric_Should_Have_Implicit_Conversion_From_Value()
    {
        var concreteType = typeof(Result<int>);
        var implicitOp = concreteType.GetMethod(
            "op_Implicit",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            [typeof(int)],
            null
        );

        implicitOp.ShouldNotBeNull();
    }

    [Fact]
    public void ResultGeneric_Should_Have_Implicit_Conversion_From_Error()
    {
        var concreteType = typeof(Result<int>);
        var implicitOp = concreteType.GetMethod(
            "op_Implicit",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            [typeof(Error)],
            null
        );

        implicitOp.ShouldNotBeNull();
    }

    // Match deve existir
    [Fact]
    public void Result_Should_Have_Match_Method()
    {
        var baseType = typeof(Result);
        var matchMethods = baseType.GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
        ).Where(m => m.Name == "Match").ToList();

        matchMethods.Count.ShouldBeGreaterThanOrEqualTo(1);
        matchMethods.Any(m => m.IsGenericMethod && m.GetGenericArguments().Length == 1).ShouldBeTrue();
    }

    [Fact]
    public void ResultGeneric_Should_Have_Exactly_Two_Match_Overloads()
    {
        var concreteType = typeof(Result<int>);
        var matchMethods = concreteType.GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
        ).Where(m => m.Name == "Match" && m.DeclaringType != typeof(object)).ToList();

        // Exatamente 2 sobrecargas: uma herdada de Result (Func<TResult>), outra em Result<T> (Func<TValue, TResult>)
        matchMethods.Count.ShouldBe(2);

        // Ambas devem ser genéricas (TResult)
        matchMethods.All(m => m.IsGenericMethod && m.GetGenericArguments().Length == 1).ShouldBeTrue();

        // Ambas devem ter 2 parâmetros (onSuccess e onFailure)
        matchMethods.All(m => m.GetParameters().Length == 2).ShouldBeTrue();
    }

    // API pública fechada — validação da superfície completa DECLARADA em Result.
    // A superfície herdada por Result<TValue> é protegida por ResultGeneric_Should_Inherit_From_Result.
    [Fact]
    public void Result_Public_API_Should_Match_Approved_Signature()
    {
        const string resultType = "Business.Platform.Core.Domain.Shared.Result";
        const string errorType = "Business.Platform.Core.Domain.Shared.Error";
        const string resultOfTValue = "Business.Platform.Core.Domain.Shared.Result<TValue>";

        var baseType = typeof(Result);

        var actualSignatures = GetPublicSurface(baseType).OrderBy(s => s, StringComparer.Ordinal).ToList();

        var expectedSignatures = new[]
        {
            $"METHOD|static|Success|return:{resultType}|params:",
            $"METHOD|static|Failure|return:{resultType}|params:{errorType}",
            $"METHOD|static|Success|generic:1|return:{resultOfTValue}|params:TValue",
            $"METHOD|static|Failure|generic:1|return:{resultOfTValue}|params:{errorType}",
            $"METHOD|instance|Match|generic:1|return:TResult|params:System.Func<TResult>,System.Func<{errorType},TResult>",
            $"PROPERTY|instance|IsSuccess|System.Boolean|index:|get:true|set:false",
            $"PROPERTY|instance|IsFailure|System.Boolean|index:|get:true|set:false",
            $"PROPERTY|instance|Error|{errorType}|index:|get:true|set:false"
        }.OrderBy(s => s, StringComparer.Ordinal).ToList();

        actualSignatures.ShouldBe(expectedSignatures,
            $"Superfície declarada de Result não corresponde. Atual: {string.Join(" || ", actualSignatures)}");
    }

    // API pública fechada — validação da superfície completa DECLARADA em Result<TValue>.
    // Inspeciona o tipo genérico aberto diretamente — sem instanciar com tipo concreto nem substituição textual.
    [Fact]
    public void ResultGeneric_Public_API_Should_Match_Approved_Signature()
    {
        const string errorType = "Business.Platform.Core.Domain.Shared.Error";
        const string resultOfTValue = "Business.Platform.Core.Domain.Shared.Result<TValue>";

        var openGenericType = typeof(Result<>);

        var actualSignatures = GetPublicSurface(openGenericType).OrderBy(s => s, StringComparer.Ordinal).ToList();

        var expectedSignatures = new[]
        {
            $"METHOD|instance|Match|generic:1|return:TResult|params:System.Func<TValue,TResult>,System.Func<{errorType},TResult>",
            $"METHOD|static|op_Implicit|return:{resultOfTValue}|params:TValue",
            $"METHOD|static|op_Implicit|return:{resultOfTValue}|params:{errorType}",
            $"PROPERTY|instance|Value|TValue|index:|get:true|set:false"
        }.OrderBy(s => s, StringComparer.Ordinal).ToList();

        actualSignatures.ShouldBe(expectedSignatures,
            $"Superfície declarada de Result<TValue> não corresponde. Atual: {string.Join(" || ", actualSignatures)}");
    }

    private static List<string> GetPublicSurface(Type type)
    {
        var signatures = new List<string>();
        var flags = BindingFlags.Public |
                   BindingFlags.Static |
                   BindingFlags.Instance |
                   BindingFlags.DeclaredOnly;

        // Métodos (accessors de propriedades/eventos excluídos; operadores/conversões preservados)
        foreach (var method in type.GetMethods(flags).Where(m => !m.IsSpecialName || m.Name.StartsWith("op_", StringComparison.Ordinal)))
        {
            var isStatic = method.IsStatic ? "static" : "instance";
            var genericArity = method.GetGenericArguments().Length;
            var genericPart = genericArity > 0 ? $"generic:{genericArity}|" : "";
            var returnType = GetCanonicalTypeName(method.ReturnType);
            var paramParts = string.Join(",", method.GetParameters().Select(p => GetCanonicalTypeName(p.ParameterType)));

            signatures.Add($"METHOD|{isStatic}|{method.Name}|{genericPart}return:{returnType}|params:{paramParts}");
        }

        // Propriedades (incluindo indexadores e visibilidade real de getter/setter)
        foreach (var prop in type.GetProperties(flags))
        {
            var accessor = prop.GetMethod ?? prop.SetMethod;
            var isStatic = accessor is not null && accessor.IsStatic ? "static" : "instance";
            var propType = GetCanonicalTypeName(prop.PropertyType);
            var indexParams = string.Join(",", prop.GetIndexParameters().Select(p => GetCanonicalTypeName(p.ParameterType)));
            var hasPublicGetter = prop.GetGetMethod(nonPublic: false) is not null;
            var hasPublicSetter = prop.GetSetMethod(nonPublic: false) is not null;

            signatures.Add($"PROPERTY|{isStatic}|{prop.Name}|{propType}|index:{indexParams}|get:{(hasPublicGetter ? "true" : "false")}|set:{(hasPublicSetter ? "true" : "false")}");
        }

        // Campos públicos (nenhum aprovado atualmente — qualquer adição deve falhar o teste)
        foreach (var field in type.GetFields(flags))
        {
            var isStatic = field.IsStatic ? "static" : "instance";
            var fieldType = GetCanonicalTypeName(field.FieldType);
            signatures.Add($"FIELD|{isStatic}|{field.Name}|{fieldType}");
        }

        // Eventos públicos (nenhum aprovado atualmente — qualquer adição deve falhar o teste)
        foreach (var evt in type.GetEvents(flags))
        {
            var addMethod = evt.GetAddMethod(nonPublic: true);
            var isStatic = addMethod is not null && addMethod.IsStatic ? "static" : "instance";
            var eventType = GetCanonicalTypeName(evt.EventHandlerType!);
            signatures.Add($"EVENT|{isStatic}|{evt.Name}|{eventType}");
        }

        return signatures;
    }

    // Identidade canônica completa de tipo: FullName para tipos concretos, recursiva para
    // genéricos/arrays/byref, e nome literal apenas para parâmetros genéricos (de tipo ou de método).
    private static string GetCanonicalTypeName(Type type)
    {
        if (type.IsGenericParameter)
            return type.Name;

        if (type.IsByRef)
            return $"ref {GetCanonicalTypeName(type.GetElementType()!)}";

        if (type.IsArray)
        {
            var rank = type.GetArrayRank();
            var brackets = rank == 1 ? "[]" : $"[{new string(',', rank - 1)}]";
            return $"{GetCanonicalTypeName(type.GetElementType()!)}{brackets}";
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var args = type.GetGenericArguments();
            var baseName = definition.Name.Split('`')[0];
            var fullBaseName = string.IsNullOrEmpty(definition.Namespace) ? baseName : $"{definition.Namespace}.{baseName}";
            var argNames = string.Join(",", args.Select(GetCanonicalTypeName));

            return $"{fullBaseName}<{argNames}>";
        }

        return type.FullName ?? type.Name;
    }

    private static string FailureMessage(TestResult result)
    {
        var failingTypes = result.FailingTypes;
        if (failingTypes is null || failingTypes.Count == 0)
            return "Falha arquitetural (detalhes não disponíveis)";

        return string.Join(Environment.NewLine, failingTypes.Select(t => $"Falha em: {t.FullName}"));
    }
}

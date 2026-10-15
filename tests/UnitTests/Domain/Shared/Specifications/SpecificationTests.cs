using Specs = Business.Platform.Core.Domain.Shared.Specifications;
using System.Linq.Expressions;
using Business.Platform.Core.Domain.Shared;

namespace Business.Platform.Core.UnitTests.Domain.Shared.Specifications;

public sealed class SpecificationTests
{
    private static readonly Probe[] AllCombinations =
    [
        new(false, false, false),
        new(false, false, true),
        new(false, true, false),
        new(false, true, true),
        new(true, false, false),
        new(true, false, true),
        new(true, true, false),
        new(true, true, true)
    ];

    private static ISpecification<Probe> SpecA => new Specification<Probe>(x => x.A);

    private static ISpecification<Probe> SpecB => new Specification<Probe>(x => x.B);

    private static ISpecification<Probe> SpecC => new Specification<Probe>(x => x.C);

    // 1. Construção válida
    [Fact]
    public void Constructor_WithCriteria_ShouldCreateSpecification()
    {
        Expression<Func<Probe, bool>> criteria = x => x.A;

        var specification = new Specification<Probe>(criteria);

        specification.ShouldNotBeNull();
        specification.ShouldBeAssignableTo<ISpecification<Probe>>();
    }

    // 2. Rejeição de expressão nula
    [Fact]
    public void Constructor_WithNullCriteria_ShouldThrowArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new Specification<Probe>(null!));

        exception.ParamName.ShouldBe("criteria");
    }

    // 3. Preservação da instância
    [Fact]
    public void Criteria_ShouldReturnSameExpressionInstance()
    {
        Expression<Func<Probe, bool>> criteria = x => x.A;

        var specification = new Specification<Probe>(criteria);

        specification.Criteria.ShouldBeSameAs(criteria);
        specification.Criteria.ShouldBeSameAs(specification.Criteria);
    }

    // 4. And — tabela verdade
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void And_ShouldFollowTruthTable(bool left, bool right, bool expected)
    {
        var result = SpecA.And(SpecB);

        Evaluate(result, new Probe(left, right, false)).ShouldBe(expected);
    }

    // 5. Or — tabela verdade
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void Or_ShouldFollowTruthTable(bool left, bool right, bool expected)
    {
        var result = SpecA.Or(SpecB);

        Evaluate(result, new Probe(left, right, false)).ShouldBe(expected);
    }

    // 6. Not
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Not_ShouldInvertResult(bool value, bool expected)
    {
        Evaluate(SpecA.Not(), new Probe(value, false, false)).ShouldBe(expected);
    }

    // 7. Dupla negação
    [Fact]
    public void Not_Twice_ShouldBeEquivalentToOriginal()
    {
        var result = SpecA.Not().Not();

        foreach (var probe in AllCombinations)
        {
            Evaluate(result, probe).ShouldBe(probe.A);
        }
    }

    [Fact]
    public void And_Or_Not_ShouldReturnNewSpecificationsAndNotChangeOperands()
    {
        var left = SpecA;
        var right = SpecB;
        var leftCriteria = left.Criteria;
        var rightCriteria = right.Criteria;

        var and = left.And(right);
        var or = left.Or(right);
        var not = left.Not();

        and.ShouldNotBeSameAs(left);
        and.ShouldNotBeSameAs(right);
        or.ShouldNotBeSameAs(left);
        not.ShouldNotBeSameAs(left);
        and.ShouldBeOfType<Specification<Probe>>();
        or.ShouldBeOfType<Specification<Probe>>();
        not.ShouldBeOfType<Specification<Probe>>();
        left.Criteria.ShouldBeSameAs(leftCriteria);
        right.Criteria.ShouldBeSameAs(rightCriteria);
    }

    [Fact]
    public void Composition_ShouldUseExpectedNodeTypes()
    {
        SpecA.And(SpecB).Criteria.Body.NodeType.ShouldBe(ExpressionType.AndAlso);
        SpecA.Or(SpecB).Criteria.Body.NodeType.ShouldBe(ExpressionType.OrElse);
        SpecA.Not().Criteria.Body.NodeType.ShouldBe(ExpressionType.Not);
    }

    // 8. True
    [Fact]
    public void True_ShouldBeSatisfiedByAnyElement()
    {
        var specification = Specs.True<Probe>();

        foreach (var probe in AllCombinations)
        {
            Evaluate(specification, probe).ShouldBeTrue();
        }
    }

    // 9. False
    [Fact]
    public void False_ShouldNeverBeSatisfied()
    {
        var specification = Specs.False<Probe>();

        foreach (var probe in AllCombinations)
        {
            Evaluate(specification, probe).ShouldBeFalse();
        }
    }

    [Fact]
    public void TrueAndFalse_ShouldNotBeNullAndShouldHaveCriteria()
    {
        Specs.True<Probe>().ShouldNotBeNull().Criteria.ShouldNotBeNull();
        Specs.False<Probe>().ShouldNotBeNull().Criteria.ShouldNotBeNull();
        Specs.True<Probe>().Criteria.Parameters.Count.ShouldBe(1);
        Specs.False<Probe>().Criteria.Parameters.Count.ShouldBe(1);
    }

    // 10. Identidades lógicas
    [Fact]
    public void LogicalIdentities_ShouldHoldByBehavior()
    {
        foreach (var probe in AllCombinations)
        {
            Evaluate(SpecA.And(Specs.True<Probe>()), probe).ShouldBe(probe.A);
            Evaluate(Specs.True<Probe>().And(SpecA), probe).ShouldBe(probe.A);
            Evaluate(SpecA.Or(Specs.False<Probe>()), probe).ShouldBe(probe.A);
            Evaluate(Specs.False<Probe>().Or(SpecA), probe).ShouldBe(probe.A);
            Evaluate(SpecA.And(Specs.False<Probe>()), probe).ShouldBeFalse();
            Evaluate(SpecA.Or(Specs.True<Probe>()), probe).ShouldBeTrue();
            Evaluate(Specs.True<Probe>().Not(), probe).ShouldBeFalse();
            Evaluate(Specs.False<Probe>().Not(), probe).ShouldBeTrue();
        }
    }

    [Fact]
    public void DeMorganLaws_ShouldHoldByBehavior()
    {
        foreach (var probe in AllCombinations)
        {
            Evaluate(SpecA.And(SpecB).Not(), probe).ShouldBe(Evaluate(SpecA.Not().Or(SpecB.Not()), probe));
            Evaluate(SpecA.Or(SpecB).Not(), probe).ShouldBe(Evaluate(SpecA.Not().And(SpecB.Not()), probe));
        }
    }

    // 11. Argumentos null
    [Fact]
    public void And_WithNullLeft_ShouldThrowArgumentNullExceptionForLeft()
    {
        Should.Throw<ArgumentNullException>(() => ((ISpecification<Probe>)null!).And(SpecB)).ParamName.ShouldBe("left");
    }

    [Fact]
    public void And_WithNullRight_ShouldThrowArgumentNullExceptionForRight()
    {
        Should.Throw<ArgumentNullException>(() => SpecA.And(null!)).ParamName.ShouldBe("right");
    }

    [Fact]
    public void Or_WithNullLeft_ShouldThrowArgumentNullExceptionForLeft()
    {
        Should.Throw<ArgumentNullException>(() => ((ISpecification<Probe>)null!).Or(SpecB)).ParamName.ShouldBe("left");
    }

    [Fact]
    public void Or_WithNullRight_ShouldThrowArgumentNullExceptionForRight()
    {
        Should.Throw<ArgumentNullException>(() => SpecA.Or(null!)).ParamName.ShouldBe("right");
    }

    [Fact]
    public void Not_WithNullSpecification_ShouldThrowArgumentNullExceptionForSpecification()
    {
        Should.Throw<ArgumentNullException>(() => ((ISpecification<Probe>)null!).Not()).ParamName.ShouldBe("specification");
    }

    // 12. Criteria null em implementações personalizadas
    [Fact]
    public void And_WithNullCriteriaInLeft_ShouldThrowArgumentExceptionForLeft()
    {
        var exception = Should.Throw<ArgumentException>(() => new NullCriteria().And(SpecB));

        exception.ShouldNotBeOfType<ArgumentNullException>();
        exception.ParamName.ShouldBe("left");
    }

    [Fact]
    public void And_WithNullCriteriaInRight_ShouldThrowArgumentExceptionForRight()
    {
        var exception = Should.Throw<ArgumentException>(() => SpecA.And(new NullCriteria()));

        exception.ShouldNotBeOfType<ArgumentNullException>();
        exception.ParamName.ShouldBe("right");
    }

    [Fact]
    public void Or_WithNullCriteriaInLeft_ShouldThrowArgumentExceptionForLeft()
    {
        var exception = Should.Throw<ArgumentException>(() => new NullCriteria().Or(SpecB));

        exception.ShouldNotBeOfType<ArgumentNullException>();
        exception.ParamName.ShouldBe("left");
    }

    [Fact]
    public void Or_WithNullCriteriaInRight_ShouldThrowArgumentExceptionForRight()
    {
        var exception = Should.Throw<ArgumentException>(() => SpecA.Or(new NullCriteria()));

        exception.ShouldNotBeOfType<ArgumentNullException>();
        exception.ParamName.ShouldBe("right");
    }

    [Fact]
    public void Not_WithNullCriteria_ShouldThrowArgumentExceptionForSpecification()
    {
        var exception = Should.Throw<ArgumentException>(() => new NullCriteria().Not());

        exception.ShouldNotBeOfType<ArgumentNullException>();
        exception.ParamName.ShouldBe("specification");
    }

    [Fact]
    public void And_WithBothNullCriteria_ShouldValidateLeftFirst()
    {
        Should.Throw<ArgumentException>(() => new NullCriteria().And(new NullCriteria())).ParamName.ShouldBe("left");
        Should.Throw<ArgumentException>(() => new NullCriteria().Or(new NullCriteria())).ParamName.ShouldBe("left");
    }

    [Fact]
    public void Composition_WithCustomImplementation_ShouldWork()
    {
        var result = new Custom(x => x.A).And(new Custom(x => x.B));

        Evaluate(result, new Probe(true, true, false)).ShouldBeTrue();
        Evaluate(result, new Probe(true, false, false)).ShouldBeFalse();
    }

    // 13. Curto-circuito
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void And_ShouldShortCircuit(bool left, int expectedRightEvaluations)
    {
        var counter = new Counter();
        var result = new Specification<Probe>(x => x.A).And(new Specification<Probe>(x => counter.Hit(x.B)));

        Evaluate(result, new Probe(left, true, false));

        counter.Calls.ShouldBe(expectedRightEvaluations);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void Or_ShouldShortCircuit(bool left, int expectedRightEvaluations)
    {
        var counter = new Counter();
        var result = new Specification<Probe>(x => x.A).Or(new Specification<Probe>(x => counter.Hit(x.B)));

        Evaluate(result, new Probe(left, true, false));

        counter.Calls.ShouldBe(expectedRightEvaluations);
    }

    [Fact]
    public void Composition_ShouldNotEvaluateOperandsDuringConstruction()
    {
        var counter = new Counter();
        var operand = new Specification<Probe>(x => counter.Hit(x.A));

        _ = operand.And(operand);
        _ = operand.Or(operand);
        _ = operand.Not();

        counter.Calls.ShouldBe(0);
    }

    // 14. Ausência de InvocationExpression
    [Fact]
    public void Composition_ShouldNotProduceInvocationExpression()
    {
        var composed = SpecA.And(SpecB).Or(SpecC.Not()).And(Specs.True<Probe>());

        NodeCollector.Collect<InvocationExpression>(composed.Criteria).ShouldBeEmpty();
        composed.Criteria.Body.ShouldNotBeAssignableTo<InvocationExpression>();
    }

    // 15. Reescrita de parâmetros
    [Fact]
    public void Composition_ShouldUseSingleNewParameterAndNoOriginalParameters()
    {
        var left = SpecA;
        var right = SpecB;
        var leftParameter = left.Criteria.Parameters[0];
        var rightParameter = right.Criteria.Parameters[0];

        var result = left.And(right);
        var parameter = result.Criteria.Parameters.ShouldHaveSingleItem();
        var used = NodeCollector.Collect<ParameterExpression>(result.Criteria.Body);

        parameter.ShouldNotBeSameAs(leftParameter);
        parameter.ShouldNotBeSameAs(rightParameter);
        parameter.Type.ShouldBe(typeof(Probe));
        parameter.Name.ShouldBe(leftParameter.Name);
        used.ShouldAllBe(p => ReferenceEquals(p, parameter));
        used.Count.ShouldBe(2);
    }

    [Fact]
    public void Composition_WithOperandsSharingTheSameParameter_ShouldStillEvaluateCorrectly()
    {
        var shared = Expression.Parameter(typeof(Probe), "x");
        var left = new Specification<Probe>(Expression.Lambda<Func<Probe, bool>>(Expression.Property(shared, nameof(Probe.A)), shared));
        var right = new Specification<Probe>(Expression.Lambda<Func<Probe, bool>>(Expression.Property(shared, nameof(Probe.B)), shared));

        var result = left.And(right);

        Evaluate(result, new Probe(true, true, false)).ShouldBeTrue();
        Evaluate(result, new Probe(true, false, false)).ShouldBeFalse();
        NodeCollector.Collect<ParameterExpression>(result.Criteria).ShouldAllBe(p => !ReferenceEquals(p, shared));
    }

    [Fact]
    public void Composition_WithDifferentParameterNames_ShouldEvaluateEachOperandOnTheSameInput()
    {
        var left = new Specification<Probe>(first => first.A && !first.B);
        var right = new Specification<Probe>(second => second.C);

        var result = left.And(right);

        Evaluate(result, new Probe(true, false, true)).ShouldBeTrue();
        Evaluate(result, new Probe(true, true, true)).ShouldBeFalse();
        Evaluate(result, new Probe(true, false, false)).ShouldBeFalse();
        result.Criteria.Parameters[0].Name.ShouldBe("first");
    }

    // 16. Lambdas aninhadas
    [Fact]
    public void And_WithNestedLambdas_ShouldEvaluateEachInnerLambdaAgainstOuterParameter()
    {
        var left = new Specification<Probe>(x => x.Values.Any(v => v > x.Min));
        var right = new Specification<Probe>(x => x.Values.Any(v => v < x.Max));

        var result = left.And(right);

        Evaluate(result, new Probe(values: [1, 5, 9], min: 4, max: 2)).ShouldBeTrue();
        Evaluate(result, new Probe(values: [1, 5, 9], min: 9, max: 2)).ShouldBeFalse();
        Evaluate(result, new Probe(values: [5, 9], min: 4, max: 2)).ShouldBeFalse();
    }

    [Fact]
    public void Or_WithNestedLambdas_ShouldEvaluateEachInnerLambdaAgainstOuterParameter()
    {
        var left = new Specification<Probe>(x => x.Values.Any(v => v > x.Min));
        var right = new Specification<Probe>(x => x.Values.All(v => v < x.Max));

        var result = left.Or(right);

        Evaluate(result, new Probe(values: [1], min: 4, max: 2)).ShouldBeTrue();
        Evaluate(result, new Probe(values: [3], min: 4, max: 2)).ShouldBeFalse();
        Evaluate(result, new Probe(values: [5], min: 4, max: 2)).ShouldBeTrue();
    }

    [Fact]
    public void Not_WithNestedLambda_ShouldKeepInnerLambdaIntact()
    {
        var result = new Specification<Probe>(x => x.Values.Any(v => v > x.Min)).Not();

        Evaluate(result, new Probe(values: [1, 2], min: 4)).ShouldBeTrue();
        Evaluate(result, new Probe(values: [1, 5], min: 4)).ShouldBeFalse();
    }

    // 17. Preservação de escopo
    [Fact]
    public void Composition_ShouldPreserveParametersOfNestedLambdas()
    {
        Expression<Func<Probe, bool>> leftCriteria = x => x.Values.Any(v => v > x.Min);
        Expression<Func<Probe, bool>> rightCriteria = x => x.Values.Any(v => v < x.Max);
        var innerParameters = NodeCollector.Collect<LambdaExpression>(leftCriteria)
            .Concat(NodeCollector.Collect<LambdaExpression>(rightCriteria))
            .Where(lambda => lambda.Parameters[0].Type == typeof(int))
            .Select(lambda => lambda.Parameters[0])
            .ToArray();

        var result = new Specification<Probe>(leftCriteria).And(new Specification<Probe>(rightCriteria));

        var composedInner = NodeCollector.Collect<LambdaExpression>(result.Criteria)
            .Where(lambda => lambda.Parameters[0].Type == typeof(int))
            .Select(lambda => lambda.Parameters[0])
            .ToArray();
        composedInner.Length.ShouldBe(2);
        composedInner.ShouldBe(innerParameters);
    }

    [Fact]
    public void Composition_ShouldNotReplaceParameterShadowedByNestedLambda()
    {
        var shared = Expression.Parameter(typeof(Probe), "x");
        var inner = Expression.Lambda<Func<Probe, bool>>(Expression.Property(shared, nameof(Probe.B)), shared);
        var body = Expression.AndAlso(
            Expression.Property(shared, nameof(Probe.A)),
            Expression.Invoke(inner, Expression.Constant(new Probe(false, true, false))));
        var left = new Specification<Probe>(Expression.Lambda<Func<Probe, bool>>(body, shared));

        var result = left.And(Specs.True<Probe>());

        NodeCollector.Collect<LambdaExpression>(result.Criteria).ShouldContain(lambda => ReferenceEquals(lambda, inner));
        Evaluate(result, new Probe(true, false, false)).ShouldBeTrue();
        Evaluate(result, new Probe(false, true, false)).ShouldBeFalse();
    }

    [Fact]
    public void Composition_ShouldNotReplaceParameterShadowedByBlockVariable()
    {
        var shared = Expression.Parameter(typeof(Probe), "x");
        var block = Expression.Block(
            [shared],
            Expression.Assign(shared, Expression.Constant(new Probe(true, true, true))),
            Expression.Property(shared, nameof(Probe.B)));
        var body = Expression.AndAlso(Expression.Property(shared, nameof(Probe.A)), block);
        var left = new Specification<Probe>(Expression.Lambda<Func<Probe, bool>>(body, shared));

        var result = left.Or(Specs.False<Probe>());

        NodeCollector.Collect<BlockExpression>(result.Criteria).ShouldContain(candidate => ReferenceEquals(candidate, block));
        Evaluate(result, new Probe(true, false, false)).ShouldBeTrue();
        Evaluate(result, new Probe(false, false, false)).ShouldBeFalse();
    }

    [Fact]
    public void Composition_ShouldNotReplaceParameterShadowedByCatchVariable()
    {
        var shared = Expression.Parameter(typeof(Exception), "e");
        var handler = Expression.Catch(shared, Expression.Constant(true));
        var tryExpression = Expression.TryCatch(
            Expression.Throw(Expression.Constant(new InvalidOperationException()), typeof(bool)),
            handler);
        var body = Expression.AndAlso(
            Expression.Equal(Expression.Property(shared, nameof(Exception.Message)), Expression.Constant("m")),
            tryExpression);
        var left = new Specification<Exception>(Expression.Lambda<Func<Exception, bool>>(body, shared));

        var result = left.And(Specs.True<Exception>());

        NodeCollector.Collect<CatchBlock>(result.Criteria).ShouldContain(candidate => ReferenceEquals(candidate, handler));
        result.Criteria.Compile()(new Exception("m")).ShouldBeTrue();
        result.Criteria.Compile()(new Exception("other")).ShouldBeFalse();
    }

    [Fact]
    public void Composition_ShouldReplaceParameterUsedFreelyInsideBlockWithOtherVariables()
    {
        var parameter = Expression.Parameter(typeof(Probe), "x");
        var local = Expression.Variable(typeof(bool), "local");
        var block = Expression.Block(
            [local],
            Expression.Assign(local, Expression.Property(parameter, nameof(Probe.A))),
            local);
        var left = new Specification<Probe>(Expression.Lambda<Func<Probe, bool>>(block, parameter));

        var result = left.And(Specs.True<Probe>());

        Evaluate(result, new Probe(true, false, false)).ShouldBeTrue();
        Evaluate(result, new Probe(false, false, false)).ShouldBeFalse();
    }

    [Fact]
    public void Composition_ShouldReplaceParameterUsedFreelyInsideCatchBody()
    {
        var parameter = Expression.Parameter(typeof(Probe), "x");
        var handler = Expression.Catch(
            Expression.Parameter(typeof(InvalidOperationException), "ex"),
            Expression.Property(parameter, nameof(Probe.A)));
        var body = Expression.TryCatch(
            Expression.Throw(Expression.Constant(new InvalidOperationException()), typeof(bool)),
            handler);
        var left = new Specification<Probe>(Expression.Lambda<Func<Probe, bool>>(body, parameter));

        var result = left.Or(Specs.False<Probe>());

        Evaluate(result, new Probe(true, false, false)).ShouldBeTrue();
        Evaluate(result, new Probe(false, false, false)).ShouldBeFalse();
    }

    [Fact]
    public void Exceptions_ForNullCriteria_ShouldCarryMessage()
    {
        Should.Throw<ArgumentException>(() => new NullCriteria().Not()).Message.ShouldStartWith("O critério da especificação");
    }

    [Fact]
    public void Composition_ShouldNotCaptureOuterParameterInsideNestedLambda()
    {
        // Operando da direita usa x (externo) dentro de uma lambda cujo parâmetro também se chama x.
        var left = new Specification<Probe>(x => x.A);
        var right = new Specification<Probe>(x => x.Values.Any(x2 => x2 == x.Min));

        var result = left.And(right);

        Evaluate(result, new Probe(true, false, false, [3, 4], 4)).ShouldBeTrue();
        Evaluate(result, new Probe(true, false, false, [3, 5], 4)).ShouldBeFalse();
    }

    // 18. Composição encadeada
    [Fact]
    public void ChainedComposition_ShouldMatchEquivalentBooleanExpression()
    {
        var result = SpecA.And(SpecB).Or(SpecC.Not()).And(SpecA.Or(SpecC));

        foreach (var p in AllCombinations)
        {
            Evaluate(result, p).ShouldBe(((p.A && p.B) || !p.C) && (p.A || p.C));
        }
    }

    [Fact]
    public void ChainedComposition_OfComposedSpecifications_ShouldKeepSingleParameter()
    {
        var result = SpecA.And(SpecB).And(SpecC).Or(SpecA.Not());

        result.Criteria.Parameters.Count.ShouldBe(1);
        NodeCollector.Collect<ParameterExpression>(result.Criteria).ShouldAllBe(p => ReferenceEquals(p, result.Criteria.Parameters[0]));
    }

    [Fact]
    public void Composition_WithSameSpecificationTwice_ShouldEvaluateConsistently()
    {
        var spec = SpecA;

        foreach (var probe in AllCombinations)
        {
            Evaluate(spec.And(spec), probe).ShouldBe(probe.A);
            Evaluate(spec.Or(spec), probe).ShouldBe(probe.A);
        }
    }

    // 19. Avaliação consistente com entradas estáveis
    [Fact]
    public void Evaluation_WithStableInput_ShouldBeConsistent()
    {
        var compiled = SpecA.And(SpecB).Or(SpecC).Criteria.Compile();
        var probe = new Probe(true, false, true);

        var first = compiled(probe);

        for (var i = 0; i < 5; i++)
        {
            compiled(probe).ShouldBe(first);
        }

        first.ShouldBeTrue();
    }

    // Limite documentado: closures são lidas na avaliação, não na construção
    [Fact]
    public void Closure_IsReadAtEvaluationTime_NotAtConstruction()
    {
        var threshold = 1;
        var specification = new Specification<Probe>(x => x.Min > threshold).And(Specs.True<Probe>());
        var compiled = specification.Criteria.Compile();

        compiled(new Probe(min: 2)).ShouldBeTrue();

        threshold = 5;

        compiled(new Probe(min: 2)).ShouldBeFalse();
    }

    private static bool Evaluate(ISpecification<Probe> specification, Probe probe) =>
        specification.Criteria.Compile()(probe);

    private sealed class Probe(
        bool a = false,
        bool b = false,
        bool c = false,
        IReadOnlyList<int>? values = null,
        int min = 0,
        int max = 0)
    {
        public bool A { get; } = a;

        public bool B { get; } = b;

        public bool C { get; } = c;

        public IReadOnlyList<int> Values { get; } = values ?? [];

        public int Min { get; } = min;

        public int Max { get; } = max;
    }

    private sealed class Counter
    {
        public int Calls { get; private set; }

        public bool Hit(bool value)
        {
            Calls++;
            return value;
        }
    }

    private sealed class NullCriteria : ISpecification<Probe>
    {
        public Expression<Func<Probe, bool>> Criteria => null!;
    }

    private sealed class Custom(Expression<Func<Probe, bool>> criteria) : ISpecification<Probe>
    {
        public Expression<Func<Probe, bool>> Criteria { get; } = criteria;
    }

    private sealed class NodeCollector : ExpressionVisitor
    {
        private readonly List<object> _nodes = [];

        public static List<T> Collect<T>(Expression expression)
            where T : class
        {
            var collector = new NodeCollector();
            collector.Visit(expression);
            return collector._nodes.OfType<T>().ToList();
        }

        public override Expression? Visit(Expression? node)
        {
            if (node is not null)
            {
                _nodes.Add(node);
            }

            return base.Visit(node);
        }

        protected override CatchBlock VisitCatchBlock(CatchBlock node)
        {
            _nodes.Add(node);
            return base.VisitCatchBlock(node);
        }
    }
}

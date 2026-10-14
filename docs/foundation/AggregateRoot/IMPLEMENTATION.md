# Detalhes de Implementação — AggregateRoot

## Status

**Implementation Complete — Auditoria Codex Pendente**

- **Contract:** definido em `API.md`
- **Decisões arquiteturais:** D01–D08 (ver seção abaixo)
- **Invariantes:** documentados em `BUSINESS_RULES.md`
- **Unit tests:** estrutura e cenários definidos em `TESTS.md`
- **Arquitetura tests:** estrutura definida em `TESTS.md`
- **Implementation:** ✅ `src/Business.Platform.Core.Domain/Shared/AggregateRoots/AggregateRoot.cs`

---

## Implementação Efetiva

A estrutura esperada para `AggregateRoot<TId>`:

```csharp
namespace Business.Platform.Core.Domain.Shared;

public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    protected AggregateRoot(TId id)
        : base(id)
    {
    }
}
```

**Notas:**

- **Sem membros adicionais:** a classe expõe apenas o construtor protegido; tudo mais é herdado de `Entity<TId>`.
- **Sem sobrescrita:** nenhum membro de `Entity<TId>` é redefinido ou sobrescrito.
- **XML documentation:** a classe e o construtor possuem comentários XML concisos no código-fonte; subclasses concretas documentam seus próprios contratos.

---

## Invariantes de Implementação

### I-ARG-01: Herança de Entity<TId>

`AggregateRoot<TId>` deve herdar de `Entity<TId>` com a mesma restrição genérica `where TId : notnull`.

**Verificação:** teste arquitetural que valida que `AggregateRoot<Guid>` é assignable a `Entity<Guid>`.

---

### I-ARG-02: Construtor Protegido Mínimo

`AggregateRoot<TId>` expõe exatamente um construtor protegido com assinatura:

```csharp
protected AggregateRoot(TId id) : base(id) { }
```

Nenhum construtor público, estático, nem parâmetros adicionais.

**Verificação:** teste arquitetural que valida:
- Nenhum construtor público
- Exatamente um construtor protegido
- Aceita parâmetro `TId id`

---

### I-ARG-03: Sem Redefinição de Igualdade

`Equals(object?)`, `Equals(AggregateRoot<TId>?)`, `GetHashCode()`, `operator ==` e `operator !=` não são definidos em `AggregateRoot<TId>`.

São todos herdados de `Entity<TId>`.

**Verificação:** teste arquitetural que valida a herança e ausência de sobrescrita.

---

### I-ARG-04: Apenas Id Herdado

`AggregateRoot<TId>` não declara propriedades, campos ou métodos públicos adicionais.

Apenas `Id` (herdado) é exposto.

**Verificação:** teste de reflexão que valida:
- Nenhuma propriedade pública declarada em `AggregateRoot<TId>`
- Nenhum campo público declarado em `AggregateRoot<TId>`
- `Id` é acessível (herdado)

---

### I-ARG-05: Sem Dependências de Infraestrutura

Nenhuma dependência de ORM, banco de dados, logging, cache, eventos, persistência, serialização, ASP.NET Core, ou qualquer biblioteca de infraestrutura.

Apenas dependências de `System.Runtime`, `System.Collections` (via `Entity<TId>`) e `Entity<TId>` itself.

**Verificação:** teste arquitetural que valida `ShouldNot().HaveDependencyOnAny([...])` para assemblies e namespaces proibidos.

---

### I-ARG-06: Abstrata e Não-Selada

`AggregateRoot<TId>` é `public abstract` e não é `sealed`.

Permite que subclasses concretas herdem e implementem agregados específicos.

**Verificação:** teste arquitetural que valida `IsAbstract == true` e `IsSealed == false`.

---

### I-ARG-07: Namespace Correto

`AggregateRoot<TId>` reside em `Business.Platform.Core.Domain.Shared`, o mesmo namespace de `Entity<TId>`.

**Verificação:** teste arquitetural que valida namespace.

---

### I-ARG-08: Implementação Typesafe

O tipo genérico `TId` é restrito a `notnull` em compilação.

Nenhuma verificação adicional em runtime (a herança de `Entity<TId>` já a fornece).

**Verificação:** teste arquitetural que valida a restrição genérica através de atributos `NullableAttribute` ou `NullableContextAttribute`.

---

## Contrato de Herança

Espera-se que subclasses concretas de `AggregateRoot<TId>`:

1. Herdem de `AggregateRoot<TId>` com um tipo `TId` específico
2. Sejam `sealed` (recomendado, não obrigatório)
3. Chamem `base(id)` no construtor para inicializar a identidade
4. Controlem a instanciação (construtores privados com factory)
5. Encapsulem suas entidades filhas conforme decisão do agregado concreto (D07)
6. Implementem métodos que mantêm invariantes de agregado
7. Não sobrescrevam `Equals(object?)` nem `GetHashCode()` (`sealed` em `Entity<TId>`) e não redefinam `==` e `!=` (permitido pela linguagem, proibido por esta arquitetura)

```csharp
public sealed class Order : AggregateRoot<Guid>
{
    private readonly List<OrderLine> _lines = new();

    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    private Order(Guid id)
        : base(id)
    {
    }

    public static Result<Order> Create(Guid id)
    {
        // validações
        return Result.Success(new Order(id));
    }

    public void AddLine(OrderLine line)
    {
        // invariantes
        _lines.Add(line);
    }
}
```

---

## Decisões Arquiteturais

### D01: Herança Simples de Entity<TId>

`AggregateRoot<TId>` herda integralmente de `Entity<TId>` sem modificação.

**Alternativa Considerada:** interface `IAggregate` separada.  
**Decisão:** herança concreta oferece type-safety e contrato garantido; interface seria apenas um rótulo.

### D02: Construtor Protegido Mínimo

Nenhuma lógica, validação ou parâmetros adicionais no construtor de `AggregateRoot<TId>`.

**Alternativa Considerada:** factory `CreateAggregate(TId id)` estático.  
**Decisão:** factories pertencem a agregados concretos; a abstração é mínima.

### D03: Sem Redefinição de Operadores

Operadores herdados de `Entity<TId>` não são redefinidos.

**Alternativa Considerada:** operadores customizados em subclasses.  
**Decisão:** igualdade por `Id` é uniforme; subclasses não a personalizam.

### D04: Sem Eventos Nesta Feature

Coleta, publicação ou gerenciamento de eventos não faz parte de `AggregateRoot<TId>`.

**Alternativa Considerada:** `IEnumerable<IDomainEvent> Events`.  
**Decisão:** eventos são responsabilidade de outra feature; esta é minimalista.

### D05: Sem Persistência

Nenhuma integração com ORM, repository, ou especificação de consulta.

**Alternativa Considerada:** marcador `IAggregateRoot` para infraestrutura.  
**Decisão:** domínio não deve conhecer persistência; marcadores pertencem a infraestrutura.

### D06: Neutralidade em Multitenancy

`AggregateRoot<TId>` não define ou impõe multitenancy.

Domínios concretos podem exigir `TenantId` como propriedade legítima do agregado. Agregados específicos a implementam conforme necessário. A Foundation permanece neutra, sem opinião sobre isolamento de tenant.

**Alternativa Considerada:** incluir `TenantId` na abstração.  
**Decisão:** nem todos os agregados exigem multitenancy; a decisão pertence ao agregado concreto.

### D07: Encapsulamento de Entidades Filhas

Entidades filhas pertencem aos agregados concretos e são encapsuladas conforme necessário (por exemplo, coleção privada com métodos de acesso; `IReadOnlyList<T>` isolado não garante imutabilidade).

`AggregateRoot<TId>` não impõe padrão de encapsulamento. Agregados concretos decidem se expõem via `IReadOnlyList<T>`, métodos access, ou nenhuma exposição.

**Alternativa Considerada:** padrão de composição automática na abstração.  
**Decisão:** encapsulamento explícito pertence aos agregados concretos; evita mágica de framework.

### D08: Sem Selagem Obrigatória

`AggregateRoot<TId>` não é `sealed`; subclasses concretas podem ser abertas (para herança hierárquica) ou seladas (padrão DDD).

**Alternativa Considerada:** `sealed AggregateRoot<TId>`.  
**Decisão:** a decisão de herança pertence ao agregado concreto.

---

## Ambiguidades Não Resolvidas

### A1: Ciclo de Vida de Agregado

Resolvido: Factory `Create` retornando `Result<T>` é uma **opção recomendada**, não obrigatória.

Agregados concretos escolhem:

- Constructores privados com factory methods (padrão DDD) — retorna `Result<T>`
- Constructores públicos com validação em caller — lança exceção ou retorna `Result<T>`
- Padrão Builder para construção complexa — até invalidação final

`AggregateRoot<TId>` não impõe `Reconstitute` ou padrão de ciclo de vida.

**Decisão:** Foundation neutra; agregados concretos decidem.

### A2: Validação vs. Exceção vs. Result

Resolvido:

- **Result.Failure(...):** para falhas de negócio **esperadas** (ex: email duplicado, limite excedido). Agregados a usam em factory methods.
- **throw Exception:** para violações de contrato (ex: `ArgumentNullException` para `id == null`) ou estados inesperados.
- **Sem `DomainException` nesta feature:** não é criada nem usada nos exemplos.

**Decisão:** Foundation oferece `Entity<TId>` com validação de `id` obrigatória; agregados concretos escolhem Result ou exceção para falhas de domínio.

### A3: Identidade de Entidades Filhas

Resolvido: Entidades filhas **possuem identidade própria**, não herdam a da raiz.

- Herdam de `Entity<TId>` com `TId` próprio (ex: `Guid`)
- `OrderLine.Id` é diferente de `Order.Id` (ambos `Guid`, mas valores diferentes)
- Identidade única dentro do agregado é responsabilidade do agregado concreto

**Decisão:** genéricidade e flexibilidade; cada entidade filha escolhe seu `TId`.

### A4: Referências Entre Agregados

Resolvido: Agregados referenciam outros **por identificador**, não por referência direta.

- Dentro do agregado: `public Guid CustomerId { get; }` (ID, não `Customer customer`)
- Carregamento: Domain Service ou Application Service usa Repository/Query
- Lazy loading pertence a infraestrutura, fora do escopo

**Decisão:** Foundation sem Repository ou ORM; coordenação fora da classe-base.

---

## Estrutura de Testes Esperada

Ver `TESTS.md` para a estratégia completa de testes.

Resumo:

- **Unit Tests:** construtor, herança, sem redefinição de operadores
- **Architecture Tests:** namespace, tipo abstrato, construtor protegido, sem dependências
- **Implementação de agregados concretos:** testes específicos de cada agregado

---

## Dependências e Riscos

### Dependências

- `Entity<TId>`: obrigatória; `AggregateRoot<TId>` herda de `Entity<TId>`
- .NET 10: compatibilidade obrigatória

### Riscos

- **Risco R1:** Subclasses concretas quebram invariantes (ex: `Equals` customizado)
  - Mitigação: testes arquiteturais que validam nenhuma sobrescrita
  
- **Risco R2:** Agregados expõem coleções mutáveis
  - Mitigação: o encapsulamento de coleções é responsabilidade dos agregados concretos e está fora do escopo da suíte de AggregateRoot; os testes de AggregateRoot não validam coleções read-only. `IReadOnlyList<T>` isolado não garante imutabilidade
  
- **Risco R3:** Eventos ficam implícitos ("como adicionamos eventos depois?")
  - Mitigação: feature futura explicitamente planejada; não bloqueia FEATURE-0004

---

## Fora do Escopo

- Eventos de domínio
- Persistência ou mapeamento ORM
- Auditoria, timestamps, soft delete
- Multi-tenancy
- Serialização
- Repository pattern
- Value Objects (planejado em FEATURE-0006)
- Domain Services orquestração
- Qualquer dependência de infraestrutura


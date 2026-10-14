# Regras de Negócio e Invariantes — Entity

Este documento define as regras obrigatórias e invariantes arquiteturais do componente `Entity<TId>`.

Todas as implementações devem respeitar estas regras.

---

## RN-ENT-01 — Imutabilidade de Identidade

`Entity<TId>` deve ter `Id` imutável após sua construção.

A propriedade `Id` não deve possuir setter público ou protegido que permita alteração.

A identidade é o que define uma entidade — alterá-la representaria transformar a entidade em outra, o que viola a semântica de domínio.

---

## RN-ENT-02 — Id Obrigatório

Todo `Entity<TId>` deve possuir um `Id` não nulo.

`Id` deve ser estabelecido no momento da construção e ser validado para não-nulidade.

Nenhuma entidade pode existir sem uma identidade válida.

---

## RN-ENT-03 — Restrição de Tipo Genérico

O tipo genérico `TId` deve estar restrito a `where TId : notnull`.

Esta restrição garante que apenas tipos que não podem ser null possam ser utilizados como identificadores.

Tipos como `int?`, `Nullable<T>` são explicitamente proibidos no tempo de compilação.

---

## RN-ENT-04 — Igualdade por Tipo e Identidade

Dois objetos `Entity<TId>` são iguais **se e somente se**:

1. São instâncias do **mesmo tipo concreto** (não da mesma classe abstrata);
2. Possuem **exatamente o mesmo `Id`**.

Nenhum outro atributo, relacionamento ou estado participa da igualdade.

**Implicações:**

- `new User(id: 1)` == `new User(id: 1)` → verdadeiro
- `new User(id: 1) { Name = "Alice" }` == `new User(id: 1) { Name = "Bob" }` → verdadeiro
- `new User(id: 1)` == `new Admin(id: 1)` → falso (tipos concretos diferentes)
- Relacionamentos não alteram igualdade

---

## RN-ENT-05 — Sobrecarga de Operadores de Igualdade

`Entity<TId>` deve implementar:

- `bool Equals(object? obj)` — implementação obrigatória de igualdade por referência e valor
- `bool Equals(Entity<TId>? other)` — sobrecarga tipada
- `int GetHashCode()` — hash baseado em tipo e Id
- `bool operator ==(Entity<TId>? left, Entity<TId>? right)` — operador de igualdade
- `bool operator !=(Entity<TId>? left, Entity<TId>? right)` — operador de desigualdade

Todas estas implementações devem ser consistentes com RN-ENT-04.

---

## RN-ENT-06 — Referências Nulas

Comparações envolvendo referências nulas devem ser tratadas explicitamente:

- `null == null` → verdadeiro
- `null == entity` → falso
- `entity == null` → falso

A implementação de `==` e `!=` deve considerar nulidade em ambos os operandos.

---

## RN-ENT-07 — Independência de Infraestrutura

`Entity<TId>` não deve depender de:

- ORM (Entity Framework, NHibernate, Dapper);
- banco de dados;
- persistência;
- serialização;
- logging;
- cache;
- observabilidade;
- ASP.NET Core ou camada de apresentação;
- qualquer biblioteca de infraestrutura.

O componente pertence à Shared Foundation do domínio.

---

## RN-ENT-08 — Sem Relacionamentos Obrigatórios

`Entity<TId>` não deve definir:

- `TenantId`, `OrganizationId`, `ParentId`, `OwnerId` (multi-tenant ou hierarquia);
- propriedades de navegação (ex.: `IEnumerable<Child>`);
- lazy loading markers;
- dependências explícitas de outros agregados.

Relacionamentos são responsabilidade de:

- entidades concretas que herdam de `Entity<TId>`;
- AggregateRoot (em futuro abstrato);
- Domain Services que coordenam múltiplas entidades;
- infraestrutura de persistência.

---

## RN-ENT-09 — Herança Controlada

`Entity<TId>` é uma classe abstrata que pode ser herdada, mas:

- Não deve ser selada (para permitir herança múltipla de padrões como strategy);
- Construtores devem ser protegidos (não públicos);
- Subclasses concretas devem ser `sealed` ou abrir explicitamente sua hierarquia;
- Subclasses devem chamar o construtor base com um `Id` válido.

---

## RN-ENT-10 — Sem Validação Genérica de Default(TId)

`Entity<TId>` não deve rejeitar `default(TId)` genericamente.

Exemplos:

- `default(int)` é `0` — pode ser uma identidade válida em determinados contextos;
- `default(Guid)` é `Guid.Empty` — pode ser semanticamente inválido, mas isso é decisão de domínio;
- `default(string)` é `null` — será rejeitado por `where TId : notnull`, mas strings vazias podem ser válidas.

A validade de valores como `0`, `Guid.Empty`, `""` é **responsabilidade do tipo de identidade ou do domínio**, não da abstração `Entity<TId>`.

---

## RN-ENT-11 — Imutabilidade de Propriedade Base

Qualquer propriedade definida em `Entity<TId>` deve ser imutável.

Propriedades em subclasses podem ter lógica de validação, mas devem garantir que, uma vez que o objeto é "pronto", ele permanece válido.

---

## RN-ENT-12 — Thread-Safety

`Entity<TId>` deve ser thread-safe para leitura após construção.

Não há estado mutável, portanto leitura simultânea é segura.

Modificações representam estado inconsistente e devem ser evitadas por design (imutabilidade).

---

## RN-ENT-13 — Validação de Construção

A criação de um `Entity<TId>` inválido deve ser impedida.

Valores inválidos incluem:

- `Id` nulo → `ArgumentNullException`

Violações do contrato de construção representam erro de programação, não falha de negócio esperada.

---

## RN-ENT-14 — Sem Tipagem Covariante Explícita em Igualdade

A implementação de igualdade deve trabalhar apenas com tipos exatos (`Entity<TId>` ou subclasses do mesmo tipo concreto).

Não deve tentar conversão de tipo ou casting dinâmico em `Equals` além da verificação de tipo exato obrigatória.

---

## RN-ENT-15 — Estabilidade do Identificador

A propriedade `Id` não pode ser substituída após construção (sem setter público ou protegido).

A Foundation impede a substituição da propriedade `Id`, mas não garante imutabilidade profunda de objetos de referência que possam ser utilizados como `TId`.

Tipos personalizados (Value Objects, classes) utilizados como `TId` são responsáveis pela própria estabilidade semântica de igualdade e hashing durante a vida da entidade.

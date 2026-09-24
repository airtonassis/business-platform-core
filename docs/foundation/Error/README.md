# Componente: Error

O `Error` é um componente fundamental da Shared Foundation responsável por representar, de forma padronizada e imutável, falhas esperadas que podem ocorrer durante operações de domínio e casos de uso da plataforma.

## Propósito Arquitetural

Em uma arquitetura baseada em DDD e Clean Architecture, falhas esperadas de negócio devem ser representadas explicitamente, sem utilizar exceções como mecanismo normal de controle de fluxo.

O componente `Error` fornece o contrato utilizado por componentes como `Result` para transportar informações sobre uma falha de forma previsível e fortemente tipada.

Exceções continuam sendo utilizadas para situações excepcionais, violações de invariantes de programação ou estados que não deveriam ocorrer durante o fluxo normal da aplicação.

## Responsabilidades

O `Error` deve:

- identificar uma falha por meio de um código estável;
- fornecer uma descrição legível da falha;
- classificar a categoria da falha;
- ser imutável;
- possuir igualdade baseada em valor;
- fornecer uma representação explícita da ausência de erro através de `Error.None`.

## Estrutura Conceitual

Um erro é composto inicialmente por:

- `Code`: identificador estável e programático do erro;
- `Description`: descrição legível;
- `Type`: categoria semântica do erro.

## Tipos de Erro

A primeira versão deverá suportar categorias suficientes para representar os principais tipos de falha da plataforma:

- Failure
- Validation
- NotFound
- Conflict
- Unauthorized
- Forbidden

`Failure` representa uma falha genérica quando nenhuma categoria mais específica for adequada.

## Error.None

`Error.None` representa explicitamente a ausência de erro.

Ele deve ser utilizado em operações bem-sucedidas, especialmente pelo componente `Result`.

`Error.None` não representa uma falha válida e não poderá ser utilizado para construir um `Result.Failure`.

## Relação com Result

O componente `Result` depende de `Error`.

A relação conceitual será:

Success → Error.None

Failure → Error válido

Dessa forma, `Error` deve estar implementado e validado antes da implementação do componente `Result`.

## Fora do Escopo

A primeira versão de `Error` não será responsável por:

- localização ou tradução de mensagens;
- serialização HTTP;
- geração de `ProblemDetails`;
- logging;
- persistência;
- tratamento global de exceções;
- códigos HTTP;
- integração com infraestrutura.

Essas responsabilidades pertencem a outras camadas da plataforma.
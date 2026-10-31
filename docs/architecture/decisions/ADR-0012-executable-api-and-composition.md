# ADR-0012 — API executável e composição de dependências

## Status

**Proposto — pendente de aprovação humana. Não autoriza implementação.**

Referências: [Plano Diretor](PLANO_DIRETOR_BUSINESS_PLATFORM_CORE.md), [ADR-0001](ADR-0001-technology-stack.md), [Sprint 1](../SPRINT_1_PLANEJAMENTO.md) e ADRs 0007–0011 propostos.

## Contexto e alternativas

Revisão vigente: [conta empresarial única](../CONTA_EMPRESARIAL_UNICA.md). Login resolve contexto pelo vínculo do servidor e entra no início da empresa. Não expor seleção/troca padrão ou gestão multiempresa. GET /me/context substitui hipóteses de listar/selecionar organizações; rotas de recurso com orgId validam igualdade com a empresa vinculada. Zero vínculo leva à preparação/convite, nunca a dados empresariais.

Auditoria encontrou Domain e Application na solução, Application sem casos de uso e arquivos de projeto Api/Infrastructure/Contracts vazios e fora da solução. O build anterior não comprova host funcional.

| Alternativa | Vantagens | Trade-offs e riscos |
|---|---|---|
| Minimal APIs organizadas por módulo | Estrutura inicial pequena, grupos/policies explícitos | Exige evitar concentração em Program.cs |
| Controllers | Convenções e organização conhecidas | Mais estrutura inicial; igualmente viável |
| Serviço por módulo | Implantação independente | Operação e consistência distribuídas prematuras |

## Recomendação proposta

Um host ASP.NET Core .NET 10, monólito modular, Minimal APIs agrupadas por módulo e interface Razor mínima para login/empresa vinculada/usuários/convite/auditoria; demonstração somente em homologação. Não é implementação do Console Platform Admin completo nem design system da plataforma.

| Projeto | Responsabilidade e dependências propostas |
|---|---|
| Domain | Foundation intacta e domínios específicos em pastas de módulo; sem frameworks novos |
| Application → Domain | Casos de uso, portas, contexto validado e resultados; sem ASP.NET/ORM/IdP |
| Contracts | DTOs públicos de transporte, sem referência a entidades ou Foundation |
| Infrastructure → Application + Domain | PostgreSQL, mapeamentos e adapters; implementa portas |
| Api → Application + Infrastructure + Contracts | Composition root, HTTP, autenticação padrão, policies e UI mínima |

Application mantém referência exclusiva ao Domain e nenhuma referência de pacote nesta proposta. Classes de registro DI ficam na borda/Infrastructure; não ampliar as permissões de dependência da Application para conveniência. Novas portas são de módulo, sem framework genérico. Reutilizar Error/Result, IDs e paginação onde cabível; mapear DTOs na borda, sem serializar agregados.

Proposta de contratos mínimos HTTP está no [pacote Sprint 1](../SPRINT_1_PLANEJAMENTO.md). OpenAPI versionada /api/v1, Problem Details sanitizado, correlação, limite de payload e rate limit nos pontos sensíveis. Schema HTTP distinto do Domain. Cookie/antiforgery na UI e JWT bearer nos endpoints externos com esquemas explícitos.

Liveness não acessa dados sensíveis. Readiness verifica dependências necessárias sem revelar segredos; nenhuma das duas comprova jornada. Não executar migrations automaticamente ao iniciar o host. Secrets e chaves de proteção fora do repositório; ambientes segregados. Mudança de dependência, SDK, pipeline ou infraestrutura exige autorização de implementação correspondente.

Não introduzir mediator, barramento distribuído, plugin engine, SDK de IA, catálogo comercial, billing ou console completo. SDK de produto e eventos de integração são entregas posteriores; não publicar assemblies internos como contrato de aplicações.

## Impactos e riscos

GIV e ERP Imobiliário mantêm implantação e linguagem independentes e integram por protocolos. Host único cria dependência de disponibilidade para as funções do Core; não implica que todo produto deve consultá-lo em cada operação. Sessão web mínima deve ter proteção e testes completos mesmo sendo demonstrativa.

## Critérios de aceite propostos

- Grafo de referências e guards comprovam as dependências autorizadas; contratos atuais da Foundation não mudam.
- Host inicia em ambiente isolado, sem migration automática ou segredo versionado.
- Jornada real com OIDC, PostgreSQL, autorização e auditoria; build/health isolados não aceitam a entrega.
- Contratos OpenAPI e códigos HTTP validados; API não responde redirect HTML para autenticação bearer inválida.
- UI mostra contexto, funciona em duas abas, teclado e viewport móvel; erros de concorrência/expiração oferecem recuperação sem repetir mutação automaticamente.
- Regressão da suíte existente, integração e E2E aprovados, com evidência por commit/ambiente.

## Aprovações

Arquitetura aprova topologia, projetos, APIs e novos pacotes; gestor aprova UI mínima e escopo; operação/segurança aprovam ambientes e credenciais. Design system completo, publicação, migrations e deploy não são autorizados por este ADR.

## Fechamento proposto UX-01/09/12/13/15

[C01/C09](../SPRINT_1_CONTRATOS_FLUXOS.md) detalham transporte e operador mínimo. Alternativas: Razor→Application no mesmo host ou adapter BFF→API. Recomenda-se primeira por eliminar rede/tokens adicionais sem duplicar autorização: web cookie+antiforgery e API bearer são adapters dos mesmos casos de uso, não HTTP interno nem browser chamando API cookie/bearer ambígua. Application permanece sem dependências web.

Operador mínimo inclui formulário owner/referência+organização, prévia, revisão e comprovante, sem dashboard global ou suporte. UserId/referência são conferidos por caso de uso restrito; não surge diretório global. Estado de erro/resultado desconhecido, ETag e convite seguem C02–C09. Nenhum contrato de cookie/session store vira contrato compartilhado com produtos.

Proposta de meta WCAG 2.2 AA e evidência por processo completo em [RF-19/UAT-13/18 e matriz WCAG](../SPRINT_1_RASTREABILIDADE_UX.md). Testar IdP, teclado/leitor, mobile, foco, tempo e recuperação; protótipo não executável não comprova comportamento runtime.

Backlog antecipa revisão documental e prototipação separadamente autorizada antes de congelar contratos. S1-10 é dividida em desenho/revisão preliminar e UI final, sem criar autorização de execução. Estimativa anterior não cobre automaticamente novos contratos/armazenamento/estados; capacidade requer reavaliação. Riscos: mais adapters/estados e dependências técnicas; critérios de aceite UAT-01–19, negativos de esquema/CSRF e guards preservados. Escolha de transporte, UI Platform e escopo dos novos endpoints exigem aceite humano; nenhum código/protótipo é criado.

## Impacto no portal e escopo revisado

P02/P03 são início/navegação da empresa. P04–P06 preparam/vinculam conta única; P07 distingue usuários/permissões de licenças futuras; P09 é administrativo separado; P11 mostra conceito futuro, sem API comercial; P12 somente avaliação/homologação, sem menu normal. Convites e bootstrap criam conta/membership atomicamente, sem permitir segundo vínculo. Múltiplas abas com a mesma sessão têm mesma empresa; relogin distinto exige revalidação.

Assinatura independente, produtos/módulos em pacotes, licenças, templates e configurações por empresa são arquitetura-alvo futura. Na Sprint 1 não criar catálogo, cobrança, license service ou agente para preencher painel. Proposta preserva Domain/Shared e referências de Application. Aprovar contexto/account model, contratos e UX antes de implementar; confirmar IdP/política para segunda empresa pela mesma pessoa.

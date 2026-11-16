---
name: saas-vertical-slice
description: Preparar ou entregar um incremento vertical do Business Platform Core, dos contratos à API e PostgreSQL, preservando autorização e isolamento. Usar para funcionalidades SaaS deste repositório; preparação não autoriza implementação ou deploy.
---

# Entrega vertical SaaS

Trabalhar a partir da raiz do repositório e seguir `AGENTS.md`. Consultar somente o
contrato aplicável, o estado vigente no início de `docs/architecture/SPRINT_1_PLANEJAMENTO.md`
e a implementação afetada. Se uma ADR ainda tem cabeçalho histórico “Proposto”,
confrontar o recorte expressamente aprovado no planejamento; não reapresentar E1
como pendente nem ampliar sua autorização para contratos futuros.

Antes de editar, distinguir **existente**, **adaptar**, **implementar**. Definir uma
operação observável, critérios positivos/negativos e dependências. Se o pedido for
apenas preparação, entregar esse recorte e parar antes de código funcional,
migrations/grants ou deploy. Não criar camadas ou DTOs sem consumidor real.

Quando a implementação estiver autorizada:

1. Contrato: definir ator, escopo, entradas, efeitos, erros e versões aprovados;
   preservar contrato de transporte e resultado incerto. Decisão ausente bloqueia
   somente a parte dependente, não concede aprovação implícita.
2. Domain/Application: reutilizar Foundation e políticas existentes; autorização
   precisa valer no caso de uso, inclusive fora do HTTP, sem duplicar regras no adapter.
3. Infrastructure: identidade SQL restrita, transação explícita e porta aprovada;
   eventos obrigatórios no mesmo commit. Nunca converter TEST-ONLY em migration.
   Migrations aplicadas são imutáveis; autorização de código não autoriza grants.
4. API: composição existente, scheme correto, validação, erros/correlação
   sanitizados, sem segredo em logs/URLs. Interface futura chama o mesmo caso de uso.
5. Testes: usar `quality-gates` antes de executar. Cobrir ação × papel × organização,
   revogação, rollback/auditoria e concorrência/ETag quando a operação os exigir.
6. Entrega: demonstrar o fluxo, não apenas container ativo; registrar o que não foi
   executado. Deploy e verificações reais dependem de autorização operacional aplicável.

Invariantes locais: uma identidade consome uma conta/organização permanentemente;
sem seletor ou autoridade por `tenant_id` do cliente. Papéis SQL Owner/Admin/Member
não vêm de headers/claims empresariais; Platform não ganha membership. RLS/FORCE,
owners SECURITY DEFINER e autorização transacional não se substituem entre si.
Preview C02 não escreve; não alterar seus limites sem requisito/falha demonstrada.
G3 não é teste de produto nem sua preparação concede execução.

Para qualquer novo produto ou integração, herdar o modelo de acesso único por
referência a DCSE-01, ARS-NAV-04 e à [consolidação estratégica](../../../docs/architecture/CONSOLIDACAO_ESTRATEGICA.md#acesso-integrado-coreprodutos):
usar identidade/contexto central e a entrada autenticada integrada, sem criar
login, portal, cadastro de organização, perfis, licenças ou permissões paralelos.
O Core é fonte de governança compartilhada; cada produto verifica no próprio
backend vínculo ativo, entitlement/licença, perfil ativo, permissão efetiva e,
quando aplicável, filial autorizada, permanecendo dono de seus dados e regras.
URLs técnicas próprias podem existir, mas não implicam uma experiência normal de
login separado. Não compartilhar cookies nem tratar diretriz aprovada como prova
de implementação, validação operacional ou publicação do mecanismo de federação.

Reportar arquivos, comportamento demonstrado, evidências com estado, impedimento
real e próxima ação. Não produzir outro plano quando já há um recorte aprovado.

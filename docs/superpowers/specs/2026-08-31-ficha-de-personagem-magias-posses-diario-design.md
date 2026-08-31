# Ficha de Personagem — Fase 1b: Magias & Habilidades, Posses, Diário — Design Spec

**Status:** Aprovado em chat pelo usuário (2026-08-31); aguardando revisão deste arquivo antes do plano de implementação.

## Contexto

Continuação direta da reconstrução visual do client sobre MudBlazor (ver `docs/superpowers/specs/2026-08-30-frontend-rebuild-fundacao-fichas-design.md`, a spec-guarda-chuva desta Fase 1, e `docs/superpowers/plans/2026-08-30-ficha-de-personagem-basicas-atributos-combate.md`, o plano já mergeado que reescreveu as 3 primeiras abas de `FichaDePersonagem.razor`). Esta spec cobre as 3 abas restantes da mesma página — **Magias & Habilidades**, **Posses**, **Diário** — hoje ainda em markup Bootstrap cru dentro dos `<MudTabPanel>` já abertos pelo plano anterior (ver comentário em `FichaDePersonagem.razor` linha ~229).

**O `@code` já está correto.** Como nas 3 abas anteriores, todo o comportamento de negócio destas 3 abas (chamadas HTTP, `Load*Async`/`Add*Async`/`Delete*Async`, modelos de formulário) já foi validado pelo gap-audit de 2026-08-29 e não muda nesta rodada — exceto o item pontual abaixo, ledger deixado pela revisão final da Fase 1a.

**Item ledgeado da Fase 1a:** a revisão final daquele plano encontrou que `MudNumericField T="int"` silenciosamente grava `0` ao ser limpo (o `int.TryParse` de guarda nos métodos `@code` aceita a caixa de `default(int)`), enquanto o `<InputNumber>` antigo bloqueava o submit inteiro via validação do `EditContext`. Foi corrigido nos 5 campos de atualização ao vivo (linha-a-linha, fora de `EditForm`) das abas já migradas, mas os 15 campos `int` do `SheetFormModel` (aba Informações Básicas, dentro do `EditForm`/`SaveAsync`) ficaram com o mesmo risco e foram explicitamente ledgeados para esta rodada, por compartilhar a causa raiz com o próprio campo `Ciclos` desta aba (Posses).

## Objetivo desta fase

1. Reescrever o markup das 3 abas restantes sobre os componentes MudBlazor e compartilhados já existentes (`Section`, `EntityPicker`), seguindo a mesma tabela de conversão já validada em duas rodadas anteriores (Fundação e Fase 1a) — reproduzida abaixo sem alteração de regra.
2. Fechar o item ledgeado: tornar os 15 campos `int` do `SheetFormModel` ligados via `@bind-Value` (Nível, Experiência Atual, Pontos de Ignição Atual/Total, 7× Núcleos de Rank, Adrenalina/Foco/Estresse/Vitalidade Atual) `int?` + `[Required]`, com `<DataAnnotationsValidator />` no `EditForm` que já existe na aba Informações Básicas — restaura o comportamento "não salva enquanto um campo estiver vazio" que existia antes desta reconstrução.

## Escopo — o que muda

### Abas (markup apenas, sem mudança de `@code`)

- **Magias & Habilidades**: exibição de Habilidade Racial (texto simples); formulário de Magia/Habilidade com seletor de Origem (Montar do zero / Escolher do Banco) alternando campos condicionalmente, sub-lista de Efeitos editável dentro do próprio formulário de adição (adicionar/remover linha antes de submeter); formulário + lista de Runas; formulário + lista de Maestrias.
- **Posses**: campo Ciclos (atualização ao vivo, fora de `EditForm` — mesmo padrão já corrigido nas abas anteriores, aqui só falta a conversão de markup); Inventário (formulário + lista com `EntityPicker` de Item); Artefatos (formulário + lista agrupada por Tipo de Alvo); Afeições (formulário + lista); Características (formulário + duas listas, Positivas/Negativas, com totais).
- **Diário**: formulário de nova entrada; lista de entradas com edição inline por item (alternância exibição/edição controlada por `_editingDiaryEntryId`, com Salvar/Cancelar).

### `@code` (única mudança de comportamento desta rodada)

- `SheetFormModel`: os 15 campos listados acima passam de `int` para `int?`, cada um com `[Required(ErrorMessage = "Campo obrigatório")]` (ou mensagem equivalente já usada em Login/Cadastro, mesmo padrão).
- Aba Informações Básicas ganha `<DataAnnotationsValidator />` dentro do `<EditForm Model="_form" OnValidSubmit="SaveAsync">` (hoje ausente).
- `SaveAsync`: como `OnValidSubmit` só dispara com os 15 campos preenchidos, usa `.Value` ao montar `UpdateCharacterSheetRequest` (sem fallback silencioso — se a suposição de não-nulidade estiver errada em algum ponto, deve estourar em vez de mascarar).
- `LoadSheetAsync` (ou onde `_form` é populado a partir de `CharacterSheetResponse`): atribuição `int` → `int?` é conversão implícita, nenhuma mudança de lógica necessária além do tipo do campo de destino.
- **Fora desta mudança**: `Ciclos` (não é campo do `SheetFormModel` ligado a `EditForm`/validação — é atualizado ao vivo via `UpdateCiclosAsync(object? value)`, que já aceita `int?` boxado sem mudança de assinatura) e todo `int` usado em formulários de adição *novos* (Grau/Quantidade/Custo em PI/Gasto Maestria etc. nos formulários de Magia, Runa, Maestria, Inventário) — esses continuam `MudNumericField T="int"` não-nulo, seguindo a mesma regra de conversão já usada nas abas anteriores para "InputNumber ligado bidirecionalmente a um modelo de formulário de adição" (um formulário de criação vazio não tem o mesmo risco de "regressão silenciosa de valor persistido" que um campo que já carrega um recurso salvo).

## Tabela de conversão (reaplicada sem alteração; ver o plano da Fase 1a para o texto completo)

| Antigo | Novo |
|---|---|
| `<InputText>` | `MudTextField T="string"` |
| `<InputTextArea>` / `<textarea>` bidirecional | `MudTextField T="string" Lines="3"` |
| `<InputNumber>` bidirecional a um modelo de formulário | `MudNumericField T="int"` (ou `T="int?"` nos 15 campos do item ledgeado acima) |
| `<select>` com `<option>` | `MudSelect T="string"` com `MudSelectItem` |
| `<input>`/`<select>` unidirecional ligado por item de lista (`value=@item.X @onchange=...`) | `MudNumericField`/`MudCheckBox`/`MudSelect` com `Value`+`ValueChanged` — valor já tipado, atribuição para `object? value` é boxing implícito |
| `<button type="submit" class="btn btn-primary">` | `MudButton ButtonType="ButtonType.Submit" Variant="Variant.Filled" Color="Color.Primary"` |
| `<button class="btn btn-outline-primary btn-sm">` | `MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small"` |
| `<button class="btn btn-outline-danger btn-sm">Remover` | `MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small"` |
| `<h3>`/`<h4>` agrupando um bloco | `<Section Title="...">` (já usado nesta página; um `<h4>` de subgrupo dentro de uma `Section` maior — ex. cada Tipo de Alvo dentro de Artefatos, Positivas/Negativas dentro de Características — vira um `<MudText Typo="Typo.h6">` interno, não uma nova `Section` aninhada) |
| `<table>`/`<ul class="list-unstyled">` de itens com ação por linha | `MudSimpleTable Dense="true" Hover="true"` reaproveitando o mesmo `@foreach` |
| `EntityPicker` | inalterado — mesmos parâmetros (`@bind-Value`/`Value`+`ValueChanged`, `SearchItems`, `Placeholder`) |

## Testes

- `dotnet build` em 0 warnings / 0 errors ao final de cada task (as tasks desta página são sequenciais, na mesma disciplina da Fase 1a — não há build limpo garantido entre tasks intermediárias que ainda não fecharam sua aba).
- Nenhum teste de integração/unit novo esperado — mudança de UI + um ajuste de validação client-side, sem endpoint novo.
- Revisão final do branch (mesma prática das rodadas anteriores) deve verificar empiricamente, se uma ferramenta de renderização estiver disponível na sessão que executar, que: (a) o formulário de Informações Básicas realmente bloqueia o submit com um campo vazio; (b) o seletor de Origem em Magias & Habilidades alterna os campos corretamente; (c) a edição inline do Diário não vaza estado entre entradas.

## Fora de escopo

- `FichaDeNpc.razor` / `FichaDeCriatura.razor` — planos futuros próprios, que reaproveitam este mesmo plano como referência de padrão (ver nota no plano da Fase 1a).
- Qualquer mudança de contrato/endpoint — nenhuma é necessária.
- Cobertura de teste de componente (bUnit) desta página — não fazia parte da Fase 1a e não é reintroduzida aqui pelos mesmos motivos já registrados (infraestrutura de stub de ~15 endpoints desproporcional a uma rodada de markup).

## Critérios de aceite

- As 3 abas renderizam sobre MudBlazor, com paridade de campos/ações em relação ao markup atual (nenhum campo do Requisitos - Ficha de Personagem.md perdido).
- Os 15 campos ledgeados do `SheetFormModel` são `int?` + `[Required]`; salvar a aba Informações Básicas com qualquer um vazio não persiste nada e mostra erro de validação.
- `Ciclos` na aba Posses usa o mesmo padrão `T="int?"` já estabelecido para os demais campos de atualização ao vivo.
- `dotnet build` limpo ao final do plano.
- Nenhuma mudança de `@code` fora do item ledgeado explicitamente listado acima.

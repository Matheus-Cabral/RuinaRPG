# Front-End Rebuild — Fundação + Fichas (MudBlazor) — Design Spec

**Status:** Aprovado em chat pelo usuário (2026-08-30); aguardando revisão deste arquivo antes do plano de implementação.

## Contexto

O client (`src/RuinaRPG.Client`, Blazor WebAssembly .NET 8) acumulou, ao longo de várias rodadas anteriores (ver memória `ruina-plan-queue`), uma base de HTML cru + Bootstrap + `components.css` (regras de tema em cima de elementos `<label>`/`<input>`/`<table>` sem classe) e um pacote de correções estruturais recentes (`Shared/Section.razor` envolvendo cada subseção, `Shared/EntityPicker.razor` substituindo campos de GUID cru — Épico 8 e "shared-picker rollout", ambos concluídos em 2026-08-30). Mesmo assim, o usuário considera o resultado "entrincado" e "desorganizado" e pediu uma reconstrução visual completa, com liberdade de tecnologia, mantendo só a paleta de cores dos temas Sol/Lua e as fontes (Cormorant Garamond + Inter).

Depois de uma primeira proposta de *portar* o markup existente para MudBlazor preservando estrutura, o usuário corrigiu explicitamente: **isso repetiria o padrão que causou o problema** (remendos incrementais em cima da mesma base). A decisão é **jogar fora e reconstruir do zero** a camada visual — não portar.

**Importante — o backend já está correto.** Um audit sistemático completo (`2026-08-29-gap-audit.md`, 9 épicos, ~45 PRs, todos mergeados) já fechou todos os gaps funcionais conhecidos entre `Docs/Requisitos/` e a API. Esta reconstrução não deve re-auditar nem redesenhar contratos/endpoints — deve construir UI nova que consome corretamente a API e os `Contracts` já existentes e corretos, lendo os `Requisitos - Ficha de *.md` como fonte de verdade de *campos e comportamento*, não o `.razor` antigo.

## Objetivo desta fase

Esta spec cobre **duas fases** de um projeto maior, decompostas porque o todo é grande demais para um plano só (ver `MEMORY.md` → `ruina-plan-queue` para o padrão já estabelecido de dividir em rodadas):

- **Fase 0 — Fundação**: novo shell da aplicação (layout, navegação, tema, tipografia) e o conjunto de componentes reutilizáveis que toda página nova vai usar, todos sobre MudBlazor.
- **Fase 1 — As 3 Fichas**: `FichaDePersonagem`, `FichaDeNpc`, `FichaDeCriatura` reescritas do zero sobre a Fundação — são as páginas maiores (1298/1121/981 linhas), mais usadas e mais citadas como "entrincadas".

**Fases futuras (specs próprios, não cobertos aqui):** Campanha, Gerenciador de Encontros, Catálogo/Banco de Magias, Compêndio, Login/Cadastro/Painéis GM/Convites — cada uma migra para os mesmos componentes da Fundação quando chegar sua vez.

## O que é jogado fora vs. o que fica

**Reconstruído do zero (a "camada visual"):**
- `Pages/` — pelo menos as 3 Fichas nesta rodada; o resto migra em fases futuras.
- `Layout/` (`MainLayout`, `NavMenu`, `ThemeToggle`) — inteiro, nesta fase.
- `Shared/` (`Section`, `TabControl`/`TabPage`, `Breadcrumbs`, `EntityPicker`) — inteiro, nesta fase. Nomes/API podem mudar livremente; nada aqui é "mantido por compatibilidade".
- `wwwroot/css/*` — `bootstrap/`, `components.css`, `app.css` são descartados **imediatamente** (incl. a referência a `bootstrap.min.css` em `index.html`), sem preocupação de manter o visual das páginas ainda não migradas. `theme.css` é **reduzido**, não reescrito: fica só com os `@font-face` e os tokens de cor `--rr-*` (claro/escuro) exatamente como estão hoje — nenhum hex novo, nenhuma fonte nova. Tudo que hoje é `components.css` (re-skin de Bootstrap/elementos crus) desaparece — quem faz esse trabalho agora é o tema do MudBlazor.

**Mantido como está (infraestrutura, não é "cara"):**
- `Services/` (`AuthStateService`, `TokenAuthenticationStateProvider`, `BearerTokenHandler`) — testado e corrigido no backlog recente, invisível ao usuário.
- `Program.cs` — só ganha o registro do MudBlazor (`AddMudServices()`); DI/HttpClient/Auth existentes não mudam.
- `App.razor` — roteamento inalterado.
- `wwwroot/js/theme.js` e o script inline em `index.html` que seta `data-theme` antes do primeiro paint — mecanismo correto, só o visual do `ThemeToggle` que o consome é reconstruído.
- `RuinaRPG.Contracts`, `RuinaRPG.Domain`, toda a API — fora de escopo, já corretos.

## Sem estratégia de transição — quebra visual das páginas fora de escopo é aceitável

Este projeto não está em produção; parar de funcionar temporariamente ou perder dados de banco não é um problema (decisão explícita do usuário — uma estratégia de coexistência/transição só enviesaria a execução da interface nova em favor de compatibilidade com a antiga). Portanto: **nenhum esforço é gasto mantendo as ~17 páginas fora desta fase visualmente funcionais.** `bootstrap.min.css` sai de `index.html` nesta própria fase, não na última. Páginas ainda não migradas podem renderizar sem estilo (HTML cru) até sua própria fase — isso é esperado e não é um defeito a corrigir aqui.

A única obrigação que **permanece**, porque é padrão de qualidade do repositório (`CLAUDE.md`) e não uma preocupação de continuidade visual: `dotnet build` tem que continuar em 0 warnings/0 errors. Isso significa que qualquer componente de `Shared/` referenciado por páginas fora de escopo (`EntityPicker`, `Section`, `TabControl`/`TabPage`) só pode ser removido ou ter sua API quebrada se os call-sites dessas páginas forem atualizados junto — mecanicamente (trocar `<Section>` por `<MudPaper>`, etc.), mesmo sem se importar com o resultado visual. Decisão de implementação: mais barato manter o nome/assinatura pública estável (ex. `EntityPicker` continua aceitando `Value`/`ValueChanged`/`SearchItems`) do que tocar ~15 páginas só para não quebrar o build.

## Direção estética — mantendo cor e tipografia como estão

- **Cor**: os `--rr-*` tokens de `theme.css` continuam sendo a única fonte da verdade. O `MudThemeProvider` não define paleta própria em C#; ao invés disso, `theme.css` ganha um bloco extra mapeando as CSS custom properties que o MudBlazor lê em runtime (`--mud-palette-primary`, `--mud-palette-background`, `--mud-palette-surface`, etc.) para `var(--rr-primary)`, `var(--rr-bg)`, `var(--rr-surface)` e assim por diante, dentro dos mesmos blocos `:root` / `:root[data-theme='dark']` que já existem. Zero paleta duplicada em C#.
- **Tipografia**: mesma dupla — Cormorant Garamond (headings) + Inter (corpo) — configurada via `Typography` do `MudTheme` (nomes de família apontando pros mesmos `@font-face` já hospedados em `wwwroot/fonts/`).
- **Layout**: rompe deliberadamente com a "cara" padrão do Material que o MudBlazor traz de fábrica (cards flutuantes, sombra pesada, cantos muito arredondados, densidade genérica). Em vez disso: `MudTheme.LayoutProperties` com os `--rr-radius-sm/md` atuais (quase retos, não os defaults do Material), elevação mínima (`Elevation="0"` como padrão dos `MudPaper`, com uma regra fina — hairline, `1px solid var(--rr-border)` — no topo de cada seção substituindo sombra), densidade compacta pra formulários tabulares densos como os das fichas.
- **Navegação e layout do shell**: liberdade total de forma (o usuário disse explicitamente que não se importa se o layout mudar totalmente) — a decisão concreta de shell (drawer lateral persistente vs. outra forma, comportamento em mobile) fica para a implementação, usando `MudLayout`/`MudDrawer`/`MudAppBar` como base técnica, mas sem a obrigação de reproduzir o hambúrguer/sidebar atual pixel a pixel. Deve continuar honesto sobre o que é alcançável por papel (GM vs. Jogador), como o shell atual já é.
- **Assinatura**: um **Selo de Linhagem** — emblema circular pequeno e reutilizável (glifo por Linhagem: Humano/Phylauc/Nephrytes/Ecônos, com um modificador visual — ex. anel sólido vs. tracejado — por Variante: Sinir/Laonir, Phylac'tai/Es'Phylauc, Yavos/Koroanos, Alóra) no cabeçalho de toda Ficha. Não é decoração: comunica linhagem + variante reais da ficha, construído só com `--rr-primary`/`--rr-accent`/`--rr-border` (sem cor nova). É o elemento que dá identidade visual própria ao app sem inventar paleta.

## Fundação (Fase 0) — inventário

**Pacotes:**
- `MudBlazor` (NuGet, versão estável mais recente compatível com net8.0) em `RuinaRPG.Client.csproj`.
- `index.html`: adiciona o CSS/JS do MudBlazor (`_content/MudBlazor/MudBlazor.min.css`, `_content/MudBlazor/MudBlazor.min.js`), mantém o script inline de tema e `bootstrap.min.css` (ver seção de transição acima).
- `Program.cs`: `builder.Services.AddMudServices();`.

**Layout (`Layout/`, reescrito):**
- `MainLayout.razor` sobre `MudLayout`/`MudAppBar`/`MudDrawer`.
- `NavMenu.razor` — mesma árvore de decisão de hoje (`AuthorizeView` por papel: deslogado / GM / Jogador, mesmos destinos de rota), visual novo sobre `MudNavMenu`.
- `ThemeToggle.razor` — mesmo contrato com `theme.js` (lê/persiste `localStorage`, sem flash), visual novo.

**Componentes compartilhados (`Shared/`, reescritos):**
- Um componente de seção/card (substitui `Section.razor`) sobre `MudPaper Elevation="0"` + regra hairline, título em Cormorant Garamond.
- `EntityPicker` reescrito sobre `MudAutocomplete<T>`, mantendo a mesma API pública (`Value`/`ValueChanged`/`SearchItems`/`Placeholder`) que os outros ~10 consumidores fora desta fase esperam — não por preocupação visual (essas páginas podem ficar sem estilo, ver seção acima), mas só pra não quebrar o build delas.
- `Breadcrumbs` sobre `MudBreadcrumbs`.
- Campos de domínio compartilhados novos: seletor de Linhagem/Variante em cascata, de Vocação/Sub-vocação em cascata, de Afinidade — hoje essas listas estão *hardcoded e duplicadas* em cada Ficha; nesta fase viram um único componente/fonte de dados reutilizado pelas 3 Fichas.
- `RrIdentityBadge` (Selo de Linhagem, ver seção estética).
- Tabs: `MudTabs` substitui `TabControl`/`TabPage` nas páginas migradas.

## Fase 1 — As 3 Fichas

Reescrita completa de `FichaDePersonagem.razor`, `FichaDeNpc.razor`, `FichaDeCriatura.razor` sobre a Fundação. Fonte de verdade para *o que* cada aba precisa: `Requisitos - Ficha de Personagem.md` (âncora) + os diffs em `Requisitos - Ficha de NPCs.md`/`Requisitos - Ficha de Criaturas.md` — não o `.razor` atual. Fonte de verdade para *como* falar com a API: os `Contracts` e controllers já existentes (inalterados).

Sem mudança de contrato/endpoint esperada — se a implementação encontrar um campo do Requisitos que a API não expõe (não deveria acontecer, dado o audit fechado), isso é escalado, não presumido.

## Testes

Não existe bUnit (ou equivalente) no repo hoje — decisão já adiada explicitamente em rodadas anteriores por ser "decisão de ferramenta que precede escopo". Esta reconstrução da Fundação é o momento certo de tomar essa decisão, já que introduz uma base de componentes nova mesmo: adicionar bUnit (novo projeto `tests/RuinaRPG.Tests.Client` ou dentro do `Unit` existente) cobrindo a lógica não-trivial dos componentes novos (regras do Selo de Linhagem, cascata Linhagem→Variante/Vocação→Sub-vocação, comportamento de busca/seleção do `EntityPicker`). TDD (R0011) se aplica a esse código; markup puro sem lógica (a maior parte de `MainLayout`/`NavMenu`) não precisa de teste dedicado, verificado por build + inspeção visual.

Verificação end-a-fim: `dotnet build` (0/0), mais verificação visual — este ambiente não teve Playwright disponível nas últimas rodadas de Épico 8 (diferente de rodadas anteriores de design system, que tinham); a implementação deve checar de novo se a ferramenta está disponível e usar renderização real se possível, caveado explicitamente se não.

## Riscos e mitigação

- **Build quebrado nas páginas fora de escopo** — o único risco que importa de fato (não o visual). Mitigado mantendo a API pública dos componentes de `Shared/` reaproveitados (`EntityPicker`, `Breadcrumbs`) estável, e fazendo um grep por todo consumidor antes de remover/renomear qualquer componente que páginas fora desta fase ainda referenciam (`TabControl`/`TabPage`/`Section`, se forem removidos em vez de mantidos).
- **bUnit é infraestrutura nova** — primeira vez que este repo roda testes de componente Blazor; risco de fricção de setup (mock de `IJSRuntime`/`HttpClient`) é esperado e deve ser tratado como parte normal da Fase 0, não bloqueador.

## Fora de escopo (fases futuras, não desta spec)

Campanha (`CampanhaDetalhe`, `Campanhas`, `MinhaCampanha`, `MinhasCampanhas`), Gerenciador de Encontros, Catálogo de Itens + Banco de Magias (listas e forms), Compêndio, Login/Cadastro/Landing/Painel/GmConvites/GmJogadores/NpcsDoGm/BestiarioDoGm. Cada uma vira seu próprio spec quando chegar a vez, reaproveitando a Fundação desta fase.

## Critérios de aceite

- `dotnet build` limpo (0 warnings/0 errors) com MudBlazor integrado.
- As 3 Fichas renderizam e salvam corretamente em ambos os temas (Sol/Lua), com os mesmos endpoints de hoje, sem regressão funcional.
- Nenhuma cor/hex nova introduzida fora de `--rr-*`; nenhuma fonte nova fora de Cormorant Garamond/Inter.
- `EntityPicker`/campos compartilhados novos têm cobertura bUnit para sua lógica não-trivial.
- Não é critério de aceite as páginas fora de escopo continuarem com aparência funcional — só precisam continuar compilando.

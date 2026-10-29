# 2.1.0 — Correções, divergências do D, sugestões do E e ortografia

**Status:** itens decididos pelo usuário em 2026-10-24 (backlog `2026-10-22-versao-2-1-0-backlog.md`, seção
"Decisões do usuário"). Este documento só os detalha para o plano. As escolhas que o usuário deixou comigo
("averigue caso a caso") estão marcadas como **proposta**.

## Estrutura (vale para tudo abaixo)

Mesma regra das outras entregas da 2.1.0: seguir a estrutura da 2.0.0 (`CLAUDE.md`, desenhos D e E) — recursos de
ficha genéricos sobre `ITipoDeFicha<TSheet>` em `Controllers/Fichas/`, só o que é exclusivo de um tipo na subclasse;
um record por forma em `Contracts/Fichas/`; abas compartilhadas em `Shared/Ficha/` guiadas por `FichaEstado` e
`TipoDeFichaCliente`, sem ramos "se NPC / se Criatura"; regras puras no Domain; nada copiado. Ao **unificar** um
comportamento entre tipos, o código específico da subclasse sai e o genérico passa a valer para todos; os testes
genéricos por tipo cobrem os três.

## A. Correções decididas (só implementar)

1. **Breadcrumb do NPC/Criatura concedido** (E, diferença A5). O jogador que abre uma ficha de NPC/Criatura
   concedida a ele vê "Painel / NPCs do GM" (ou "Bestiário do GM"), que leva a páginas só do GM. Passa a ver o
   caminho pela campanha, como na Ficha de Personagem: Painel → Minhas Campanhas → a campanha → a ficha. O GM
   continua vendo o caminho de hoje.
2. **"Placeholder" nos seletores de item** de NPC e Criatura (`Shared/CatalogoItemPicker.razor:13` passa o texto
   literal `Placeholder`). Usar o placeholder certo de cada campo, como os seletores da Ficha de Personagem. A rede
   de segurança `RedeDeSeguranca/` fixou o texto de hoje: atualizar com a correção, explicando no commit.
3. **`EstrelaSelect` quebra com uma resposta ruim de `/api/rulebook`**: tratar a falha como os outros campos que
   leem o livro — a aba continua de pé e o alerta de erro da página mostra a mensagem.
4. **PI perdido depois de editar o Nome rapidamente** (perda de dado). Editar o Nome e, dentro da janela de 400 ms
   do auto-save, os Pontos de Ignição: o recarregamento do salvamento geral (`LoadSheetAsync`) sobrescreve o PI do
   formulário antes de o salvamento do PI rodar, e a edição some. Corrigir para que nenhuma edição pendente se
   perca por um recarregamento (ligado ao resíduo conhecido do `AutoSaveCoordinator` de guardar um só salvamento
   pendente).
5. **Recusa ao trocar o atributo de uma Perícia sem mensagem** (`FichaEstado.Atributos.cs`, nos três tipos):
   mostrar a mensagem do servidor no alerta de erro, como as outras edições.

## B. Divergências do D (decisões do usuário)

Lista completa em `2026-10-18-versao-2-0-0-d-backend-design.md`, seção "Divergências encontradas".

1. **Jogador apaga NPC/Criatura concedido** → corrigir: só o GM da ficha apaga (Campanha R0010, "criação e
   exclusão continuam exclusivas do GM"). O jogador dono recebe a mesma recusa que um estranho.
2. **Maestria existente (PUT)** → NPC e Criatura também editam, como o Personagem (Ficha de Criaturas 4.e "mesma
   estrutura do Personagem"). O endpoint sai da subclasse do Personagem para o controller genérico.
3. **Orçamento de Perícias** → só o Personagem tem; **mantém**.
4. **Afeição existente (PUT)** → só o Personagem tem; **mantém**.
5. **Inventário do NPC aceita qualquer item** → correto; **mantém**.
6. **Magias: o Personagem nega leitura com 404 e escrita com 403** → o usuário disse "todos deveriam funcionar
   assim". **Proposta:** regra única para os recursos da Ficha de Personagem — **leitura por quem não tem acesso:
   404** (não confirma que a ficha existe) e **escrita por quem não tem acesso: 403**. As Magias já seguem isso.
7. **Peso carregado da Criatura** → atualizar para a fórmula de 2026-09-08 (só Inventário e Armas/Escudos
   **desequipados** contam), igual a Personagem e NPC. O hook `CargaAsync` sobrescrito em `CreatureSheetStats`
   deixa de existir.
8. **Leituras 404 × 403 do Personagem** → "averigue caso a caso". **Proposta:** pela regra do item 6, os cinco GET
   que hoje negam com 403 — `level-up-notice`, `sub-attributes`, `condicoes-ativas`, `equipment-penalties`,
   `modificador-de-dano` — passam a negar com **404**, como os outros GET da ficha. Caso a caso: todos são leituras
   da própria ficha que revelam o mesmo que o GET da ficha (já 404); nenhum é escrita. NPC e Criatura **não mudam**
   (já negam tudo com 404).

## C. Sugestões da revisão do E (aceitas)

1. **Base comum das páginas de ficha.** As três páginas `Pages/FichaDe*.razor` ainda repetem a coordenação da
   página (carga, auto-save, avisos em tempo real, alerta de erro, cabeçalho). Esse código comum vai para uma base
   única (componente ou classe base, escolha do construtor dentro do anti-objetivo do E: nada de base cheia de "se
   Personagem / se Criatura"); cada página fica só com o que é dela.
2. **Um fato próprio de "formato dos dados" em `TipoDeFichaCliente`**, em vez de reaproveitar flags de interface
   para escolher os records da Criatura.

Os dois são refatorações sem mudança de comportamento: as redes de segurança do cliente (`RedeDeSeguranca/`) não
podem mudar.

## D. Ortografia do livro de regras

Das 38 propostas de `2026-10-23-g-propostas-sistema-rpg.md`, **só ortografia** (decisão do usuário): acento,
grafia e letra maiúscula em texto corrido. Nada de regra, número, nome de jogo, reordenação, gramática de frase
ou nome de arquivo.

- Entram: 12 ("A criatura", "já", "utilização"), 21 só `conseqüências`/`paranóico`/`pêlo` (**não** `Lingüísta`,
  chave do seed), 25 ("histórico" no texto; o arquivo **não** é renomeado), 34 só os acentos em texto
  (`Pericia`, `Caracteristica`, `Ultima` → com acento; `pontos de Ignição` → `Ignição` já está; maiúscula de
  "Pontos" só se for grafia, não concordância).
- Antes de trocar uma palavra em arquivo embutido (lista do `CLAUDE.md`), confirmar que ela não é chave de seed
  nem de parser. Os snapshots congelados do livro (`LivroDeRegrasCongeladoTests`, `RulebookEquipagemCongeladoTests`)
  mudam só pelos acentos, de forma intencional e explicada.

## Restrições

- TDD; build com 0 warnings; projetos de teste um de cada vez.
- `rotas-da-api.txt` muda só pelo que B.2 move (o PUT de Maestria aparece para NPC e Criatura); B.1, B.6 e B.8 não
  mudam rotas, só status.
- Requisitos em `Docs/Requisitos/` atualizados onde a decisão muda o texto (B.1, B.2, B.6/B.8 se os requisitos
  citam status, B.7 na Ficha de Criaturas).
- Sem migration (nenhuma mudança de esquema).

## Deixado para o construtor

Forma da base comum das páginas; nome do fato de formato; como o auto-save preserva edições pendentes (fila,
reaplicação depois do recarregamento ou outra), desde que o teste do cenário do item A.4 passe.

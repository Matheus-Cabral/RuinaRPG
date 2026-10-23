# Cadastro inline de itens do Catálogo nas Fichas de NPC e Criatura — Design

## Contexto

A Ficha de Criatura já tem uma forma de "cadastrar itens": a seção Espólios (loot), além de
Combate (Arma/Armadura/Escudo por catálogo) e Artefatos — todas essas seções escolhem um item já
existente do Catálogo via `EntityPicker` (busca + seleção). A Ficha de NPC segue o mesmo padrão
(Combate, Inventário, Artefatos). O que não existe é uma forma de **criar** um item novo do
Catálogo sem sair da Ficha: hoje o único caminho é navegar até a página separada `/catalogo/novo`,
preencher o formulário lá, voltar pra Ficha e então buscar o item recém-criado no picker.

O GM frequentemente percebe que precisa de um item que ainda não existe justamente no meio de
montar uma Ficha de NPC/Criatura (equipando uma arma nova, definindo o loot de uma criatura nova).
Esse vaivém de página quebra o fluxo.

Não existe hoje, em lugar nenhum do app, um precedente de "criar um novo X" embutido dentro de um
picker/busca — todo picker (itens nas 3 Fichas, Traços nas 3 Fichas) é busca-e-seleciona puro.

## Objetivo

Nas seções de item das Fichas de NPC e Criatura (Arma, Armadura, Escudo, Inventário/Espólios,
Artefato), o GM pode cadastrar um item novo no Catálogo sem sair da Ficha, através de um botão
"+" ao lado do buscador que abre um formulário em um diálogo; ao salvar, o item novo já fica
selecionado no picker, como se tivesse sido buscado normalmente.

**Fora de escopo, deliberadamente:**
- Ficha de Personagem não é tocada — o pedido foi específico para NPC e Criatura.
- Nenhuma mudança de backend: `ItemsController.Create` já existe, já é `[Authorize(Roles = "GM")]`,
  e o formato dos campos por Tipo de item não muda.
- O botão "+" só aparece para o GM. Numa ficha concedida a um jogador (NPC/Criatura que o jogador
  possui e pode editar — ver `GrantedSheetAuthorization.CanEdit`), os campos de cadastro de item
  não aparecem — o jogador continua podendo apenas escolher itens já existentes no catálogo do seu
  GM, nunca criar um novo.

## Abordagens consideradas

**A — Escolhida: componente wrapper `CatalogoItemPicker`**, combinando o `EntityPicker` existente
com um diálogo inline que reaproveita o `CatalogoItemForm` já usado em `/catalogo/novo`. Zero
mudança de backend; troca mecânica nos pontos de uso atuais.

**B — Rejeitada: estender o próprio `EntityPicker`** com conceitos de catálogo (Tipo de item,
criação). `EntityPicker` é compartilhado também pelo picker de Traços nas 3 Fichas — misturar uma
responsabilidade específica de item num componente genérico de busca quebra a separação de
responsabilidades sem necessidade.

**C — Rejeitada: botão que abre `/catalogo/novo` em nova aba/navegação com retorno.** Não atende
o objetivo de "sem sair da Ficha" — o GM perde contexto da Ficha (e de qualquer edição não salva
em outros campos) e precisa voltar e buscar manualmente.

## Design

### 1. `EntityPicker.razor` — um método público novo, nenhuma mudança de comportamento existente

`EntityPicker` já tem uma seleção interna (`SelectAsync`, privado) que define `Value`, guarda o
rótulo pra exibição (`_selectedLabel`) e notifica `ValueChanged`. Hoje só é chamado quando o
próprio usuário escolhe algo no `MudAutocomplete` — não existe forma de um componente pai "aplicar"
uma seleção vinda de fora (por exemplo, o item que acabou de ser criado num diálogo).

Adiciona um método público equivalente (`SelectExternallyAsync(PickerOption option)`, mesma lógica
de `SelectAsync`), chamável via `@ref` por quem envolve o `EntityPicker`. Nenhum parâmetro
existente muda — os usos atuais (Personagem, os pickers de Traço nas 3 Fichas) continuam
inalterados.

### 2. `CatalogoItemForm.razor` — 2 parâmetros novos, opcionais

- `FixedTipo` (`string?`): quando definido, trava `_form.Tipo` nesse valor e esconde o seletor de
  Tipo (que hoje só aparece em modo de criação) — o contexto de onde o formulário foi aberto já
  sabe o tipo (ex.: a seção Arma sempre cria Tipo=Arma). Quando `null` (uso atual, página
  `/catalogo/novo` standalone), nada muda — o seletor de Tipo continua aparecendo normalmente.
  Espólios (Criatura), que aceita item de qualquer Tipo, passa `Tipo=null` pro picker — o diálogo
  então mostra o seletor de Tipo como hoje.
- `OnCreated` (`EventCallback<ItemResponse>`): quando tem um delegate associado, `CreateAsync()`
  invoca esse callback com o item recém-criado **no lugar de** `Navigation.NavigateTo("/catalogo")`.
  Sem delegate associado (uso atual da página standalone), o comportamento de navegar para
  `/catalogo` continua exatamente como hoje.

O restante do formulário (campos por Tipo, upload de imagem, validação, mensagem de erro via
`DismissibleAlert`) não muda — é o mesmo componente, só ganha esses 2 pontos de extensão.

Quando `FixedTipo` está definido, `<Breadcrumbs Items="@Crumbs" />` também não é renderizado —
dentro de um diálogo aberto no meio da edição de uma Ficha, uma trilha de navegação pra
"Painel"/"Catálogo de Itens" não faz sentido e arriscaria uma navegação de página inteira
acidental, abandonando a Ficha que o GM está editando. Sem `FixedTipo` (página standalone), o
breadcrumb continua aparecendo normalmente.

### 3. Novo `CatalogoItemPicker.razor` (`src/RuinaRPG.Client/Shared/`)

Substitui `EntityPicker` nos pontos de item das Fichas de NPC/Criatura (não nos pickers de Traço,
que continuam usando `EntityPicker` puro).

**Parâmetros** (mesmo contrato do `EntityPicker`, mais 2 novos — troca de tag direta nos pontos de
uso):
- `Value` / `ValueChanged` — repassados ao `EntityPicker` interno.
- `SearchItems` — repassado ao `EntityPicker` interno.
- `Placeholder` — repassado ao `EntityPicker` interno.
- `Tipo` (`string?`, novo): o Tipo de item desta seção (`"Arma"`, `"Armadura"`, `"Escudo"`,
  `"ItemGeral"`, `"Artefato"`), repassado como `FixedTipo` pro diálogo. `null` só para Espólios
  (Criatura), que aceita qualquer Tipo.
- `PodeCriar` (`bool`, novo): controla se o botão "+" aparece. As Fichas passam
  `PodeCriar="@_isGmCaller"` — flag que ambas as páginas já calculam
  (`authState.User.IsInRole("GM")`), então nenhuma lógica de permissão nova é necessária.

**Comportamento interno:**
```
<EntityPicker @ref="_picker" Value="Value" ValueChanged="ValueChanged"
              SearchItems="SearchItems" Placeholder="Placeholder" />
@if (PodeCriar)
{
    <MudIconButton Icon="@Icons.Material.Filled.Add" OnClick="@(() => _dialogOpen = true)"
                   title="Cadastrar novo item" />
}

<MudDialog @bind-Visible="_dialogOpen">
    <DialogContent>
        <CatalogoItemForm FixedTipo="@Tipo" OnCreated="HandleCreatedAsync" />
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _dialogOpen = false)">Cancelar</MudButton>
    </DialogActions>
</MudDialog>
```
(mesmo padrão de diálogo inline — `<MudDialog @bind-Visible="...">`, não `IDialogService` — já
usado em `FichaDePersonagem`/`ClickableImage`/`ClickableAvatarImage`, não introduz um padrão novo
de UI.)

`HandleCreatedAsync(ItemResponse created)`: fecha o diálogo (`_dialogOpen = false`) e chama
`_picker.SelectExternallyAsync(new PickerOption(created.Id, created.Nome))` — o item novo aparece
selecionado no `EntityPicker` interno exatamente como se tivesse sido buscado e escolhido
normalmente, disparando `ValueChanged` pra página-mãe sem que ela precise de nenhuma lógica nova.

### 4. Pontos de uso trocados

Em `FichaDeCriatura.razor`: Arma (`Tipo="Arma"`), Armadura (`Tipo="Armadura"`), Escudo
(`Tipo="Escudo"`), Espólios (`Tipo=null`), Artefato (`Tipo="Artefato"`) — todos com
`PodeCriar="@_isGmCaller"`.

Em `FichaDeNpc.razor`: Arma, Armadura, Escudo, Inventário (`Tipo="ItemGeral"`), Artefato — mesma
troca, mesmo `PodeCriar="@_isGmCaller"`.

Os pickers de Traço (Características) nas duas fichas **não** mudam — continuam `EntityPicker`
puro.

### Erros

Reaproveita o `_errorMessage`/`DismissibleAlert` que `CatalogoItemForm` já tem internamente —
nenhum tratamento de erro novo no `CatalogoItemPicker`. Se o POST falhar, a mensagem aparece
dentro do próprio diálogo, que permanece aberto.

### Segurança

Defesa em profundidade já existe nos dois lados: client-side, `PodeCriar` esconde o botão pra
quem não é `_isGmCaller` (inclusive um jogador dono de uma ficha concedida); server-side,
`ItemsController.Create` já é `[Authorize(Roles = "GM")]` independente do que o client mostra —
nenhuma mudança de autorização é necessária em nenhum dos dois lados.

### Testes

- `EntityPickerTests`: um novo caso cobrindo `SelectExternallyAsync` (mesmas asserções do teste
  existente de seleção via busca, mas disparado pelo novo método público).
- `CatalogoItemFormTests`: casos novos para `FixedTipo` (seletor de Tipo escondido, `_form.Tipo`
  pré-preenchido) e `OnCreated` (callback disparado no lugar de navegar, quando tem delegate
  associado); confirmar que o uso sem esses parâmetros (página standalone) continua idêntico.
- `CatalogoItemPickerTests` (novo arquivo): botão "+" aparece/some conforme `PodeCriar`; clicar
  abre o diálogo com o `Tipo` certo; criar com sucesso fecha o diálogo e propaga a seleção pro
  `ValueChanged` do componente pai; Cancelar fecha sem criar nada.
- Nenhuma mudança de teste de integração/backend — `ItemsController` não muda.

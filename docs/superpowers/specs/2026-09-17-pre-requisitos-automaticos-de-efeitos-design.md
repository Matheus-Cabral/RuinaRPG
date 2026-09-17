# Pré-requisitos Automáticos de Efeitos — Design

## Contexto

`AddEfeitoForm.razor` (componente compartilhado usado em `BancoDeMagiasForm.razor` e nas 3 Fichas
— Personagem, NPC, Criatura) filtra hoje o dropdown "Efeito" por dois critérios: Grau/Círculo da
Magia/Habilidade **e** pré-requisito já satisfeito (`_disponiveis = _todos.Where(Grau <=
Magia.Grau).Where(PreRequisitos.All(grupo => grupo.Any(nomesPresentes.Contains)))`). A maioria dos
51 Efeitos do catálogo exige "Duração" ou "Dano" como pré-requisito (ver
`docs/superpowers/plans/2026-09-14-custeio-automatico-de-efeitos.md`) — enquanto o jogador não
adicionou esse Efeito básico primeiro, a esmagadora maioria dos Efeitos nomeados fica invisível no
dropdown, sem nenhuma explicação na tela. O usuário relatou isso como "faltam vários efeitos" e
"não consigo colocar mais de um efeito que não seja dano, alcance ou área" — confirmado nesta
sessão (via reprodução direta contra o catálogo real de 51 linhas na stack local e um teste bUnit)
que **não é um bug de filtragem** — a lógica de Grau e de grupo AND-of-OR já funciona como
projetada — mas sim uma experiência confusa: o jogador precisa saber, sem nenhuma pista na tela,
que precisa ir adicionar "Duração" (ou "Dano"/"Alcance") manualmente antes de conseguir ver os
Efeitos que realmente quer.

**Decisão do usuário**: parar de esconder Efeitos por pré-requisito não satisfeito. Ao confirmar um
Efeito cujo pré-requisito ainda falta, adicionar esse pré-requisito automaticamente (com
Quantidade/Custo editável depois), em vez de forçar o jogador a fazer isso manualmente antes.

**Efeito colateral necessário**: como agora uma linha pode nascer com Quantidade "pendente" (ver
Parte 2), a tabela de Efeitos já adicionados — hoje somente leitura, com um botão "Remover" — passa
a permitir editar Quantidade (e Custo, para os tipos Manual/ManualPorUnidade) de **qualquer**
linha, não só as adicionadas automaticamente, com o Custo em PI recalculado ao vivo. Essa tabela
está hoje duplicada, quase byte-a-byte, em 4 arquivos (`BancoDeMagiasForm.razor`,
`FichaDePersonagem.razor`, `FichaDeNpc.razor`, `FichaDeCriatura.razor`); esta mudança extrai um
componente compartilhado `EfeitosTable.razor` para as 4, evitando quadruplicar a lógica de edição e
recálculo em cascata.

## Fora de escopo

- Nenhuma mudança de schema — tudo já persiste como `EfeitoNome`/`Quantidade`/`CustoPI` por linha,
  igual hoje.
- Nenhuma mudança na validação server-side (`EfeitoValidator`) — ela já rejeita uma submissão cuja
  Quantidade/Custo final não bata com o catálogo; isso é o suficiente para pegar uma linha
  "pendente" (Quantidade 0) que o jogador esqueceu de preencher, ao tentar salvar (0 é um valor
  internamente consistente — `2 PI * 0 = 0 PI` — então não é rejeitado sozinho; é o próprio
  resultado nonsense, 0 Dados de Duração por exemplo, que o jogador precisa perceber e corrigir).
- Remover uma linha da qual outra linha depende (ex: remover "Dano" com "Dreno de Vitalidade"
  ainda presente) continua permitido livremente, sem bloqueio nem remoção em cascata — igual hoje,
  o servidor pega isso na submissão final.
- Ficha de Criatura's Combate/Runas (que também usa Grau/pré-requisito, mas para Runas, não
  Efeitos de Magia/Habilidade) — fora de escopo, não usa `AddEfeitoForm`.

## Parte 1 — Resolução automática de pré-requisitos

Novo tipo puro em `RuinaRPG.Domain.SpellsAndAbilities` (mesma pasta de `EfeitoValidator`/
`EfeitoCustoCalculator`, zero I/O):

```csharp
public sealed record EfeitoResolutionResult(
    IReadOnlyList<EfeitoRegra> ParaAutoAdicionar,   // em ordem de dependência (o mais "básico" primeiro)
    IReadOnlyList<string>? GrupoAmbiguo);            // null quando a resolução terminou; senão, os nomes elegíveis a perguntar

public static class EfeitoPrerequisiteResolver
{
    public static EfeitoResolutionResult Resolver(
        EfeitoRegra alvo,
        IReadOnlyList<EfeitoRegra> catalogo,
        IReadOnlySet<string> nomesPresentes,
        int grauDaMagia,
        IReadOnlyDictionary<string, string> escolhasForcadas); // chave: grupo serializado (string.Join("|", grupo)); valor: nome escolhido
}
```

Percorre `alvo.PreRequisitos` (grupos AND-of-OR, igual `EfeitoValidator` já usa). Para cada grupo
ainda não satisfeito por `nomesPresentes` ∪ o que já foi decidido nesta chamada:

1. Filtra os candidatos do grupo pelos elegíveis por Grau (`candidato.Grau <= grauDaMagia`) — só
   assim "Detrito" (Grau 4) não tenta sugerir "Atordoamento" (Grau 8) numa Magia de Grau 4.
2. **1 candidato elegível** → resolve sozinho.
3. **Nenhum candidato elegível** → inconsistência de catálogo que não deveria acontecer com os
   dados atuais (o próprio Grau do `alvo` sempre garante ao menos 1 candidato elegível nos grupos
   ambíguos existentes hoje — ver "Detrito" abaixo); retorna o grupo vazio como ambíguo, e a UI
   trata "0 opções" como um erro a exibir, não como uma escolha pra fazer.
4. **>1 candidato elegível** → se `escolhasForcadas` já tem uma resposta pra esse grupo (o
   chamador perguntou e a UI reenviou), usa ela; senão, para a resolução aqui e devolve
   `GrupoAmbiguo` com os elegíveis — o chamador (o componente) precisa perguntar ao jogador.
5. Antes de adicionar o candidato escolhido à lista `ParaAutoAdicionar`, resolve **recursivamente**
   os pré-requisitos do próprio candidato (ex: "Congelar", candidato de Detrito, também exige
   "Duração" — se "Duração" também estiver faltando, ela entra na lista *antes* de "Congelar").

Hoje, entre as 51 linhas do catálogo, **só "Detrito"** tem um grupo com mais de 1 candidato
elegível possível (`["Atordoamento", "Congelar", "Enraizar"]`, Grau 4 do próprio Detrito garante
que ao menos Enraizar(G2)/Congelar(G3) sempre están elegíveis quando Detrito é selecionável). Todo
outro grupo do catálogo tem exatamente 1 item — resolve sempre sem perguntar nada.

**Em `AddEfeitoForm.razor`**:

- `_disponiveis` para de filtrar por `PreRequisitos` — só filtra por Grau, igual já faz hoje pro
  Grau. Mostra os 51 Efeitos (ou quantos couberem no Grau atual) sempre.
- Ao clicar "Adicionar Efeito" (depois de já preencher Quantidade/Custo do Efeito selecionado,
  igual hoje): chama `EfeitoPrerequisiteResolver.Resolver` com o Efeito selecionado como alvo.
  - Se voltar `GrupoAmbiguo` não-nulo: mostra um pequeno seletor inline ("`Detrito` exige um
    destes já presente: escolha um") com os nomes elegíveis; ao escolher, acumula a resposta em
    `escolhasForcadas` e chama `Resolver` de novo (pode, em teoria, encontrar outro grupo ambíguo
    — o loop continua até resolver por completo; hoje isso nunca acontece de fato, só um grupo
    ambíguo existe no catálogo inteiro).
  - Quando resolvido (`GrupoAmbiguo == null`): monta a lista final — cada `EfeitoRegra` de
    `ParaAutoAdicionar` vira uma linha com Quantidade inicial `0` se `TipoDeCusto` for
    `PorUnidade`/`ManualPorUnidade` (Custo em PI = 0, "pendente" até o jogador editar — ver Parte
    2) ou a linha já completa se for `Fixo` (Custo em PI = `CustoFixo`, sem Quantidade); a linha do
    Efeito originalmente selecionado (com a Quantidade/Custo que o jogador já preencheu) vai por
    último. Emite a lista inteira de uma vez.

`OnAdicionar` muda de `EventCallback<EfeitoAdicionadoResult>` (uma linha) para
`EventCallback<List<EfeitoAdicionadoResult>>` (o lote inteiro desta confirmação — prováveis
pré-requisitos + o Efeito escolhido). As 4 páginas chamadoras trocam `_efeitos.Add(...)` por
`_efeitos.AddRange(...)` — mudança mecânica, um `EfeitoAdicionadoResult` por linha igual antes.

## Parte 2 — `EfeitosTable.razor` (novo componente compartilhado)

Substitui o bloco `<MudSimpleTable>` hoje duplicado nas 4 páginas (cabeçalho: Nome do Efeito /
Quantidade / Custo em PI / [Remover]). Recebe:

```csharp
[Parameter, EditorRequired] public List<EfeitoLinha> Efeitos { get; set; }  // mutado in-place
[Parameter, EditorRequired] public int Grau { get; set; }                   // Grau da Magia/Habilidade, pra recalcular Custo
[Parameter] public EventCallback OnChanged { get; set; }                    // disparado após qualquer edição/remoção (autosave etc.)
```

`EfeitoLinha` (novo tipo compartilhado em `RuinaRPG.Client.Shared.Fields`, substitui as 4 classes
`EffectFormModel`/`SpellAbilityEffectFormModel` hoje quase idênticas): `EfeitoNome`, `Quantidade`
(`int?`), `CustoPI` (`int`).

Busca o catálogo (`GET efeitos`) uma vez, do mesmo jeito que `AddEfeitoForm` já faz (fetch próprio,
cacheado em `_todos` — sem elevar isso a um estado compartilhado entre os dois componentes; catálogo
tem 51 linhas, buscar duas vezes é irrelevante).

Por linha, o comportamento depende do `TipoDeCusto` do catálogo pra esse Nome:

- **`PorUnidade`**: Quantidade editável (`MudNumericField<int?>`); Custo em PI é só texto,
  recalculado ao vivo (ver cascata abaixo).
- **`Fixo`**: nem Quantidade nem Custo são editáveis (ambos fixos pelo catálogo) — "—" na coluna
  Quantidade, Custo em PI mostrado como texto.
- **`DerivadoDeOutroEfeito`**: nem Quantidade nem Custo são editáveis diretamente nesta linha — os
  dois só mudam como efeito colateral de editar a linha de origem (ver cascata abaixo).
- **`Manual` / `ManualPorUnidade`**: **nenhum recálculo automático** — igual
  `EfeitoValidator.cs` já comenta hoje ("o valor do Mestre É a entrada, não algo a recalcular").
  Quantidade (só pra `ManualPorUnidade`) e Custo em PI ficam os dois diretamente editáveis, sem
  nenhuma fórmula ligando um ao outro — o catálogo não guarda o valor-por-unidade que o Mestre
  digitou ao adicionar (só o `CustoPI` total daquela linha já persiste), então não há como
  reconstruir uma proporção pra recalcular depois. Isso replica exatamente como `AddEfeitoForm` já
  trata esses dois tipos na hora de adicionar (campo de Custo digitado à mão, sem cálculo).
- Botão "Remover" — comportamento inalterado, em toda linha.

**Recálculo ao editar Quantidade de uma linha `PorUnidade`** (`@bind-Value:after` no campo):
recomputa o `CustoPI` da própria linha via `EfeitoCustoCalculator.Calcular` (mesma função que
`AddEfeitoForm` já usa) usando o catálogo + `Grau`; depois, procura entre as OUTRAS linhas alguma
cujo `QuantidadeDerivadaDeEfeito` (do catálogo) aponte pro Nome da linha editada, e recalcula a(s)
linha(s) `DerivadoDeOutroEfeito` dependente(s) também (ex: editar a Quantidade de "Dano" atualiza o
Custo de "Dreno de Vitalidade" na mesma tabela, se presente). Sem essa cascata, o Custo de uma
linha derivada ficaria stale até o jogador reabrir/readicionar — o servidor pegaria isso como
"Custo em PI incorreto" só na hora de salvar, tarde demais pra ser uma boa experiência.

Cada página troca seu bloco `<MudSimpleTable>` por `<EfeitosTable Efeitos="..." Grau="..."
OnChanged="..." />`, e sua classe local `EffectFormModel`/`SpellAbilityEffectFormModel` por
`EfeitoLinha`.

## Testes

- `EfeitoPrerequisiteResolverTests` (Domain/Unit): grupo com 1 candidato resolve sozinho; grupo com
  >1 candidato elegível volta `GrupoAmbiguo`; grupo com candidatos parcialmente inelegíveis por
  Grau filtra corretamente; resolução recursiva (candidato escolhido que também tem pré-requisito
  não satisfeito) devolve a lista na ordem certa (pré-requisito do candidato antes do candidato);
  nada a resolver quando tudo já está presente (`ParaAutoAdicionar` vazio).
- `AddEfeitoFormTests` (Client): confirmar um Efeito sem seu pré-requisito presente emite o lote
  completo (pré-requisito + alvo) em uma chamada de `OnAdicionar`; confirmar "Detrito" sem nenhum
  dos 3 candidatos presente mostra o seletor ambíguo em vez de confirmar direto; escolher uma opção
  do seletor completa a resolução e emite o lote.
- `EfeitosTableTests` (Client, novo): editar Quantidade de uma linha `PorUnidade` recalcula seu
  Custo; editar a Quantidade de uma linha da qual outra deriva (`QuantidadeDerivadaDeEfeito`)
  recalcula a dependente também; uma linha `Fixo` não mostra nenhum campo editável; uma linha
  `Manual`/`ManualPorUnidade` mostra Custo (e Quantidade, só pra `ManualPorUnidade`) editáveis sem
  nenhum recálculo entre os dois; Remover continua funcionando em qualquer tipo.
- Batch de regressão nas 4 páginas (`BancoDeMagiasFormTests` já existe; as 3 Fichas não têm teste
  de página dedicado hoje — não é escopo desta mudança criar um do zero pra cada, mas confirmar via
  `dotnet build` que a troca mecânica de tipo/chamada compila e via leitura cuidadosa do diff que
  as 4 ficaram idênticas entre si, igual o padrão já estabelecido nas mudanças anteriores deste
  componente).

# Automatizar Afinidades — Design

## Contexto

A ficha tem dois campos distintos chamados "Afinidade":

- **1.a Afinidade**: um dropdown único (`AfinidadeElemental`, 18 valores — os 4 Elementos e os 14
  Sub-Elementos da Matriz Elemental) representando a afinidade elemental "principal" do
  personagem. Hoje é puramente cosmético — nenhum calculador em `src/RuinaRPG.Domain` lê esse
  valor pra nada.
- **2.c Afinidades**: uma lista incremental (`CharacterAffinity`/`NpcAffinity` — Elemento, Valor do
  Elemento, Sub-Elemento, Valor do Sub-Elemento, Caminho, Experiência). `Requisitos - Ficha de
  Personagem.md` já diz hoje, de propósito, que o Valor "não tem relação de cálculo com os demais
  campos da linha" — deixado assim à espera da regra que falta.

Essa regra que falta é **Eficiência Elemental** e **Dano Elemental** (2.b, Sub-Atributos):
`Docs/Sistema RPG/Formulas.md` diz que ambos "vêm de uma Tabela", mas essa tabela nunca foi escrita
em lugar nenhum do sistema — `Requisitos - Ficha de Personagem.md:115` já registra isso como
lacuna conhecida ("Campos previstos porém não implementados até a tabela existir").

**Fora de escopo, deliberadamente — Ficha de Criatura**: `Requisitos - Ficha de Criaturas.md`
diz explicitamente que "Eficiência Elemental e Dano Elemental não aparecem na Ficha de Criatura" e
que "2.c Afinidades não existe na Ficha de Criatura — a Criatura tem apenas o campo único
*Afinidade* em 1.a". Criatura também não tem *Vocação* (tem *Arquétipo*, Físico/Arcano, que não
participa de nada abaixo). Este design cobre **Personagem e NPC apenas**; nenhuma mudança na Ficha
de Criatura.

**Duas estruturas de imagem, dois papéis diferentes** (ambas em `Docs/Sistema RPG/`, referenciadas
como material de apoio sem seção própria, per `Requisitos - Livro de Regras.md:30`):

- **Matriz Elemental** (`Matriz_Elemental.png`) — já totalmente implementada em código
  (`ElementoSubElementoValidator.IsValidCombination`): define quais pares Elemento+Sub-Elemento
  são combináveis numa mesma linha de 2.c. Nada muda aqui.
- **Escolas de Magia** (`Escolas_de_Magia.png`) — agrupa os 18 valores em 4 escolas temáticas,
  **nunca implementada**: Dobra (Ar, Água, Fogo, Terra), Transmutação (Flora, Ferro, Raio, Gelo),
  Maculação (Necromancia, Invocação, Ecomancia, Hemomancia), Consagração (Curar, Aprimorar,
  Prever, Purificar). É a base da restrição por Vocação abaixo (confirmado com o usuário, que
  também deu o mapeamento Vocação→Escola, ausente em qualquer documento).

## Objetivo

1. **Restringir por Vocação** quais Elementos/Sub-Elementos podem ser escolhidos em 1.a e 2.c,
   via Escola de Magia.
2. **Impedir duas linhas de 2.c** com o mesmo Elemento não-nulo ou o mesmo Sub-Elemento não-nulo
   no mesmo Personagem/NPC.
3. **Calcular e exibir** Eficiência Elemental e Dano Elemental como Sub-Atributos automáticos.

## Parte 1 — Restrição por Vocação (Escola de Magia)

**Mapeamento Vocação → Escola(s)** (dado pelo usuário, sem equivalente em nenhum documento):

| Vocação | Escolas liberadas |
|---|---|
| Feiticeiro | Dobra, Maculação |
| Adepto | Dobra, Consagração |
| Bruxo | Dobra, Transmutação |
| Campeão, Caçador, ou Vocação nula | nenhuma |

Todo Elemento (Ar/Água/Fogo/Terra) pertence a Dobra — as 3 Vocações mágicas compartilham acesso a
todos os 4 Elementos-base; o que muda entre elas são os Sub-Elementos (cada Escola de
Sub-Elemento é exclusiva de uma única Vocação mágica). Campeão/Caçador não têm Escola nenhuma —
não escolhem Elemento nem Sub-Elemento algum, em 1.a nem em 2.c.

**Onde a restrição vale**: 1.a (dropdown único) e 2.c (Elemento e Sub-Elemento de cada linha),
em Personagem e NPC. Cliente filtra as opções oferecidas pela Vocação atual; servidor valida na
gravação (nunca confia só no filtro do cliente).

**Regra de gravação — só bloqueia escolha NOVA, nunca invalida dado antigo** (decisão explícita do
usuário): tanto 1.a quanto cada linha de 2.c são submetidos como formulário completo (todo o
`UpdateCharacterSheetRequest`/`UpdateCharacterAffinityRequest` chega de uma vez, mesmo padrão de
autosave já usado em toda a ficha) — então validar cegamente "o valor enviado precisa estar
liberado pela Vocação enviada" rejeitaria uma gravação inocente onde só a Vocação mudou e o
Elemento/Sub-Elemento antigo (já salvo, de uma Vocação anterior) foi reenviado sem alteração. A
validação correta é: **comparar o valor recebido contra o valor já salvo no banco antes de
validar** — se for igual ao que já estava lá, pula a checagem de Escola (grandfathered); só
valida contra a Vocação quando o valor realmente muda para algo diferente do que já estava salvo.
Isso vale nos dois lugares (1.a em `CharacterSheetsController.Update`/`NpcSheetsController.Update`;
2.c em `CharacterAffinitiesController.Update`/`NpcAffinitiesController.Update` — `Add` não tem
"valor antigo", então toda escolha nova numa linha recém-criada é validada normalmente).

**Novos tipos (Domain, puros, sem I/O)**:
```
public enum EscolaDeMagia { Dobra, Transmutacao, Maculacao, Consagracao }

public static class EscolaDeMagiaCatalog
{
    public static EscolaDeMagia DoElemento(Elemento elemento) => EscolaDeMagia.Dobra;

    public static EscolaDeMagia DoSubElemento(SubElemento subElemento) => subElemento switch
    {
        SubElemento.Gelo or SubElemento.Raio or SubElemento.Flora or SubElemento.Ferro => EscolaDeMagia.Transmutacao,
        SubElemento.Necromancia or SubElemento.Invocacao or SubElemento.Ecomancia or SubElemento.Hemomancia => EscolaDeMagia.Maculacao,
        SubElemento.Curar or SubElemento.Aprimorar or SubElemento.Prever or SubElemento.Purificar => EscolaDeMagia.Consagracao,
        _ => throw new ArgumentOutOfRangeException(nameof(subElemento)),
    };
}

public static class VocacaoEscolaMap
{
    private static readonly Dictionary<Vocacao, EscolaDeMagia[]> Escolas = new()
    {
        [Vocacao.Feiticeiro] = [EscolaDeMagia.Dobra, EscolaDeMagia.Maculacao],
        [Vocacao.Adepto] = [EscolaDeMagia.Dobra, EscolaDeMagia.Consagracao],
        [Vocacao.Bruxo] = [EscolaDeMagia.Dobra, EscolaDeMagia.Transmutacao],
    };

    public static bool PodeEscolherElemento(Vocacao? vocacao, Elemento elemento) =>
        vocacao is { } v && Escolas.TryGetValue(v, out var escolas) && escolas.Contains(EscolaDeMagiaCatalog.DoElemento(elemento));

    public static bool PodeEscolherSubElemento(Vocacao? vocacao, SubElemento subElemento) =>
        vocacao is { } v && Escolas.TryGetValue(v, out var escolas) && escolas.Contains(EscolaDeMagiaCatalog.DoSubElemento(subElemento));

    public static bool PodeEscolherAfinidade(Vocacao? vocacao, AfinidadeElemental afinidade) =>
        // AfinidadeElemental compartilha os mesmos nomes de membro que Elemento/SubElemento —
        // resolve pra qual dos dois enums o valor pertence antes de checar a Escola.
        Enum.TryParse<Elemento>(afinidade.ToString(), out var elemento)
            ? PodeEscolherElemento(vocacao, elemento)
            : PodeEscolherSubElemento(vocacao, Enum.Parse<SubElemento>(afinidade.ToString()));
}
```

**Client**: mesmo padrão já usado pra Variante-por-Linhagem/Sub-Vocação-por-Vocação (dicionários
hardcoded em `@code`, sem infraestrutura de rules-data-provider — dataset pequeno e estável,
mesma justificativa já aceita nessas duas cascatas existentes). O dropdown de 1.a e os dropdowns
de Elemento/Sub-Elemento de cada linha de 2.c passam a filtrar pela Vocação atual da ficha.

## Parte 2 — Duplicata de Elemento/Sub-Elemento entre linhas de 2.c

No `Add`/`Update` de uma linha de Afinidade, rejeitar (400) se outra linha do mesmo
Personagem/NPC já tiver o mesmo Elemento não-nulo, ou o mesmo Sub-Elemento não-nulo — mesmo lugar
onde a Matriz Elemental já é validada (`CharacterAffinitiesController`/`NpcAffinitiesController`,
ao lado de `ElementoSubElementoValidator.IsValidCombination`). Como a Escola de Magia já impede um
Elemento/Sub-Elemento indevido, e essa checagem impede repetição do mesmo valor, uma ficha nunca
tem mais de uma linha "candidata" pro mesmo Elemento/Sub-Elemento escolhido em 1.a — resolvendo de
saída a pergunta de "qual linha conta" da Parte 3, abaixo.

## Parte 3 — Eficiência Elemental e Dano Elemental

**Fórmula**: `EficienciaElemental = DanoElemental = Valor` da linha de 2.c cujo Elemento OU
Sub-Elemento bate com o valor escolhido em 1.a (1:1 por ora — cada um vira sua própria função no
calculador, não uma referência direta ao mesmo número, porque a proporção pode divergir no
futuro). Sem 1.a preenchido, ou sem uma linha correspondente em 2.c, ambos valem **0** (mesma
convenção "quando NULL assume 0" do resto da ficha).

**Efeito mecânico** (confirmado com o usuário): Dano Elemental é um bônus de dano exibido —
mesmo tratamento que "Modificador de Dano" (Cortante/Perfurante/Contundente/Mágico) já recebe hoje
(`FichaDePersonagem.razor`, Seção "Modificador de Dano"): um número mostrado na ficha, aplicado
manualmente pelo jogador ao narrar um ataque elemental — o app não simula rolagem de dano nem tem
um conceito de "Magia/Efeito X é do elemento Y", então não há integração automática com Efeitos ou
Magias. Eficiência Elemental reduz o Custo em Arcana/Foco de Magias elementais pelo mesmo
raciocínio — exibido, aplicado manualmente, sem integração com `SpellAbilityCostCalculator`.

**Domain** (`SubAttributeFormulas`, mesmo arquivo/padrão das fórmulas de Sub-Atributo já
existentes):
```
public static int EficienciaElemental(int valorDaAfinidadeCorrespondente) => valorDaAfinidadeCorrespondente;
public static int DanoElemental(int valorDaAfinidadeCorrespondente) => valorDaAfinidadeCorrespondente;
```
Mais uma função de resolução (dado o `Afinidade` da ficha e a lista de linhas de 2.c, encontra o
Valor correspondente ou 0):
```
public static int ValorDaAfinidadeCorrespondente(AfinidadeElemental? afinidade, IReadOnlyList<CharacterAffinity /* ou NpcAffinity */> linhas)
```

**`SubAttributesResponse`** (hoje compartilhado pelos 3 controllers de ficha) ganha
`EficienciaElemental`/`DanoElemental` como `int?` — preenchidos em Personagem/NPC, `null` em
Criatura (mesmo padrão de campo "às vezes aplicável" que `PesoAtual`/`PesoMaximo` já usam nesse
mesmo tipo; `CreatureSheetsController` passa `null` nos dois).

**Client**: exibidos ao lado dos Sub-Atributos existentes em `FichaDePersonagem.razor` e
`FichaDeNpc.razor`, só quando não-nulos (`FichaDeCriatura.razor` não muda).

## Docs a atualizar

- `Docs/Sistema RPG/Formulas.md`: `Eficiência elemental = Tabela` / `Dano elemental = Tabela` →
  fórmula real.
- `Docs/Requisitos/Requisitos - Ficha de Personagem.md`:
  - 2.b: remove o texto "campos previstos porém não implementados até a tabela existir", descreve
    a fórmula real.
  - 2.c: acrescenta a regra de duplicata (Parte 2) e a restrição por Vocação/Escola (Parte 1),
    com uma tabela ou lista explicando a Escola de Magia (já que ela nunca teve texto em nenhum
    documento do sistema até agora — vale documentá-la aqui, já que é a fonte usada por este
    requisito, e não há um lugar mais natural em `Docs/Sistema RPG/` sem imagem-pra-texto virar
    escopo à parte).
  - 1.a: acrescenta a restrição por Vocação ao campo Afinidade.
- NPC não muda (herda por silêncio, mesmo padrão já usado em toda a ficha).

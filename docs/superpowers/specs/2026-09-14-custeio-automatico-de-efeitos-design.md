# Custeio automático de Efeitos (Magias/Habilidades) — Design

## Contexto

Toda Magia/Habilidade (Banco de Magias do GM, e as 3 fichas — Personagem, NPC, Criatura) tem uma
lista de Efeitos (`SpellAbilityEffect`/`CharacterSpellAbilityEffect`/`NpcSpellAbilityEffect`/
`CreatureSpellAbilityEffect`/`SpellAbilityBankEffect` — mesmo shape em todo lugar: `EfeitoNome`
string livre, `Quantidade` opcional, `CustoPI` digitado à mão). `SpellAbilityCostCalculator` já
soma esses `CustoPI` e converte pra Foco (`Custo = teto(GastoEmPI × 1,25)`) — isso já funciona.
O que não existe é validação: `EfeitoNome` aceita qualquer texto, `CustoPI` aceita qualquer
número, nada confere contra "[[GRAUS & CÍRCULOS]]".

`GraduacaoEfeitoParser` já lê esse documento (Nome, Grau, Descrição, texto cru de `Gasto`, flag
de pré-requisito), mas só alimenta a busca do Compêndio — nenhum controller usa esses dados hoje.
O texto de `Gasto` é irregular demais pra virar uma fórmula por regex genérico de forma confiável
(custo fixo, por unidade, por unidade com teto que escala por Grau, teto com contagem relativa
começando num Grau específico, e pelo menos 4 efeitos com custo "Variável"/"X PI" — decisão do
Mestre, não uma fórmula). Este design não estende esse parser — cria um catálogo novo,
hand-authored, com os dados já extraídos e estruturados manualmente (mesmo método de
`RacialTraitLookup`/`RacialAbilityLookup`: fatos extraídos à mão da prosa do sistema, não um
parser genérico).

**Levantamento completo do documento** (45 efeitos nomeados em 9 graus, mais os 3 efeitos
básicos) encontrou problemas reais nele, resolvidos em conversa com o usuário:
- "Selar" é citado como pré-requisito alternativo do Detrito mas não existe como efeito definido
  em lugar nenhum — **decisão: o catálogo nasce sem ele; o Auditor de Regras cria quando/se for
  definido**, mesmo mecanismo que já resolve esse tipo de lacuna pra Características (R0003 dessa
  mesma auditoria).
- "Congelar" (Grau 3) está sem o `##` de cabeçalho no markdown — cabeçalho corrigido como parte
  deste trabalho (não afeta o catálogo novo, que é hand-authored, mas conserta a busca do
  Compêndio, que ainda depende do parser antigo).
- Os tetos de Dano/Alcance (efeitos básicos) vêm da tabela do topo do documento (Grau → máx.
  dados/pés), não do corpo de cada efeito — incluída no catálogo.

## Objetivo

1. `CustoPI` de cada Efeito adicionado a uma Magia/Habilidade é calculado automaticamente a
   partir de uma fórmula estruturada por efeito — exceto os poucos que o próprio livro deixa a
   critério do Mestre (esses ganham um campo numérico manual, um por caso).
2. Nas 4 telas que adicionam Efeito (Banco de Magias + as 3 fichas), o campo Nome vira uma
   seleção — não mais texto livre — restrita aos efeitos já desbloqueados pelo Grau da
   Magia/Habilidade **e** bloqueada até os pré-requisitos (incluindo grupos "OU") já estarem na
   mesma lista.
3. O catálogo de Efeitos é editável pelo Auditor de Regras (nova página), mesmo padrão de
   Características — nasce com um seed hand-authored, sobrevive a reinicializações, e o Auditor
   pode corrigir/adicionar (ex.: "Selar", quando definido).

## Modelo de dados

Nova tabela, catálogo global (mesmo modelo de `Trait`/`CreatureExclusiveTrait` — não é biblioteca
por-GM):

```
Efeito
  Id                            Guid PK
  Nome                          string
  Grau                          int (1-9) — grau/círculo em que é desbloqueado; cumulativo
                                 (disponível em qualquer Magia/Habilidade de Grau >= este)
  Descricao                     string
  TipoDeCusto                   enum: Fixo | PorUnidade | Manual | ManualPorUnidade |
                                 DerivadoDeOutroEfeito
  CustoFixo                     int?   — usado quando TipoDeCusto = Fixo
  CustoPorUnidade                int?  — usado quando TipoDeCusto = PorUnidade ou
                                 DerivadoDeOutroEfeito (nesses dois, a Quantidade nunca é digitada
                                 pelo usuário: PorUnidade usa o campo Quantidade do próprio
                                 formulário; DerivadoDeOutroEfeito lê a Quantidade de outro Efeito
                                 já presente na mesma lista — ver abaixo)
  UnidadeLabel                  string? — rótulo da unidade pro campo Quantidade (ex.: "Dado",
                                 "Ponto de Redução", "Ação", "5% de Taxa Crítica"); null quando o
                                 efeito não tem Quantidade (Fixo, Manual)
  QuantidadeDerivadaDeEfeito    string? — só para TipoDeCusto = DerivadoDeOutroEfeito: o Nome do
                                 outro Efeito (na mesma lista) cuja Quantidade este efeito espelha
                                 (ex.: Dreno de Vitalidade/Dreno de Arcana → "Dano")
  MaxUnidades                   int?   — teto de Quantidade; null = sem teto
  MaxEscalaPorGrau              bool   — true: o teto é MaxUnidades × multiplicador (ver abaixo);
                                 false: MaxUnidades é um teto fixo, não escala
  MaxContandoAPartirDoGrau      int?   — só relevante quando MaxEscalaPorGrau=true: se definido, o
                                 multiplicador é (GrauDaMagia − este valor + 1) em vez do Grau
                                 bruto da Magia (ex.: Absorção conta a partir do 7º grau)
  CustoAlternativo              int?   — exceção tipo Encantamento Elemental: quando definido,
                                 substitui CustoFixo/CustoPorUnidade inteiramente
  CustoAlternativoAPartirDoGrau int?   — o Grau da Magia/Habilidade a partir do qual
                                 CustoAlternativo passa a valer (null = sem exceção)
  PreRequisitosJson             string? — `List<List<string>>` serializado: cada sublista é um
                                 grupo "OU" (satisfeito se qualquer um dos Nomes estiver na mesma
                                 lista de Efeitos), todos os grupos precisam estar satisfeitos
                                 ("E" entre grupos). Ex. Detrito: `[["Duração"],["Área"],
                                 ["Atordoamento","Congelar","Enraizar"]]`. Mesmo padrão de
                                 `RacialTraitOverride.GratuitaOptionsJson` (JSON num campo string,
                                 não uma tabela filha) — precedente já estabelecido neste código.
  IsCustomized                  bool   — mesma função de `Trait.IsCustomized`: uma vez editado
                                 pelo Auditor, o seed nunca mais sobrescreve essa linha
  IsDeleted                     bool   — soft delete, mesma razão de `Trait.IsDeleted`
  UpdatedByUserId               Guid?
  UpdatedAt                     DateTime?
```

`EfeitoSeeder` (mesmo padrão de `TraitSeeder`, chamado no startup): faz upsert de ~49 linhas
hand-authored (a tabela completa abaixo) por `Nome`, pulando qualquer linha já `IsCustomized`.

## O catálogo completo (seed data)

*Grau*: quando um efeito é desbloqueado. *Unidade*: rótulo da Quantidade (`—` = sem Quantidade).
*Teto*: `—` sem teto; `N` fixo; `N×Grau` escala com o Grau da Magia; `N×(Grau−G+1) a partir de G`
escala contando a partir do Grau G. *Pré-requisito*: `E` entre grupos, `OU` dentro de um grupo.

| Nome | Grau | Custo | Unidade | Teto | Pré-requisito |
|---|---|---|---|---|---|
| Dano *(básico)* | 1 | 2 PI/un | Dado | conforme tabela de Grau (ver nota) | — |
| Alcance *(básico)* | 1 | 3 PI/un | Pé | conforme tabela de Grau (ver nota) | — |
| Duração *(básico)* | 1 | 4 PI/un | Dado | — | — |
| Aumentar Armadura | 1 | 2 PI/un | Ponto de Redução | 5×Grau | Duração |
| Aumentar Atributo | 1 | 2 PI/un | Ponto de Atributo | 2×Grau | Duração |
| Contrato Mágico | 1 | 3 PI fixo | — | — | — |
| Cura | 1 | 2 PI fixo | — | — | Dano |
| Deslocamento | 1 | 1 PI fixo | — | — | Alcance |
| Miragem | 1 | 1 PI/un | Sentido | 3 (fixo) | Duração |
| Regeneração | 1 | 3 PI fixo | — | — | Duração |
| Aceleração | 2 | 3 PI fixo | — | — | Duração |
| Amedrontar | 2 | 3 PI fixo | — | — | Duração |
| Diminuir Atributo | 2 | 2 PI/un | Ponto de Atributo | 2×Grau | Duração |
| Encantamento Elemental | 2 | 2 PI fixo (4 PI a partir do Grau 4) | — | — | Duração E Dano |
| Enraizar | 2 | 2 PI fixo | — | — | Duração |
| Envenenar | 2 | 4 PI fixo | — | — | Duração |
| Libra (Vitalidade) | 2 | 4 PI fixo | — | — | Alcance E Duração |
| Libra (Arcana) | 2 | 6 PI fixo | — | — | Alcance E Duração |
| Área | 3 | 4 PI/un | Anel | 1×Grau | — |
| Armadura Arcana | 3 | 3 PI/un | Ponto de Redução | 5×Grau | Duração |
| Ato Múltiplo | 3 | manual/un | Dado | 1×(Grau−2) a partir do Grau 3 | — |
| Aumentar Max. Vitalidade | 3 | 2 PI/un | Dado | 2×Grau | Duração |
| Congelar | 3 | 4 PI fixo | — | — | Duração |
| Dreno de Vitalidade | 3 | 2 PI/un (derivado) | Dado | = Quantidade de Dano | Dano |
| Reflexão | 3 | 4 PI fixo | — | — | Dano E Duração |
| Proteção | 3 | 3 PI fixo | — | — | Dano E Duração |
| Provocar | 3 | 3 PI fixo | — | — | Duração |
| Marionete Arcana | 3 | 5 PI fixo | — | — | Duração |
| Abençoar | 4 | 3 PI fixo | — | — | Duração |
| Aumentar Max. Arcana | 4 | 3 PI/un | Dado | 2×Grau | Duração |
| Confusão | 4 | 3 PI fixo | — | — | Duração |
| Dreno de Arcana | 4 | 4 PI/un (derivado) | Dado | = Quantidade de Dano | Dano |
| Decair | 4 | 4 PI fixo | — | — | Duração E Dano |
| Detrito | 4 | 2 PI fixo | — | — | Duração E Área E (Atordoamento OU Congelar OU Enraizar) |
| Fadiga | 4 | 3 PI fixo | — | — | Duração |
| Imagem Ilusória | 4 | manual fixo | — | — | Duração |
| Feixe | 5 | 10 PI fixo | — | — | Alcance |
| Ações por Turno | 5 | 20 PI/un | Ação | 2 (fixo) | Duração |
| Cegueira | 5 | 4 PI fixo | — | — | Duração |
| Defesa Verdadeira | 5 | 6 PI fixo | — | — | Duração |
| Deflexão | 5 | 8 PI fixo | — | — | Dano |
| Deflexão Mágica | 5 | 8 PI fixo | — | — | Dano |
| Encantamento Pessoal | 5 | 5 PI fixo | — | — | Duração |
| Encantar | 5 | 10 PI fixo | — | — | Duração |
| Amplificação Arcana | 6 | 4 PI fixo | — | — | — |
| Fúria | 6 | 4 PI fixo | — | — | Duração |
| Julgamento | 6 | 5 PI fixo | — | — | Duração |
| Absorção | 7 | 3 PI/un | Dado | 3×(Grau−6) a partir do Grau 7 | Duração |
| Crítico Aprimorado | 7 | 4 PI/un | 5% de Taxa Crítica | 4 (fixo) | Duração |
| Atordoamento | 8 | 10 PI fixo | — | — | Duração |
| Imunidade | 9 | manual fixo | — | — | — |

**Nota sobre Dano/Alcance** (efeitos básicos): o teto vem da tabela do topo de
"[[GRAUS & CÍRCULOS]]", copiada aqui como uma segunda tabela de teto fixa indexada por Grau (não
pela fórmula genérica `MaxUnidades × multiplicador` — os números não seguem essa progressão
linear):

| Grau | Máx. Dano (dados) | Máx. Alcance (pés) |
|---|---|---|
| 1 | 3 | 2 |
| 2 | 4 | 3 |
| 3 | 5 | 4 |
| 4 | 6 | 5 |
| 5 | 7 | 6 |
| 6 | 8 | 7 |
| 7 | 9 | 8 |
| 8 | 10 | 9 |
| 9 | 11 | 10 |

Duração usa um tipo de dado (d2, d4, ..., d100) por Grau, não uma contagem — sem teto de
Quantidade, fora do escopo do mecanismo `MaxUnidades`. (A mesma tabela do topo do documento tem
uma 4ª coluna, "Gasto" — 2/4/8/.../512 PI por Grau — que é o custo de *avançar* de Grau/Círculo em
si, um mecanismo de progressão de personagem já coberto por `EapCalculator`/Graduação em outro
lugar do app; não tem relação com o custo de comprar um Efeito e fica fora deste design.)

**Dreno de Vitalidade/Arcana**: `TipoDeCusto = DerivadoDeOutroEfeito`,
`QuantidadeDerivadaDeEfeito = "Dano"`. A Quantidade nunca é digitada — é sempre igual à
Quantidade que o Efeito "Dano" já tem na mesma lista. Se "Dano" ainda não foi adicionado (mesmo
que o pré-requisito já bloqueie a seleção inicial — cobre o caso de alguém remover Dano depois de
já ter adicionado Dreno), o formulário mostra um erro persistente ("Dreno de Vitalidade exige que
Dano já tenha dados definidos") até Dano ser reposto, bloqueando o salvamento enquanto isso.

## Cálculo de custo (Domain, `EfeitoCustoCalculator`)

```csharp
public static int Calcular(Efeito efeito, int grauDaMagia, int? quantidade, int? custoManual, int? quantidadeDerivada)
{
    if (efeito.CustoAlternativoAPartirDoGrau is { } limiar && grauDaMagia >= limiar)
        return efeito.CustoAlternativo!.Value; // ex.: Encantamento Elemental a partir do Grau 4

    return efeito.TipoDeCusto switch
    {
        TipoDeCusto.Fixo => efeito.CustoFixo!.Value,
        TipoDeCusto.PorUnidade => efeito.CustoPorUnidade!.Value * (quantidade ?? 1),
        TipoDeCusto.DerivadoDeOutroEfeito => efeito.CustoPorUnidade!.Value * (quantidadeDerivada ?? 0),
        TipoDeCusto.Manual => custoManual!.Value,
        TipoDeCusto.ManualPorUnidade => custoManual!.Value * (quantidade ?? 1), // custoManual = taxa por unidade digitada pelo Mestre
        _ => throw new InvalidOperationException(),
    };
}

public static int? MaxPermitido(Efeito efeito, int grauDaMagia)
{
    if (efeito.MaxUnidades is not { } max) return null;
    if (!efeito.MaxEscalaPorGrau) return max;
    var multiplicador = efeito.MaxContandoAPartirDoGrau is { } inicio ? grauDaMagia - inicio + 1 : grauDaMagia;
    return max * Math.Max(1, multiplicador);
}
```

Dano/Alcance usam uma tabela de teto separada indexada por Grau (não esta fórmula) — ver nota
acima. `SpellAbilityCostCalculator.GastoEmPI`/`.Custo` (soma + conversão pra Foco) não mudam.

## API

Novo `EfeitosController` (`api/efeitos`) — espelha `CreatureExclusiveTraitsController`/
`TraitsController`: `GET` aberto a qualquer autenticado (usado pelos 4 formulários e pela própria
página de Auditoria), `POST`/`PUT`/`DELETE` gated ao Auditor de Regras. `DELETE` bloqueado (409)
se o Nome do efeito aparecer em algum `SpellAbilityEffect`/`*SpellAbilityEffect` existente
(checagem por Nome, já que esses efeitos são cópias independentes — mesmo padrão que Item/Trait já
seguem para seus próprios catálogos).

Os 4 controllers existentes (`SpellAbilityBankController`, `CharacterSpellAbilitiesController`,
`NpcSpellAbilitiesController`, `CreatureSpellAbilitiesController`) ganham, no Create/Update (onde
a lista inteira de Efeitos é recebida), uma validação por efeito submetido: resolve o `Efeito` do
catálogo pelo Nome enviado, recalcula o `CustoPI` esperado com `EfeitoCustoCalculator` (usando o
`Grau` da própria Magia/Habilidade sendo salva), confere contra o `CustoPI` que o cliente mandou —
rejeita (400) se não bater, se faltar pré-requisito, se exceder o teto, ou se o Nome não existir
no catálogo. Isso barra a manipulação direta da API além de guiar a UI.

## Cliente

**Nova página `AuditoriaEfeitos.razor`** (`/auditoria/efeitos`), CRUD completo — mesmo padrão de
`AuditoriaCaracteristicasDeCriatura.razor`, com campos extra pros novos atributos estruturados
(TipoDeCusto, Custo, Unidade, Teto, pré-requisitos como uma lista de grupos editável). Link novo
em `RulesAuditorNavLinks.razor`.

**Os 4 formulários de adicionar Efeito** (Banco de Magias + 3 fichas): o campo Nome vira um select
(não mais texto livre) — lista filtrada por `Grau <= Grau da Magia/Habilidade` (cumulativo) **e**
por pré-requisito já satisfeito pelos Efeitos já presentes na mesma lista (grupos E/OU). Quando o
efeito selecionado exige Quantidade, mostra o campo com o `UnidadeLabel` certo e o teto calculado
como dica; quando `TipoDeCusto` é Manual/ManualPorUnidade, mostra o campo numérico manual
correspondente; quando é `DerivadoDeOutroEfeito`, não mostra Quantidade nenhuma — lê do Efeito
"Dano" já na lista (e mostra o erro persistente descrito acima se ausente). `CustoPI` sempre some
como somente-leitura, calculado ao vivo — nunca mais digitado.

## Fora de escopo

- Estender `GraduacaoEfeitoParser` para extrair os campos estruturados automaticamente — o
  catálogo novo é hand-authored, independente desse parser (que continua só alimentando a busca
  do Compêndio).
- Enforçar o teto de Duração (é um tipo de dado por Grau, não uma contagem — mecânica diferente).
- "Selar": fica de fora do catálogo até o Auditor definir.
- A tag "Tipo: Reativo" de Deflexão (sem efeito mecânico conhecido no app hoje).

## Testes

TDD em toda a parte de backend: `EfeitoCustoCalculatorTests` (Domain, unit — cobre Fixo,
PorUnidade, Manual, ManualPorUnidade, DerivadoDeOutroEfeito, a exceção
CustoAlternativo/CustoAlternativoAPartirDoGrau, e `MaxPermitido` nos 3 modos: sem teto, fixo,
escalando com e sem `ContandoAPartirDoGrau`); `EfeitosControllerTests` (integração, espelha
`CreatureExclusiveTraitsControllerTests`); testes de integração nos 4 controllers de
Magia/Habilidade existentes cobrindo: efeito com custo certo aceito, custo errado rejeitado (400),
efeito sem pré-requisito satisfeito rejeitado, teto excedido rejeitado, Dreno sem Dano na mesma
lista rejeitado. Cliente: sem bUnit dedicado para os 4 formulários (mesmo precedente de
`AuditoriaCaracteristicas.razor`/`BancoDeMagiasForm.razor`, que não têm) — a lógica de
filtro/bloqueio por pré-requisito fica pequena o bastante pra revisão de código cobrir.

# 2.2.0 — Afinidades por elemento e Auditoria de Afinidades

**Status:** desenho conversado com o usuário em 2026-10-30. A 2.2.0 deve ser **barata**: além disto, só entram
as pendências menores da 2.1.0 (seções "Cemitério — entregue" e "Correções e acabamento — entregue" de
`2026-10-22-versao-2-1-0-backlog.md`). O backup robusto da produção e **Vocações/Classes criadas pelo Auditor**
ficam para uma versão futura — este desenho guarda a regra "elementos por Vocação/Classe" como **dado**, para nada
precisar ser refeito quando Vocações/Classes virarem dinâmicas.

## Intenção

Hoje a seção 2.c é uma tabela de linhas (Essência Básica 1 + valor, Essência Básica 2 + valor, Sub-Elemento
calculado pelo cruzamento + valor, Experiência). O usuário quer **distribuir pontos por elemento** de forma direta e
visual, e quer que o **Auditor de Regras** controle as regras das afinidades (máximos, elementos, cruzamentos e o que
cada Vocação/Classe vê) sem mexer em código.

## Decisões do usuário

### Ficha (Personagem e NPC)

1. **Formato** (referência visual `Docs/Afinidades_sample.jpeg`, só ilustrativa — os nomes e números dela não são
   dados; o arquivo é apagado ao fim): uma lista de elementos, cada um com o nome, os botões **−** e **+**, o valor e
   uma **barra de segmentos** (um segmento por ponto possível, até o teto do elemento, preenchidos até o valor). No
   topo, "**distribuídos X/Y**" (Y = máximo da ficha) e uma mensagem de validação. O estilo segue o **padrão do resto
   da aplicação** (MudBlazor, cores e componentes já usados — nada copiado da imagem; o botão "Restaurar exemplo"
   da imagem **não** existe).
2. **O que aparece:**
   - **Ar, Água, Fogo e Terra**: sempre.
   - **Caminhos (Alma, Vida, Mundano) e Sub-Elementos**: conforme a **Vocação/Classe** do personagem, pelo que o
     Auditor configurar. **Hoje não há esse filtro** — é regra nova.
   - **Vocações marciais (Campeão, Caçador)**: veem **todos** os elementos, sem filtro — mas a seção só aparece se a
     ficha tiver **Afinidade Adicional > 0 ou coração de mana**.
   - Sub-Elemento cujo **requisito** não está cumprido aparece **bloqueado** (sem +), com dica do que falta
     (ex.: "Precisa de 1 em Ar e 1 em Água").
3. **Requisito de um Sub-Elemento** = o cruzamento que o gera: **mínimo de pontos em cada um dos dois elementos**,
   padrão **1**, editável pelo Auditor por cruzamento.
4. **Limites:** um **total** por ficha (hoje: coluna *Afinidade (Valores Absolutos)* da "Tabela de Circulo e Grau por
   EAP" no Círculo atual, + **Afinidade Adicional**, que continua existindo) e um **teto por elemento** (novo). O
   Auditor edita os dois.
5. **Experiência:** continua, **uma por elemento** (pode ser removida no futuro).
6. **Consistência:**
   - baixar um elemento abaixo do requisito de um Sub-Elemento que tem pontos → o servidor **recusa** e explica;
   - o Auditor muda uma regra e uma ficha fica inconsistente (requisito maior, Sub-Elemento tirado da Vocação, teto
     menor) → os pontos **não somem**: o elemento aparece bloqueado/marcado, continua contando no total, e só aceita
     **tirar** pontos;
   - o Auditor tenta **remover um elemento com pontos em alguma ficha** → recusado, informando quantas fichas.
7. **Conversão das fichas existentes** (linhas → pontos por elemento):
   - cada Elemento/Caminho recebe o **maior valor** com que aparecia em qualquer linha (a mesma conta do gasto de
     hoje — o total gasto de cada ficha **não muda**);
   - cada Sub-Elemento recebe a soma dos valores das suas linhas; Alma e Vida, que existiam como Essência e como Sub-Elemento, ficam com os dois valores somados (o gasto antigo contava os dois);
   - a Experiência da linha vai para o Sub-Elemento dela, ou para a Essência Básica 1 se a linha não tem
     Sub-Elemento; duas linhas mandando para o mesmo elemento → fica a maior;
   - linhas totalmente vazias somem.

### Auditoria de Afinidades

A página de Auditoria de Afinidades (hoje só a "Tabela de Afinidades": Eficiência/Dano por valor, Auditoria R0016,
que **continua**) passa a controlar também:

1. **Total por Círculo** (o máximo de pontos de afinidade de cada Círculo) — valores iniciais = a coluna atual da
   "Tabela de Circulo e Grau por EAP"; depois disso vale o que o Auditor mantém (mesmo padrão da Tabela de Níveis,
   Auditoria R0013: o Markdown só semeia enquanto a tabela está vazia).
2. **Elementos** — criar, editar e remover (com a recusa da decisão 6). Cada elemento tem nome, **tipo** (Elemento
   base, Caminho ou Sub-Elemento) e **teto** de pontos.
3. **Cruzamentos** — editar os existentes e criar novos: dois elementos → um Sub-Elemento, com o mínimo de pontos de
   cada requisito. Os **12 cruzamentos de hoje** entram como estão (Ar+Água=Gelo, Ar+Fogo=Raio, Água+Terra=Flora,
   Fogo+Terra=Ferro, Ar+Alma=Prever, Água+Alma=Purificar, Fogo+Vida=Curar, Terra+Vida=Aprimorar, Ar+Mundano=Ecomancia,
   Água+Mundano=Hemomancia, Fogo+Mundano=Necromancia, Terra+Mundano=Invocação), com requisito 1 e 1.
4. **Elementos por Vocação e por Classe** — quais Caminhos e Sub-Elementos cada Vocação (mágica) e cada Classe
   vê.

## Comportamento

### Gasto e validação
- **Gasto** = soma dos pontos de todos os elementos da ficha (a conta de "maior valor por Essência" deixa de existir,
  porque cada elemento agora tem um valor só).
- **Máximo** = total do Círculo atual (vocações mágicas; sem coração de mana o Círculo é 0) + Afinidade Adicional;
  vocações marciais e ficha sem Vocação: só a Afinidade Adicional — como hoje (R0007), agora com o total por Círculo
  vindo da Auditoria.
- O servidor recusa um + que faria o gasto passar do máximo, passar do teto do elemento, ou usar um elemento
  bloqueado/não visível para a Vocação/Classe. Baixar o máximo continua permitido (contador em vermelho; só aceita
  tirar pontos), como hoje.
- **NPC:** mesmo formato e regras; ver "Valores iniciais" sobre quem é bloqueado.
- **Visibilidade por Vocação e por Classe:** um Caminho/Sub-Elemento aparece se estiver ligado à Vocação **ou** à
  Classe da ficha *(chamada minha)*. Se o Auditor tirar todas as ligações de uma Vocação mágica, ela passa a ver
  só os 4 Elementos base.

### Afinidade Elemental (2.b) e Eficiência/Dano
- O campo *Afinidade* de 2.b (a afinidade elemental do personagem, que define Eficiência e Dano Elemental pela
  Tabela de Afinidades) passa a listar os elementos do catálogo do Auditor (exceto Caminhos, como hoje), e usa o
  **valor daquele elemento** na nova lista. *(chamada minha: hoje ele lista o enum fixo; com elementos criáveis ele
  precisa ler o catálogo.)*

### Modelo de dados (direção; nomes ficam com o construtor)
- **Decisão do usuário (2026-10-30): um só catálogo de elementos para tudo.** Os enums fixos `AfinidadeElemental`,
  `Elemento`, `EssenciaBasica` e `SubElemento` dão lugar a um **catálogo de elementos** no banco, usado pela ficha
  (Personagem/NPC: 2.c e o campo *Afinidade* de 2.b), pela *Afinidade* da **Criatura** (1.a), pelas **Condições**
  (`AlvoDeCondicao.AfinidadeElemental`) e pelos **requisitos de Passivas** — um elemento criado pelo Auditor aparece
  em todos esses lugares; nada fica numa segunda lista.
- **Ids estáveis:** cada elemento tem um id próprio, separado do nome (o Auditor pode renomear). Os elementos de hoje
  entram no catálogo com **o mesmo inteiro** que o `AfinidadeElemental` já grava (Terra=0 … Invocacao=17), e
  **Mundano** (que hoje só existe em `EssenciaBasica`) recebe um id novo — assim as colunas de Criatura, Condições,
  Passivas e logs que guardam `AfinidadeElemental` **não precisam de conversão**, só passam a referenciar o catálogo.
  São **19 elementos**: 4 base, 3 Caminhos (Alma, Vida, Mundano), 12 Sub-Elementos.
- A remoção de um elemento (decisão 6) também é recusada enquanto alguma **Condição** ou **Passiva** o usar.
- A migration cria o catálogo, os 12 cruzamentos, o total por Círculo, as ligações iniciais de visibilidade e converte
  as linhas de afinidade → pontos por elemento (decisão 7).
- **Decisão do usuário (2026-10-30): deploy com janela.** A conversão muda dados e a 2.1.0 não lê o formato novo:
  o release vai com o label `deploy:janela` (uns 3 minutos em manutenção, como na virada da 2.0.0) e com entrada em
  `deploy/data-changes.yml` para a conversão. Não manter colunas antigas em paralelo.

### Valores iniciais (chamadas minhas — preservam o comportamento de hoje)
- **Teto por elemento:** sem limite (o Auditor define depois).
- **Visibilidade:** toda Vocação mágica (Feiticeiro, Adepto, Bruxo) começa ligada a **todos** os Caminhos e
  Sub-Elementos — nenhuma ficha existente fica com pontos "inconsistentes" na virada; o Auditor restringe depois.
- **Marcial × mágica:** Campeão e Caçador são marciais pela regra que já existe no Domain (R0007); vira dado quando
  as Vocações ficarem dinâmicas. Trocar a Vocação de uma ficha com pontos cai na decisão 6 (inconsistente: pontos
  ficam, só podem ser tirados).
- **Afinidade Elemental (2.b):** lê o valor único do elemento escolhido na lista nova; Caminhos continuam fora da
  lista do campo.
- **NPC:** a regra de hoje (Ficha de NPCs R0010) se estende às regras novas — o **GM** não é bloqueado; o
  **jogador** de um NPC concedido segue as regras do Personagem.

## Estrutura (regra da 2.0.0 — pedido do usuário)

- Regras puras no **Domain**: gasto, máximo, visibilidade por Vocação/Classe, requisito cumprido, o que pode subir ou
  descer, e a conversão de linhas → pontos (função pura, testada com casos reais).
- Ficha: o recurso de afinidades já é **genérico** (`AfinidadesDaFichaController` sobre `ITipoDeFicha`, Personagem e
  NPC); continua assim — a diferença "NPC não bloqueia" é um fato do tipo (como já é hoje), não um ramo.
- Cliente: a seção nova substitui `AfinidadesSection` em `Shared/Ficha/`, usada pelas duas fichas; diferenças via
  `TipoDeFichaCliente`. Componentes MudBlazor existentes; nada de estilo novo.
- Auditoria: segue as páginas de Auditoria existentes (`AuditoriaTabelaDeAfinidades.razor` como ponto de partida,
  ou páginas irmãs no mesmo padrão); endpoints no controller da Tabela de Afinidades/Auditoria, restritos ao Auditor.
- Vocações/Classes continuam o enum/tabela de hoje, mas a ligação elemento↔Vocação/Classe é **tabela de dados**
  (chave por Vocação e por Classe como texto/identificador estável), para o futuro "Vocações dinâmicas" só trocar a
  origem da lista.

## Documentação

- Ficha de Personagem 2.c e R0007 reescritos; 1.a/2.b (*Afinidade*) ajustados; Ficha de NPCs R0010 (limites não
  bloqueiam também as regras novas).
- Auditoria de Regras: R0016 ampliado ou requisitos novos (total por Círculo, elementos, cruzamentos, elementos por
  Vocação/Classe).
- Modelo de Dados: catálogo de elementos, cruzamentos, total por Círculo, ligações, pontos por elemento na ficha.
- "Tabela de Circulo e Grau por EAP": a coluna de Afinidade passa a só semear (anotar em CLAUDE.md, como a Tabela
  de Níveis).

## Restrições
- TDD; build 0 warnings; projetos de teste um de cada vez.
- `rotas-da-api.txt` e redes de segurança do cliente mudam só pelo que esta entrega muda, explicado no commit; a aba
  Atributos e Perícias tem rede de segurança (`RedeDeSeguranca/`), que muda pela seção nova de forma intencional.
- O ensaio do pipeline (`release.yml` por `workflow_dispatch`) precisa passar com o dump da produção (a conversão das
  fichas reais é o maior risco desta entrega).

## Deixado para o construtor
Nomes de tabelas/rotas/componentes; layout exato das páginas de Auditoria; como migrar com segurança (compatível ou
janela); ordem das tarefas.

> Esta página não corresponde a nenhum bloco do "[[01 - Visão Geral.canvas]]" ainda — é um subsistema novo, complementar a "[[Requisitos - Ficha de Personagem]]" (4.b), "[[Requisitos - Ficha de NPCs]]" e "[[Requisitos - Ficha de Criaturas]]" (R0007).

  

> **Modelo de edição**: o banco pertence à conta do GM (não a uma Campanha específica — ver "[[Requisitos - Campanha]]" para como uma entrada dele é opcionalmente anexada e liberada numa Campanha). O GM tem acesso total a qualquer entrada, a qualquer momento. Jogadores não têm uma tela própria para o banco — eles só o alcançam a partir de 5.b/4.b nas fichas que controlam, e apenas para reutilizar entradas (ver R0003).

  

> **Convenção de campos**: mesma de "[[Requisitos - Ficha de Personagem]]" — valor padrão do banco de dados, placeholder em itálico quando NULL, travessão para campos não aplicáveis.

  

# **R0001** - Toda Magia/Habilidade criada em qualquer ficha é adicionada automaticamente ao banco.

**Descrição**: Sempre que uma entrada de Magia ou Habilidade é criada em 4.b de uma Ficha de Personagem, Ficha de NPC ou Ficha de Criatura, uma cópia dela é automaticamente salva neste banco geral do GM — tanto quando criada por um jogador quanto quando criada pelo GM, sem etapa de aprovação. A cópia no banco é **independente**: editar a entrada na ficha de origem depois de criada não altera a cópia no banco, e vice-versa.

  

# **R0002** - O GM pode criar uma entrada diretamente no banco.

**Descrição**: Além das entradas vindas automaticamente de fichas (R0001), o GM pode criar uma nova entrada direto no banco, sem que ela venha de nenhuma ficha específica.

  

# **R0003** - Ao montar uma Magia/Habilidade numa ficha, é possível partir de uma entrada existente do banco.

**Descrição**: Ao criar uma nova entrada em 4.b de uma Ficha de Personagem, de NPC ou de Criatura, o jogador/GM pode escolher entre montar do zero (ver "[[Requisitos - Ficha de Personagem]]" 4.b) ou selecionar uma entrada já existente no banco, que preenche todos os campos automaticamente. A partir da escolha, a entrada na ficha é uma cópia independente (mesmo comportamento de R0001) — editá-la depois não afeta a entrada original do banco.

  

# **R0004** - O banco deve ser listado com filtros avançados.

**Descrição**: Uma lista exibe todas as entradas do banco, com filtros combináveis por **Nome**, **Tipo** (Magia, Habilidade ou Racial) e **Grau**.

  

# **R0005** - Campos de uma entrada do banco.

**Descrição**: Os mesmos campos de uma entrada de Magia/Habilidade em "[[Requisitos - Ficha de Personagem]]" 4.b: **Nome**, **Tipo**, **Grau**, **Efeitos**, **Gasto em PI** (calculado) e **Custo** (calculado), **Descrição**.

  

# **R0006** - O GM pode editar e excluir qualquer entrada do banco.

**Descrição**: A partir da lista (R0004), o GM pode editar qualquer campo de uma entrada ou excluí-la. Excluir uma entrada do banco não afeta nenhuma ficha que já a usou como base (ver R0003) — são cópias independentes.

# **R0007** - A cópia criada por um jogador também vira um anexo público da campanha.

> Quando quem cria a entrada (do zero ou reaproveitando outra) é um **jogador**, não o GM que gerencia a ficha, a cópia independente que R0001 já cria no banco também é anexada automaticamente à campanha daquela ficha como pública, conforme Requisitos - Campanha R0012.

> Esta página não corresponde a nenhum bloco do "[[01 - Visão Geral.canvas]]" ainda — é um subsistema novo, complementar a "[[Requisitos - Ficha de Personagem]]" (4.d) e "[[Requisitos - Ficha de NPCs]]". A Ficha de Criatura não tem Runas (ver "[[Requisitos - Ficha de Criaturas]]" R0007) e, portanto, não usa este banco.

  

> **Modelo de edição**: o banco pertence à conta do GM (não a uma Campanha específica — ver "[[Requisitos - Campanha]]" para como uma entrada dele é opcionalmente anexada e liberada numa Campanha). O GM tem acesso total a qualquer entrada, a qualquer momento. Jogadores não têm tela própria para o banco nem leitura direta dele (ver R0008): só o alcançam a partir de 4.d nas fichas que controlam, e apenas para reutilizar entradas que o GM liberou como públicas na campanha (ver R0003).

  

> **Convenção de campos**: mesma de "[[Requisitos - Ficha de Personagem]]" — valor padrão do banco de dados, placeholder em itálico quando NULL, travessão para campos não aplicáveis.

  

# **R0001** - Toda Runa criada em qualquer ficha é adicionada automaticamente ao banco.

**Descrição**: Sempre que uma Runa é criada em 4.d de uma Ficha de Personagem ou de uma Ficha de NPC, uma cópia dela é automaticamente salva neste banco geral do GM — tanto quando criada por um jogador quanto quando criada pelo GM, sem etapa de aprovação, e tanto quando montada do zero quanto quando partiu de uma entrada existente do banco (R0003). A cópia no banco é **independente**: editar a Runa na ficha de origem depois de criada não altera a cópia no banco, e vice-versa. Uma Runa que já existia numa ficha antes deste banco existir **não** é copiada retroativamente — o banco começa vazio.

  

# **R0002** - O GM pode criar uma entrada diretamente no banco.

**Descrição**: Além das entradas vindas automaticamente de fichas (R0001), o GM pode criar uma nova entrada direto no banco, sem que ela venha de nenhuma ficha específica.

  

# **R0003** - Ao adicionar uma Runa numa ficha, é possível partir de uma entrada existente do banco.

**Descrição**: Ao adicionar uma Runa em 4.d de uma Ficha de Personagem ou de NPC, o jogador/GM pode escolher entre montar do zero (ver "[[Requisitos - Ficha de Personagem]]" 4.d) ou selecionar uma entrada já existente no banco, que preenche todos os campos automaticamente. O GM pode escolher qualquer entrada do próprio banco; um jogador só pode escolher entre as entradas que o GM anexou à campanha da ficha como **públicas** ("[[Requisitos - Campanha]]" R0008). A partir da escolha, a Runa na ficha é uma cópia independente (mesmo comportamento de R0001) — editá-la depois não afeta a entrada original do banco.

  

# **R0004** - O banco deve ser listado com filtros.

**Descrição**: Uma lista exibe todas as entradas do banco, com filtros combináveis por **Nome** (contém, sem diferenciar maiúsculas de minúsculas) e **Grau** (igual).

  

# **R0005** - Campos de uma entrada do banco.

**Descrição**: Os mesmos campos de uma Runa em "[[Requisitos - Ficha de Personagem]]" 4.d: **Nome** (texto), **Descrição** (texto livre) e **Grau** (número inteiro). Nenhum campo é calculado. Uma entrada do banco não pertence a nenhuma ficha, então o limite de Grau de "[[Requisitos - Ficha de Personagem]]" 4.d não se aplica a ela.

Cada entrada tem ainda uma **Imagem**: opcional, uma só por entrada, com o mesmo comportamento da Imagem de "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0003 (formatos e tamanho máximo iguais); no banco, o GM só usa imagens que ele mesmo enviou. Ao partir de uma entrada do banco, a Runa da ficha herda a imagem da entrada (não é possível escolher outra) e a cópia automática (R0001) a leva junto. Ao montar uma Runa do zero numa ficha, a imagem opcional pode ser um upload do próprio usuário ou, para o jogador, uma imagem que o GM liberou como pública na campanha.

  

# **R0006** - O GM pode editar e excluir qualquer entrada do banco.

**Descrição**: A partir da lista (R0004), o GM pode editar qualquer campo de uma entrada ou excluí-la. Excluir uma entrada do banco não afeta nenhuma ficha que já a usou como base (ver R0003) — são cópias independentes; anexos de campanha dessa entrada, porém, são removidos junto com ela.

  

# **R0007** - A cópia criada por um jogador também vira um anexo público da campanha.

> Quando quem cria a Runa (do zero ou reaproveitando outra) é um **jogador**, não o GM que gerencia a ficha, a cópia independente que R0001 já cria no banco também é anexada automaticamente à campanha daquela ficha como pública, conforme "[[Requisitos - Campanha]]" R0012. Para um NPC concedido a um jogador, a campanha é a da concessão (ver "[[Requisitos - Campanha]]" R0010).

  

# **R0008** - O banco em si é visível apenas ao GM.

**Descrição**: Diferente do "[[Requisitos - Banco de Magias e Habilidades]]" (cuja lista um jogador vinculado consegue ler por inteiro), o Banco de Runas é GM-only — listar, criar, editar e excluir entradas. O jogador nunca lê o banco privado do GM: ele só enxerga as entradas anexadas como públicas a uma campanha da qual é membro (ver "[[Requisitos - Campanha]]" R0008 e R0009).

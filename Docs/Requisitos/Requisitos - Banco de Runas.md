> Esta página não corresponde a nenhum bloco do "[[01 - Visão Geral.canvas]]" ainda — é um subsistema novo, complementar a "[[Requisitos - Ficha de Personagem]]" (4.d) e "[[Requisitos - Ficha de NPCs]]". A Ficha de Criatura não tem Runas (ver "[[Requisitos - Ficha de Criaturas]]" R0007) e, portanto, não usa este banco.

  

> **Modelo de edição**: o banco pertence à conta do GM (não a uma Campanha específica — ver "[[Requisitos - Campanha]]" para como uma entrada dele é opcionalmente anexada e liberada numa Campanha). O GM tem acesso total a qualquer entrada, a qualquer momento. Jogadores não têm tela própria para o banco nem leitura direta dele (ver R0008): só o alcançam a partir de 4.d nas fichas que controlam, e apenas para reutilizar entradas que o GM liberou como públicas na campanha (ver R0003).

  

> **Convenção de campos**: mesma de "[[Requisitos - Ficha de Personagem]]" — valor padrão do banco de dados, placeholder em itálico quando NULL, travessão para campos não aplicáveis.

  

# **R0001** - Toda Runa criada em qualquer ficha é adicionada automaticamente ao banco.

**Descrição**: Sempre que uma Runa é montada **do zero** em 4.d de uma Ficha de Personagem ou de uma Ficha de NPC, uma cópia dela é automaticamente salva neste banco geral do GM — tanto quando criada por um jogador quanto quando criada pelo GM, sem etapa de aprovação. A Runa da ficha guarda de qual entrada do banco ela veio. Uma Runa que parte de uma entrada já existente do banco (R0003) **não** gera cópia nova — o banco não fica com duplicatas. A cópia no banco é **independente**: editar depois a entrada do banco (R0006) não altera nenhuma Runa já adicionada a fichas. A Runa na ficha, por sua vez, não pode ser editada (só adicionada e removida — ver "[[Requisitos - Ficha de Personagem]]" 4.d), então nada na ficha muda a cópia do banco. Uma Runa que já existia numa ficha antes deste banco existir **não** é copiada retroativamente — o banco começa vazio.

  

# **R0002** - O GM pode criar uma entrada diretamente no banco.

**Descrição**: Além das entradas vindas automaticamente de fichas (R0001), o GM pode criar uma nova entrada direto no banco, sem que ela venha de nenhuma ficha específica.

  

# **R0003** - Ao adicionar uma Runa numa ficha, é possível partir de uma entrada existente do banco.

**Descrição**: Ao adicionar uma Runa em 4.d de uma Ficha de Personagem ou de NPC, o jogador/GM pode escolher entre montar do zero (ver "[[Requisitos - Ficha de Personagem]]" 4.d) ou selecionar uma entrada já existente no banco, que preenche todos os campos automaticamente. O GM pode escolher qualquer entrada do próprio banco; um jogador só pode escolher entre as entradas que o GM anexou à campanha da ficha como **públicas** ("[[Requisitos - Campanha]]" R0008). A partir da escolha, a Runa na ficha é uma cópia independente (mesmo comportamento de R0001), e nenhuma entrada nova é criada no banco — editar a entrada do banco depois não afeta a Runa já adicionada à ficha. A Runa na ficha não pode ser editada; para mudá-la, remove-se e adiciona-se de novo.

  

# **R0004** - O banco deve ser listado com filtros.

**Descrição**: Uma lista exibe todas as entradas do banco, com filtros combináveis por **Nome** (contém, sem diferenciar maiúsculas de minúsculas), **Grau** (igual) e **Tipo** (R0009: Todos, Runa Arcana, Runa Negra ou Sem tipo) e **Disciplina** (R0010: Todas, uma das quatro ou Sem disciplina). A lista mostra também o Tipo e a Disciplina de cada entrada (travessão quando não tem).

  

# **R0005** - Campos de uma entrada do banco.

**Descrição**: Os mesmos campos de uma Runa em "[[Requisitos - Ficha de Personagem]]" 4.d: **Nome** (texto), **Descrição** (texto livre) e **Grau** (número inteiro) e **Disciplina** (R0010, obrigatória). Nenhum campo é calculado. Uma entrada do banco não pertence a nenhuma ficha, então o limite de Grau de "[[Requisitos - Ficha de Personagem]]" 4.d não se aplica a ela.

Cada entrada tem ainda uma **Imagem**: opcional, uma só por entrada, com o mesmo comportamento da Imagem de "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0003 (formatos e tamanho máximo iguais); no banco, o GM só usa imagens que ele mesmo enviou. Ao partir de uma entrada do banco, a Runa da ficha herda a imagem da entrada (não é possível escolher outra) e a cópia automática (R0001) a leva junto. Ao montar uma Runa do zero numa ficha, a imagem opcional pode ser um upload do próprio usuário ou, para o jogador, uma imagem que o GM liberou como pública na campanha.

  

# **R0006** - O GM pode editar e excluir qualquer entrada do banco.

**Descrição**: A partir da lista (R0004), o GM pode editar qualquer campo de uma entrada ou excluí-la. Excluir uma entrada do banco não afeta nenhuma ficha que já a usou como base (ver R0003) — são cópias independentes; anexos de campanha dessa entrada, porém, são removidos junto com ela.

  

# **R0007** - A cópia criada por um jogador também vira um anexo público da campanha.

> Quando quem monta a Runa do zero é um **jogador**, não o GM que gerencia a ficha, a cópia independente que R0001 já cria no banco também é anexada automaticamente à campanha daquela ficha como pública, conforme "[[Requisitos - Campanha]]" R0012. Para um NPC concedido a um jogador, a campanha é a da concessão (ver "[[Requisitos - Campanha]]" R0010). Reaproveitar uma entrada do banco (R0003) não anexa nada: para o jogador alcançá-la, ela já precisa ser pública na campanha.

  

# **R0008** - O banco em si é visível apenas ao GM.

**Descrição**: Diferente do "[[Requisitos - Banco de Magias e Habilidades]]" (cuja lista um jogador vinculado consegue ler por inteiro), o Banco de Runas é GM-only — listar, criar, editar e excluir entradas. O jogador nunca lê o banco privado do GM: ele só enxerga as entradas anexadas como públicas a uma campanha da qual é membro (ver "[[Requisitos - Campanha]]" R0008 e R0009).

  

# **R0009** - Tipo opcional da Runa: Arcana ou Negra.

**Descrição**: Cada Runa — entrada do banco e Runa de ficha (Personagem ou NPC) — tem um **Tipo** opcional: **Runa Arcana**, **Runa Negra** ou nenhum ("Sem tipo", o padrão). É só uma classificação exibida no banco, nas fichas e nas listas de Runas liberadas a um jogador; **não altera nenhum cálculo**. No formulário do banco (criar/editar) e ao montar uma Runa do zero em 4.d ("[[Requisitos - Ficha de Personagem]]"), um select **Tipo** oferece "Sem tipo", "Runa Arcana" e "Runa Negra", acompanhado de um popup de ajuda ⓘ ("Tipo da Runa"). Ao partir de uma entrada do banco (R0003), a Runa da ficha copia o Tipo da entrada, como copia Nome, Grau e Imagem; a cópia automática de R0001 leva o Tipo junto; conceder/copiar uma ficha de NPC também o preserva. Runas que já existiam antes deste campo ficam **sem tipo** até o GM editá-las no banco (a Runa de ficha continua não editável). No fio, o Tipo é o texto "Arcana" ou "Negra" (ou vazio/ausente = sem tipo); qualquer outro valor é rejeitado com 400 ("Tipo de Runa desconhecido."). A listagem do banco aceita o filtro de Tipo ("Arcana", "Negra" ou "Nenhum" para as sem tipo).

  

# **R0010** - Disciplina obrigatória da Runa.

**Descrição**: Cada Runa — entrada do banco e Runa de ficha (Personagem ou NPC) — tem uma **Disciplina**, que diz o que a Runa influencia. São quatro, sem opção em branco:

- **Adição**: influencia o corpo do usuário.
- **Alteração**: influencia objetos inanimados.
- **Emissão**: influencia alvos inanimados.
- **Manifestação**: manifesta a aura do usuário.

O formulário do banco (criar/editar) e a montagem de uma Runa do zero em 4.d ("[[Requisitos - Ficha de Personagem]]") oferecem um select **Disciplina**, obrigatório, acompanhado de um popup de ajuda ⓘ ("Disciplina da Runa") que lista as quatro com suas descrições. Criar ou editar sem escolher uma Disciplina mostra "Escolha a Disciplina da runa." e **não** envia nada ao servidor (a edição não é salva automaticamente até uma ser escolhida); o servidor também rejeita com 400 ("Disciplina é obrigatória."). Ao partir de uma entrada do banco (R0003), a Runa da ficha copia a Disciplina da entrada, como copia Nome, Grau, Tipo e Imagem; a cópia automática de R0001 a leva junto. **Runas antigas**, anteriores a este campo, ficam **sem Disciplina** (travessão nas listas) até o GM editá-las no banco — ao editar uma, o GM precisa escolher uma Disciplina para o salvamento voltar a funcionar —, e uma entrada antiga sem Disciplina ainda pode ser escolhida numa ficha (a Runa herda "sem Disciplina"). No fio, a Disciplina é o nome do valor ("Adicao", "Alteracao", "Emissao" ou "Manifestacao"); qualquer outro valor é rejeitado com 400. A listagem do banco aceita o filtro de Disciplina (um dos quatro nomes ou "Nenhuma" para as sem disciplina).

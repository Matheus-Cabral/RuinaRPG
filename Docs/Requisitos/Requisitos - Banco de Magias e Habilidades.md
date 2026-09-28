> Esta página não corresponde a nenhum bloco do "[[01 - Visão Geral.canvas]]" ainda — é um subsistema novo, complementar a "[[Requisitos - Ficha de Personagem]]" (4.b), "[[Requisitos - Ficha de NPCs]]" e "[[Requisitos - Ficha de Criaturas]]" (R0007).

  

> **Modelo de edição**: o banco pertence à conta do GM (não a uma Campanha específica — ver "[[Requisitos - Campanha]]" para como uma entrada dele é opcionalmente anexada e liberada numa Campanha). O GM tem acesso total a qualquer entrada, a qualquer momento. Jogadores não têm uma tela própria para o banco nem leitura direta dele (a listagem é GM-only) — eles só o alcançam a partir de 4.b nas fichas que controlam, e apenas para reutilizar as entradas que o GM anexou como públicas à campanha (ver R0003).

  

> **Convenção de campos**: mesma de "[[Requisitos - Ficha de Personagem]]" — valor padrão do banco de dados, placeholder em itálico quando NULL, travessão para campos não aplicáveis.

  

# **R0001** - Toda Magia/Habilidade criada em qualquer ficha é adicionada automaticamente ao banco.

**Descrição**: Sempre que uma entrada de Magia ou Habilidade é montada **do zero** em 4.b de uma Ficha de Personagem, Ficha de NPC ou Ficha de Criatura, uma cópia dela é automaticamente salva neste banco geral do GM — tanto quando criada por um jogador quanto quando criada pelo GM, sem etapa de aprovação. A cópia no banco é **independente**: editar a entrada na ficha de origem depois de criada não altera a cópia no banco, e vice-versa. A entrada da ficha guarda, porém, de qual entrada do banco ela veio (usado na concessão — "[[Requisitos - Campanha]]" R0010). Uma entrada que parte de uma entrada já existente do banco (R0003) **não** gera cópia nova — o banco não fica com duplicatas.

  

# **R0002** - O GM pode criar uma entrada diretamente no banco.

**Descrição**: Além das entradas vindas automaticamente de fichas (R0001), o GM pode criar uma nova entrada direto no banco, sem que ela venha de nenhuma ficha específica.

  

# **R0003** - Ao montar uma Magia/Habilidade numa ficha, é possível partir de uma entrada existente do banco.

**Descrição**: Ao criar uma nova entrada em 4.b de uma Ficha de Personagem, de NPC ou de Criatura, o jogador/GM pode escolher entre montar do zero (ver "[[Requisitos - Ficha de Personagem]]" 4.b) ou selecionar uma entrada já existente no banco, que preenche todos os campos automaticamente. A partir da escolha, a entrada na ficha é uma cópia independente (mesmo comportamento de R0001) — editá-la depois não afeta a entrada original do banco. Nenhuma entrada nova é criada no banco: a ficha fica vinculada à entrada escolhida.

**Escopo da escolha**: o GM escolhe qualquer entrada do próprio banco; um jogador só pode escolher entre as entradas que o GM anexou à campanha da ficha como **públicas** (ver "[[Requisitos - Campanha]]" R0008 e "[[Requisitos - Ficha de Personagem]]" R0003). O servidor recusa (400) uma entrada de outro GM, ou uma que o jogador não alcança — não basta o cliente só listar as públicas. No NPC/Criatura concedido a um jogador, a campanha é a da concessão.

  

# **R0004** - O banco deve ser listado com filtros avançados.

**Descrição**: Uma lista exibe todas as entradas do banco, com filtros combináveis por **Nome**, **Tipo** (Magia, Habilidade, Racial ou Passiva), **Grau** e **Criatura** (Todas / Só de Criatura / Sem Criatura — ver R0008). As entradas marcadas como de Criatura exibem, ao lado do Nome, o mesmo ícone (e cor) do Bestiário no menu lateral, com a dica "Magia/Habilidade de Criatura". Uma linha de Passiva (Tipo = Passiva) exibe a **Categoria** no lugar de Grau/Efeitos.

  

# **R0005** - Campos de uma entrada do banco.

**Descrição**: Os mesmos campos de uma entrada de Magia/Habilidade em "[[Requisitos - Ficha de Personagem]]" 4.b: **Nome**, **Tipo**, **Grau**, **Efeitos**, **Gasto em PI** (calculado) e **Custo** (calculado), **Descrição**, e mais o checkbox **Magia/Habilidade de Criatura** (R0008), que só existe no banco. O Nome de cada Efeito e seu Custo em PI seguem o catálogo e o cálculo automático descritos em "[[Requisitos - Auditoria de Regras]]" R0006/R0007 — não são mais campos de texto/número livres. Quando o Tipo é Passiva, os campos são outros — ver R0009.

  

# **R0006** - O GM pode editar e excluir qualquer entrada do banco.

**Descrição**: A partir da lista (R0004), o GM pode editar qualquer campo de uma entrada ou excluí-la. Excluir uma entrada do banco não afeta nenhuma ficha que já a usou como base (ver R0003) — são cópias independentes.

# **R0007** - A cópia criada por um jogador também vira um anexo público da campanha.

> Quando quem monta a entrada do zero é um **jogador**, não o GM que gerencia a ficha, a cópia independente que R0001 já cria no banco também é anexada automaticamente à campanha daquela ficha como pública, conforme Requisitos - Campanha R0012. Reaproveitar uma entrada do banco (R0003) não anexa nada: para o jogador alcançá-la, ela já precisa ser pública na campanha.

  

# **R0008** - Uma entrada pode ser marcada como Magia/Habilidade de Criatura.

**Descrição**: Nas páginas de nova entrada e de edição do banco (só acessíveis ao GM), o checkbox **Magia/Habilidade de Criatura** marca a entrada como própria de criaturas. A marcação serve apenas de filtro na lista (R0004) — não restringe em qual ficha a entrada pode ser usada. A cópia que R0001 cria a partir de uma **Ficha de Criatura** já nasce marcada, sem checkbox na ficha; as vindas de Ficha de Personagem ou de NPC nascem desmarcadas. Padrão: **desmarcado**.


# **R0009** - Passivas são cadastradas só no banco, com Categoria e Requisitos.

**Descrição**: Uma Passiva (Tipo = Passiva) é um novo tipo de entrada do banco, com campos próprios: **Nome**, **Descrição**, **Categoria** (Passiva Livre / Passiva Vocacional / Passiva de Classe) e o checkbox **Magia/Habilidade de Criatura** (R0008). Ela não tem Grau, Efeitos, Gasto em PI nem Custo. Uma Passiva nunca é montada do zero numa ficha — só pode ser escolhida a partir do banco (ver "[[Requisitos - Ficha de Personagem]]" 4.f), tanto pelo GM quanto pelo jogador.

**Requisitos**: além dos campos acima, uma Passiva tem uma seção de Requisitos que a ficha precisa cumprir para recebê-la. Todo campo é opcional; NULL/vazio significa que aquele requisito não existe, e todos os campos preenchidos precisam ser cumpridos ao mesmo tempo (E lógico) — a checagem vale para qualquer ficha, GM incluído. Os campos são: **Nível** (mínimo), **Vocação**, **Classe**, **Linhagem**, **Variante**, **Grau/Círculo** (mínimo), **Coração de Mana** (exige que a ficha o possua), **Afinidade Elemental**, **Estrela**, **Histórico** (referência ao catálogo de "[[Requisitos - Ficha de Personagem]]"), mais três listas construídas incrementalmente pelo GM: **Atributos** (cada item: qual Atributo e o mínimo exigido — o Atributo compara com o Total, não com o valor base), **Sub-Atributos** (Iniciativa, Movimentação, Esquiva Natural, Defesa Natural, Redução Física ou Redução Mágica, cada um com um mínimo) e **Perícias** (cada item: qual Perícia e o Total mínimo exigido).

Um ⓘ ao lado da seção de Requisitos abre um popup explicando ao GM como o cadastro funciona: Passivas só são cadastradas aqui; campos vazios não são requisito; todos os requisitos preenchidos precisam ser cumpridos; uma ficha que não os cumpre não recebe a Passiva, nem o GM pode forçar; uma Criatura ignora requisitos sobre campos que ela não possui; e uma ficha que deixa de cumprir os requisitos depois de já ter a Passiva a mantém, apenas com um aviso.

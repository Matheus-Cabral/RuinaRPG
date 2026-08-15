> Esta página se baseia no bloco **PAINEL DO *GM*** (ferramenta 5, "Campanhas") do "[[01 - Visão Geral.canvas]]".

  

> **Modelo de edição**: apenas o **GM** dono da campanha tem acesso à criação, edição e exclusão de tudo o que está descrito aqui. Um jogador membro só enxerga o que é descrito em [[#**R0009** - Um jogador membro só vê as próprias fichas e os anexos públicos.|R0009]].

  

> **Documentos relacionados**: jogadores só podem ser adicionados a uma campanha se já estiverem vinculados ao GM (ver "[[Requisitos - Convite de Jogador]]"). Fichas de personagem criadas aqui seguem "[[Requisitos - Ficha de Personagem]]". Itens anexáveis vêm de "[[Requisitos - Catálogo de Itens e Equipamentos]]". Fichas de criatura e de NPC anexáveis vêm de "Requisitos - Ficha de Criaturas" e "Requisitos - Ficha de NPCs" (documentos ainda não detalhados).

  

> **Convenção de campos**: salvo indicação em contrário, todo campo tem como valor padrão o valor salvo no banco de dados; quando o valor for NULL, o campo exibe seu *placeholder* em itálico.

  

# **R0001** - O GM deve poder criar uma campanha.

**Descrição**: Um formulário com os campos **Nome** (text input) e **Descrição** (texto livre) cria uma nova campanha vinculada ao GM. Um GM pode ter múltiplas campanhas simultâneas.

  

# **R0002** - As campanhas do GM devem ser listadas.

**Descrição**: Uma lista exibe todas as campanhas criadas pelo GM, com Nome e um link para abrir cada uma.

  

# **R0003** - O GM deve poder adicionar jogadores como membros da campanha.

**Descrição**: Dentro de uma campanha, o GM escolhe, entre os jogadores já vinculados à sua conta (ver "[[Requisitos - Convite de Jogador]]"), quais fazem parte dela. Um jogador pode ser membro de mais de uma campanha do mesmo GM.

  

# **R0004** - O GM deve poder criar fichas de personagem para os membros da campanha.

**Descrição**: A partir da tela da campanha, o GM cria uma ficha de personagem (ver "[[Requisitos - Ficha de Personagem]]") escolhendo qual jogador membro é o dono dela. A ficha nasce vinculada tanto a essa campanha quanto a esse jogador — é essa vinculação que faz a ficha aparecer na coluna "campanha" da lista de fichas no painel do jogador (ver `PAINEL DO JOGADOR` no "[[01 - Visão Geral.canvas]]").

  

# **R0005** - A campanha deve ter um diário com entradas datadas.

**Descrição**: O GM pode adicionar entradas ao diário da campanha, cada uma com **data/hora** (preenchida automaticamente no momento da criação), **texto** e, opcionalmente, **imagens**. O GM pode editar ou excluir qualquer entrada já publicada.

  

# **R0006** - O GM deve poder anexar itens, fichas de criatura, fichas de NPC e entradas do Banco de Magias e Habilidades à campanha.

**Descrição**: De dentro da campanha, o GM anexa conteúdo já existente: itens do Catálogo de Itens e Equipamentos, fichas do Bestiário (Fichas de Criaturas), Fichas de NPCs e entradas do "[[Requisitos - Banco de Magias e Habilidades]]". Anexar não duplica o registro original — a campanha guarda uma referência a ele.

  

# **R0007** - O GM deve poder adicionar imagens avulsas à campanha.

**Descrição**: Além dos anexos de R0006, o GM pode fazer upload direto de imagens para a campanha, sem que elas venham de um catálogo de origem.

  

# **R0008** - Cada anexo da campanha deve ter um toggle público/privado individual.

**Descrição**: Todo anexo (item, entrada do Banco de Magias e Habilidades ou imagem, ver R0006 e R0007) tem um controle público/privado, definido pelo GM item a item. O valor padrão ao anexar é **privado**.

**Exceção**: anexos do tipo Ficha de NPC ou Ficha de Criatura não usam esse toggle único — em vez disso, o GM liga/desliga Nome e Imagem independentemente (ver "[[Requisitos - Ficha de NPCs]]" R0004 e "[[Requisitos - Ficha de Criaturas]]" R0003). O restante dessas fichas nunca é visível a jogadores, nem com o anexo "público".

  

# **R0009** - Um jogador membro só vê as próprias fichas e os anexos públicos.

**Descrição**: Um jogador que é membro da campanha só tem acesso, dentro dela, às fichas de personagem das quais é dono (R0004), às fichas de NPC/Criatura que lhe foram concedidas (R0010), e aos anexos marcados como públicos (R0008). Anexos privados e o diário (R0005) são visíveis apenas ao GM.

  

# **R0010** - O GM deve poder conceder fichas de NPC ou Criatura a jogadores membros.

**Descrição**: Útil para pets e invocações de jogadores. De dentro da campanha, o GM escolhe um jogador membro e concede a ele uma Ficha de NPC ou de Criatura, de uma das duas formas:

- **Cópia de uma existente**: o GM escolhe uma ficha já cadastrada no Bestiário/NPCs do GM (ver "[[Requisitos - Ficha de NPCs]]" e "[[Requisitos - Ficha de Criaturas]]"); uma cópia independente é criada para o jogador — editar a cópia não afeta o original, e vice-versa.
- **Em branco**: uma ficha nova, vazia, do tipo escolhido (NPC ou Criatura), é criada já vinculada ao jogador.

Em ambos os casos, a partir da concessão a ficha passa a ter um **jogador dono** e segue o mesmo modelo de edição de uma Ficha de Personagem (o jogador dono edita os campos livremente; criação e exclusão continuam exclusivas do GM) — mantendo, porém, a estrutura de campos de NPC ou Criatura (ver "[[Requisitos - Ficha de NPCs]]" R0001 e "[[Requisitos - Ficha de Criaturas]]" R0001, que descrevem o modelo padrão sem jogador dono). No painel do jogador, fichas concedidas aparecem numa seção própria, separada da lista de Fichas de Personagem (ex: "Meus Companheiros"), já que têm estrutura de campos diferente.

  

# **R0011** - A campanha deve ter Notas Secretas endereçadas a um ou mais jogadores.

**Descrição**: Diferente do diário (R0005, visível só ao GM) e do Diário da Ficha de Personagem (ver "[[Requisitos - Ficha de Personagem]]" 6, visível ao jogador dono e ao GM), uma Nota Secreta é dirigida a um sub-grupo específico de jogadores da campanha — útil para pistas ou informações que só alguns personagens devem saber.

De dentro da campanha, o GM cria uma Nota Secreta escolhendo um ou mais jogadores membros como destinatários. Mesmo padrão de entrada dos outros diários: **Data/hora** (automática), **Texto** e **Imagens** (opcionais). O GM pode editar ou excluir uma Nota Secreta já publicada.

Uma Nota Secreta só é visível ao GM e aos jogadores destinatários escolhidos — os demais membros da campanha não a veem, nem sabem que ela existe. É via de mão única: o jogador só lê, não há resposta pelo sistema.

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

  

# **R0006** - O GM deve poder anexar itens, fichas de criatura, fichas de NPC e entradas do Banco de Magias e Habilidades e do Banco de Runas à campanha.

**Descrição**: De dentro da campanha, o GM anexa conteúdo já existente: itens do Catálogo de Itens e Equipamentos, fichas do Bestiário (Fichas de Criaturas), Fichas de NPCs e entradas do "[[Requisitos - Banco de Magias e Habilidades]]" e do "[[Requisitos - Banco de Runas]]". Anexar não duplica o registro original — a campanha guarda uma referência a ele.

Os campos de busca usados para anexar (item, Magia/Habilidade, Runa, Ficha de NPC e Ficha de Criatura) não listam o que já está anexado à campanha; ao remover um anexo, o registro volta a aparecer na busca.

A lista de anexos da campanha é **agrupada por tipo**, sempre nesta ordem: Itens, Magias/Habilidades, Runas, Imagens, NPCs e Criaturas. Cada grupo é um painel recolhível com a contagem no título, e um tipo sem anexos não aparece. Dentro do grupo os anexos ficam em ordem alfabética, em linhas de colunas fixas (miniatura, nome, visibilidade, ação); as Imagens são exibidas como uma galeria, na ordem em que foram anexadas.

Cada grupo tem os seus próprios filtros, que afetam só ele: busca por nome (exceto Imagens), visibilidade (públicos/privados — um NPC ou uma Criatura conta como público quando o Nome ou a Imagem é público, ver R0008) e os critérios do tipo — **Itens**: tipo de item e Subcategoria; **Magias/Habilidades**: tipo e Grau; **Runas**: Grau e Disciplina. As opções de cada filtro são os valores presentes nos anexos do grupo.

  

# **R0007** - O GM deve poder adicionar imagens avulsas à campanha.

**Descrição**: Além dos anexos de R0006, o GM pode fazer upload direto de imagens para a campanha, sem que elas venham de um catálogo de origem.

  

# **R0008** - Cada anexo da campanha deve ter um toggle público/privado individual.

**Descrição**: Todo anexo (item, entrada do Banco de Magias e Habilidades, entrada do Banco de Runas ou imagem, ver R0006 e R0007) tem um controle público/privado, definido pelo GM item a item. O valor padrão ao anexar é **privado**.

**Exceção**: anexos do tipo Ficha de NPC ou Ficha de Criatura não usam esse toggle único — em vez disso, o GM liga/desliga Nome e Imagem independentemente (ver "[[Requisitos - Ficha de NPCs]]" R0004 e "[[Requisitos - Ficha de Criaturas]]" R0003). O restante dessas fichas nunca é visível a jogadores, nem com o anexo "público".

  

# **R0009** - Um jogador membro só vê as próprias fichas e os anexos públicos.

**Descrição**: Um jogador que é membro da campanha só tem acesso, dentro dela, às fichas de personagem das quais é dono (R0004), às fichas de NPC/Criatura que lhe foram concedidas (R0010), e aos anexos marcados como públicos (R0008). Anexos privados e o diário (R0005) são visíveis apenas ao GM. Os anexos públicos aparecem para o jogador com a mesma organização da lista do GM (R0006) — agrupados por tipo, com a galeria de Imagens e os mesmos filtros —, exceto o filtro de visibilidade e os controles de edição.

  

# **R0010** - O GM deve poder conceder fichas de NPC ou Criatura a jogadores membros.

**Descrição**: Útil para pets e invocações de jogadores. De dentro da campanha, o GM escolhe um jogador membro e concede a ele uma Ficha de NPC ou de Criatura, de uma das duas formas:

- **Cópia de uma existente**: o GM escolhe uma ficha já cadastrada no Bestiário/NPCs do GM (ver "[[Requisitos - Ficha de NPCs]]" e "[[Requisitos - Ficha de Criaturas]]"); uma cópia independente é criada para o jogador — editar a cópia não afeta o original, e vice-versa.
- **Em branco**: uma ficha nova, vazia, do tipo escolhido (NPC ou Criatura), é criada já vinculada ao jogador.

Em ambos os casos, a partir da concessão a ficha passa a ter um **jogador dono** e segue o mesmo modelo de edição de uma Ficha de Personagem (o jogador dono edita os campos livremente; criação e exclusão continuam exclusivas do GM) — mantendo, porém, a estrutura de campos de NPC ou Criatura (ver "[[Requisitos - Ficha de NPCs]]" R0001 e "[[Requisitos - Ficha de Criaturas]]" R0001, que descrevem o modelo padrão sem jogador dono). No painel do jogador, fichas concedidas aparecem numa seção própria, separada da lista de Fichas de Personagem (ex: "Meus Companheiros"), já que têm estrutura de campos diferente.

Na **cópia de uma existente**, cada Magia/Habilidade da ficha copiada fica **anexada à campanha como pública** (R0008), para o jogador vê-la e reaproveitá-la ("[[Requisitos - Banco de Magias e Habilidades]]" R0003). O anexo usa a entrada do banco de onde aquela Magia/Habilidade veio — nenhuma entrada nova é criada; se ela já estava anexada como privada, passa a pública. Uma Magia/Habilidade antiga, sem vínculo com o banco, reaproveita uma entrada idêntica do banco do GM (mesmo Nome, Tipo, Grau e Descrição); só na falta dela é criada uma entrada nova, à qual a cópia passa a ficar vinculada.

  

# **R0011** - A campanha deve ter Notas Secretas endereçadas a um ou mais jogadores.

**Descrição**: Diferente do diário (R0005, visível só ao GM) e do Diário da Ficha de Personagem (ver "[[Requisitos - Ficha de Personagem]]" 6, visível ao jogador dono e ao GM), uma Nota Secreta é dirigida a um sub-grupo específico de jogadores da campanha — útil para pistas ou informações que só alguns personagens devem saber.

De dentro da campanha, o GM cria uma Nota Secreta escolhendo um ou mais jogadores membros como destinatários. Mesmo padrão de entrada dos outros diários: **Data/hora** (automática), **Texto** e **Imagens** (opcionais). O GM pode editar ou excluir uma Nota Secreta já publicada.

Uma Nota Secreta só é visível ao GM e aos jogadores destinatários escolhidos — os demais membros da campanha não a veem, nem sabem que ela existe. É via de mão única: o jogador só lê, não há resposta pelo sistema.

# **R0012** - Conteúdo criado por um jogador é anexado à campanha automaticamente como público.

> Uma Magia/Habilidade ou uma Runa que um jogador cria em sua ficha (Requisitos - Ficha de Personagem R0003, Requisitos - Banco de Magias e Habilidades R0001, Requisitos - Banco de Runas R0007), e uma Imagem que um jogador envia a partir de sua ficha, são anexadas à campanha correspondente automaticamente como **públicas** — sem etapa de aprovação do GM. Isso não se aplica a Itens: um jogador nunca cria um Item, então este anexo automático não existe para eles.
>
> A tela de gerenciamento da campanha (Membros, Anexos, Diário, Notas Secretas, Conceder Ficha, Detalhes) nunca é alcançável por um jogador — a tela dele é a descrita em R0009.

# **R0013** - Escolher a Equipagem inicial anexa e publica seus itens automaticamente na campanha.

> Exceção equivalente à de R0012, mas do lado do GM: quando uma Ficha de Personagem ou uma Ficha de NPC concedida escolhe um kit de Equipagem inicial, cada Item que o kit concede (fixo ou de slot de escolha) é anexado à campanha correspondente e marcado **público** automaticamente — sem passar pelo fluxo manual de anexar-e-depois-publicar de R0006/R0008. A justificativa é que Itens de Equipagem são "de conhecimento geral": não há razão para o GM escondê-los dos demais jogadores só porque vieram de um kit em vez de terem sido anexados manualmente.
>
> Isso vale apenas para o Item em si (fica público na aba de Anexos da campanha); não altera nada sobre a ficha que recebeu a Equipagem, nem sobre outros anexos já existentes.

# **R0014** - A aba Detalhes deve permitir ao GM definir um Bônus de carga para os personagens da campanha.

> Na aba "Detalhes", seção "Editar Campanha", o GM edita o campo numérico **Bônus de carga dos personagens** (decimal, passo 1, **pode ser negativo**; padrão 0; limitado a −1000..1000, com erro 400 e mensagem em português fora disso), salvo pelo mesmo autosave de Nome e Descrição. Um ⓘ explica: o valor é somado ao Peso Máximo do inventário de todos os personagens da campanha, um número negativo reduz a capacidade, e NPCs e Criaturas não são afetados.
>
> O bônus é somado ao Peso Máximo de toda Ficha de Personagem da campanha (ver "[[Requisitos - Ficha de Personagem]]" 2.b, Movimentação) — tanto no Peso exibido na aba "Posses" quanto na penalidade de sobrepeso da Movimentação —, e o Peso Máximo resultante nunca fica abaixo de 0. Quando o bônus é diferente de 0, a ficha mostra a legenda "inclui +N da campanha" (ou "inclui −N da campanha") ao lado do Peso. Só o GM dono da campanha pode alterá-lo.

# **R0015** - O jogador deve ser notificado quando recebe uma Nota Secreta.

**Descrição**: Quando o GM publica uma Nota Secreta (R0011), cada jogador destinatário que estiver com o sistema aberto recebe, em qualquer página, um aviso visual e um aviso sonoro (`harp_notification.mp3`). O aviso visual informa a campanha de origem e é clicável: leva à aba Notas Secretas da tela do jogador naquela campanha (R0009). O texto da nota não aparece no aviso.

O jogador também vê um contador de Notas Secretas não lidas: um indicador na barra superior e o total no item "Minhas Campanhas" do menu, a quantidade de cada campanha na lista de Minhas Campanhas, e a quantidade da campanha na própria aba Notas Secretas. Uma nota deixa de ser não lida quando o jogador abre a aba Notas Secretas daquela campanha — todas as notas dele ali passam a lidas de uma vez. O contador cobre quem não estava com o sistema aberto no momento do envio; o aviso sonoro não é reproduzido depois.

Se o GM editar uma Nota Secreta e acrescentar um destinatário, esse jogador é notificado como se a nota fosse nova; os destinatários que já existiam não são notificados de novo e conservam o estado de leitura. Editar apenas o texto ou as imagens não notifica ninguém. Excluir a nota, remover um destinatário ou excluir a campanha apenas corrige o contador de quem a perdeu.

Vale a mesma restrição de R0011: quem não é destinatário não recebe aviso nem contagem.

> O navegador pode bloquear o aviso sonoro enquanto o jogador não tiver interagido com a página; o aviso visual aparece de qualquer forma.

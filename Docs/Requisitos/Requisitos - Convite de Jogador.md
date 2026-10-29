> Esta página se baseia no bloco **PAINEL DO *GM*** (ferramenta 1, "Convidar jogador") do "[[01 - Visão Geral]]".

  

> **Modelo de edição**: apenas o **GM** tem acesso a esta página. Um jogador nunca vê a lista de códigos nem o filtro de busca de jogadores.

  

> **Convenção de campos**: salvo indicação em contrário, todo campo tem como valor padrão o valor salvo no banco de dados; quando o valor for NULL ou não fizer sentido para o estado atual do registro, o campo exibe um travessão (**—**), para não gerar confusão ou interpretações erradas.

  

# **R0001** - O GM deve poder gerar um código de convite.

**Descrição**: A página exibe um botão "Gerar código de convite". Ao ser clicado, o sistema gera um código alfanumérico de 8 caracteres, único no sistema, vinculado à conta do GM que o gerou. O código nasce no estado **Ativo** e tem validade de **48 horas** a partir do momento da geração.

  

# **R0002** - Os códigos gerados devem ser listados.

**Descrição**: Abaixo do botão de geração, uma lista exibe todos os códigos já gerados pelo GM, com as colunas:

- **Código**: o valor alfanumérico gerado.
- **Status**: uma tag indicando o estado atual do código — **Ativo**, **Usado**, **Expirado** (R0004) ou **Revogado** (ver [[#**R0005** - O GM deve poder revogar manualmente um código Ativo.|R0005]]).
- **Gerado em**: data e hora da geração do código.
- **Expira em**: data e hora em que o código deixa de ser válido (Gerado em + 48h). Exibe travessão quando o código já não está mais **Ativo**.
- **Resgatado por**: nickname e e-mail do jogador que usou o código para se cadastrar. Exibe travessão enquanto o código não for usado.
- **Resgatado em**: data e hora do cadastro do jogador que resgatou o código. Exibe travessão enquanto o código não for usado.

  

# **R0003** - Ao ser resgatado, o código deve vincular o jogador ao GM.

**Descrição**: Quando um jogador usa o código no cadastro (ver aba "Cadastro de jogadores" no `PÁGINA DE CADASTRO` do "[[01 - Visão Geral]]"), a conta criada é automaticamente vinculada à conta do GM que gerou aquele código. Na lista de códigos (R0002), o status muda para **Usado** e as colunas **Resgatado por** e **Resgatado em** passam a exibir os dados do jogador e o momento do resgate. Um código **Usado** não pode ser resgatado novamente.

  

# **R0004** - Um código Ativo deve expirar automaticamente após 48 horas.

**Descrição**: Se um código permanecer no estado **Ativo** por 48 horas sem ser resgatado, seu status muda automaticamente para **Expirado**. Um código **Expirado** não pode mais ser usado para cadastro. As colunas **Gerado em** e **Expira em** continuam exibindo os valores originais; **Resgatado por** e **Resgatado em** exibem travessão.

  

# **R0005** - O GM deve poder revogar manualmente um código Ativo.

**Descrição**: Enquanto um código estiver no estado **Ativo**, a lista exibe uma ação "Revogar" para aquela linha. Ao confirmar, o status do código muda para **Revogado** e ele não pode mais ser resgatado, independentemente do tempo restante até a expiração. Um código já **Usado**, **Expirado** ou **Revogado** não exibe mais essa ação.

  

# **R0006** - O GM deve poder filtrar/buscar jogadores vinculados a ele.

**Descrição**: A página contém um campo de busca que filtra os jogadores vinculados à conta do GM (isto é, que se cadastraram usando um dos seus códigos) por **nickname** ou **e-mail**. A busca é usada como base para a ação de redefinição de senha (ver [[#**R0007** - O GM deve poder redefinir a senha de um jogador.|R0007]]) e para as ações de conta (R0008 e R0009).

A lista e a busca mostram os jogadores com conta **Ativa**, cada um com as ações **Desativar** e **Excluir**. Uma seção separada, **Desativados**, lista os jogadores com conta desativada, com as ações **Reativar** e **Excluir**. Jogadores com conta excluída não aparecem em lugar nenhum desta página nem em qualquer seletor de jogador (concessão, Notas Secretas, membros).

  

# **R0007** - O GM deve poder redefinir a senha de um jogador.

**Descrição**: A partir de um jogador encontrado pelo filtro (R0006), o GM pode abrir um formulário de redefinição de senha com os campos **Nova senha** e **Confirmação da nova senha**. O GM não precisa informar a senha atual do jogador para concluir a redefinição. Ao confirmar, a senha do jogador é substituída pela informada; cabe ao GM comunicar a nova senha ao jogador por fora do sistema. A redefinição vale para contas ativas e desativadas, nunca para excluídas.

  

# **R0008** - O GM deve poder desativar e reativar a conta de um jogador.

> **Quem pode**: só o GM que convidou o jogador (o vínculo de R0003). Para qualquer outro GM a conta do jogador não existe. Só contas de jogador têm esse estado: **Ativa** (padrão), **Desativada** e **Excluída** (ver "[[Requisitos - Modelo de Dados]]" 1).

**Descrição**: **Desativar** (de uma conta Ativa) tira o jogador de jogo temporariamente, com confirmação. Em uma única operação — nada fica pela metade se algo falhar — o sistema:

- remove o jogador de **todas** as campanhas do GM em que ele é membro, com os mesmos efeitos de "[[Requisitos - Campanha]]" R0018 (fichas ao cemitério pelos motivos de "[[Requisitos - Ficha de Personagem]]" R0010, concessões de volta ao GM, saída dos Encontros ativos);
- encerra as sessões abertas do jogador: a conta não consegue mais entrar nem usar o sistema, mesmo com uma sessão ainda válida. O login é recusado com a mensagem "Esta conta está desativada. Fale com o seu GM." (ver "[[Requisitos - Login e Cadastro]]" R0002).

A conta desativada deixa de aparecer na lista de adicionar membro à campanha (Campanha R0003) e passa à seção **Desativados** de R0006.

**Reativar** (de uma conta Desativada) volta a conta a Ativa e libera o login. **Nenhuma campanha é restaurada**: o GM adiciona o jogador de novo a cada campanha (Campanha R0003), e as fichas dele saem do cemitério sozinhas ao voltar a ser membro, se não estiverem marcadas como mortas. As concessões não voltam sozinhas (Campanha R0019).

  

# **R0009** - O GM deve poder excluir a conta de um jogador.

**Descrição**: **Excluir** (de uma conta Ativa ou Desativada) tira o jogador de vez e é **irreversível**. A confirmação é forte: o GM digita o **apelido** do jogador para confirmar. Em uma única operação o sistema faz o que Desativar faz (R0008) e então:

- a conta passa a **Excluída**. A linha da conta **não é apagada**, porque fichas, logs, notas e anexos apontam para ela; ela vira um registro de conta excluída, sem e-mail, senha nem demais dados de login, de modo que ninguém entra com ela;
- o **e-mail fica livre**: o mesmo e-mail pode se cadastrar de novo, como uma conta nova ("[[Requisitos - Login e Cadastro]]" R0003);
- o **apelido é mantido e continua reservado**: ninguém pode se cadastrar com ele (Login e Cadastro R0003), e ele identifica de quem era cada ficha.

Todas as fichas do jogador vão para o cemitério e lá ficam, pelo motivo "Conta do jogador excluída". Imagens enviadas por ele, diários das fichas e o log das fichas ficam como estão. A conta excluída não aparece em nenhuma lista nem seletor de jogador (R0006).

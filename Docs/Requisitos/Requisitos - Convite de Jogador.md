> Esta página se baseia no bloco **PAINEL DO *GM*** (ferramenta 1, "Convidar jogador") do "[[01 - Visão Geral.canvas]]".

  

> **Modelo de edição**: apenas o **GM** tem acesso a esta página. Um jogador nunca vê a lista de códigos nem o filtro de busca de jogadores.

  

> **Convenção de campos**: salvo indicação em contrário, todo campo tem como valor padrão o valor salvo no banco de dados; quando o valor for NULL ou não fizer sentido para o estado atual do registro, o campo exibe um travessão (**—**), para não gerar confusão ou interpretações erradas.

  

# **R0001** - O GM deve poder gerar um código de convite.

**Descrição**: A página exibe um botão "Gerar código de convite". Ao ser clicado, o sistema gera um código alfanumérico de 8 caracteres, único no sistema, vinculado à conta do GM que o gerou. O código nasce no estado **Ativo** e tem validade de **48 horas** a partir do momento da geração.

  

# **R0002** - Os códigos gerados devem ser listados.

**Descrição**: Abaixo do botão de geração, uma lista exibe todos os códigos já gerados pelo GM, com as colunas:

- **Código**: o valor alfanumérico gerado.
- **Status**: uma tag indicando o estado atual do código — **Ativo**, **Usado** ou **Revogado** (ver [[#**R0005** - O GM deve poder revogar manualmente um código Ativo.|R0005]]).
- **Gerado em**: data e hora da geração do código.
- **Expira em**: data e hora em que o código deixa de ser válido (Gerado em + 48h). Exibe travessão quando o código já não está mais **Ativo**.
- **Resgatado por**: nickname e e-mail do jogador que usou o código para se cadastrar. Exibe travessão enquanto o código não for usado.
- **Resgatado em**: data e hora do cadastro do jogador que resgatou o código. Exibe travessão enquanto o código não for usado.

  

# **R0003** - Ao ser resgatado, o código deve vincular o jogador ao GM.

**Descrição**: Quando um jogador usa o código no cadastro (ver aba "Cadastro de jogadores" no `PÁGINA DE CADASTRO` do "[[01 - Visão Geral.canvas]]"), a conta criada é automaticamente vinculada à conta do GM que gerou aquele código. Na lista de códigos (R0002), o status muda para **Usado** e as colunas **Resgatado por** e **Resgatado em** passam a exibir os dados do jogador e o momento do resgate. Um código **Usado** não pode ser resgatado novamente.

  

# **R0004** - Um código Ativo deve expirar automaticamente após 48 horas.

**Descrição**: Se um código permanecer no estado **Ativo** por 48 horas sem ser resgatado, seu status muda automaticamente para **Expirado**. Um código **Expirado** não pode mais ser usado para cadastro. As colunas **Gerado em** e **Expira em** continuam exibindo os valores originais; **Resgatado por** e **Resgatado em** exibem travessão.

  

# **R0005** - O GM deve poder revogar manualmente um código Ativo.

**Descrição**: Enquanto um código estiver no estado **Ativo**, a lista exibe uma ação "Revogar" para aquela linha. Ao confirmar, o status do código muda para **Revogado** e ele não pode mais ser resgatado, independentemente do tempo restante até a expiração. Um código já **Usado**, **Expirado** ou **Revogado** não exibe mais essa ação.

  

# **R0006** - O GM deve poder filtrar/buscar jogadores vinculados a ele.

**Descrição**: A página contém um campo de busca que filtra os jogadores vinculados à conta do GM (isto é, que se cadastraram usando um dos seus códigos) por **nickname** ou **e-mail**. A busca é usada como base para a ação de redefinição de senha (ver [[#**R0007** - O GM deve poder redefinir a senha de um jogador.|R0007]]).

  

# **R0007** - O GM deve poder redefinir a senha de um jogador.

**Descrição**: A partir de um jogador encontrado pelo filtro (R0006), o GM pode abrir um formulário de redefinição de senha com os campos **Nova senha** e **Confirmação da nova senha**. O GM não precisa informar a senha atual do jogador para concluir a redefinição. Ao confirmar, a senha do jogador é substituída pela informada; cabe ao GM comunicar a nova senha ao jogador por fora do sistema.

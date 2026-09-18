> Esta página se baseia nos blocos **LANDING PAGE**, **PÁGINA DE LOGIN** e **PÁGINA DE CADASTRO** do "[[01 - Visão Geral.canvas]]". Formaliza em R000x o que até agora só existia como texto solto nesses três blocos.

  

> **Modelo de acesso**: Landing Page, Login, Cadastro e o "[[Requisitos - Livro de Regras]]" são as únicas telas do sistema acessíveis **sem autenticação**. Toda outra tela exige um JWT válido (ver "[[Requisitos - Técnico]]" R0004).

  

# **R0001** - A Landing Page apresenta o sistema e leva a Login ou Cadastro.

**Descrição**: Uma página com uma explicação breve do sistema, uma sinopse da lore do RPG, e dois botões: **Cadastrar** e **Entrar**, levando respectivamente às páginas de Cadastro (R0003) e Login (R0002). Acima desses dois botões, em destaque, um terceiro botão leva ao "[[Requisitos - Livro de Regras]]" — a única das três ações que não exige cadastro ou login para ser usada.

  

# **R0002** - Login com Nickname e Senha.

**Descrição**: Formulário com os campos **Nickname** e **Senha**, e um botão de login.

- Sucesso: o usuário é autenticado e redirecionado ao painel correspondente ao seu papel — GM vai para o painel de GM, Jogador vai para o painel de jogador (ver `01 - Visão Geral.canvas`).
- Falha: mensagem genérica de credenciais inválidas — não indica se foi o Nickname ou a Senha que estava incorreto, para não vazar quais nicknames existem no sistema.

  

# **R0003** - Cadastro em duas abas: Gamemaster e Jogador.

**Descrição**: Uma página com duas abas:

- **Cadastro do Gamemaster**: campos **Nickname**, **Email**, **Senha**, **Confirmação da Senha**, e um botão de cadastrar.
- **Cadastro de Jogador**: os mesmos campos da aba de GM, mais um campo extra: **Código de acesso** — o código de convite gerado por um GM (ver "[[Requisitos - Convite de Jogador]]" R0001). Ao cadastrar, o código é resgatado automaticamente, seguindo o comportamento de "[[Requisitos - Convite de Jogador]]" R0003 (o jogador é vinculado ao GM que gerou o código).

Validações, em ambas as abas:

- **Nickname**: obrigatório, único no sistema.
- **Email**: obrigatório, único no sistema, formato de e-mail válido.
- **Senha**: segue a política de senha padrão do ASP.NET Core Identity (ver "[[Requisitos - Técnico]]" R0004) — não há regra adicional definida pelo sistema além disso.
- **Confirmação da Senha**: deve ser idêntica à Senha.
- Na aba de Jogador, o **Código de acesso** deve corresponder a um código **Ativo** (ver "[[Requisitos - Convite de Jogador]]" R0002) — código inválido, expirado, já usado ou revogado impede o cadastro, com uma mensagem indicando o motivo.

Sucesso: o usuário é autenticado automaticamente (sem precisar logar de novo) e redirecionado ao painel correspondente ao seu papel.

  

# **R0004** - Logout.

**Descrição**: Um controle de logout, acessível de qualquer tela autenticada, revoga o refresh token atual (ver "[[Requisitos - Técnico]]" e a tabela `RefreshTokens` em "[[Requisitos - Modelo de Dados]]") e redireciona à Landing Page (R0001).

  

# **R0005** - Recuperação de senha de GM via console.

**Descrição**: Não há serviço de e-mail configurado, então a recuperação de senha de uma conta de GM não é self-service — é feita por um operador com acesso ao servidor, via o comando de console descrito em "[[Requisitos - Técnico]]" R0008.

- O comando recebe o e-mail da conta e devolve o **Nickname** e uma **senha temporária** gerada na hora, que o operador comunica ao GM por fora do sistema.
- Rodar o comando também revoga todos os refresh tokens ativos daquele GM (Requisitos - Técnico e a tabela `RefreshTokens` em "[[Requisitos - Modelo de Dados]]") — toda sessão já aberta é encerrada, já que a senha anterior é considerada perdida/comprometida.
- O GM loga normalmente (R0002) com a senha temporária. Enquanto não trocar a senha, o painel (GM ou Jogador, ver `01 - Visão Geral.canvas`) mostra só um formulário obrigatório de nova senha (**Nova senha** e **Confirmação da nova senha**) — nenhuma outra ação do painel é liberada até a troca ser concluída.
- Não há prazo de expiração para a senha temporária: ela vale até ser trocada pelo GM, ou até o comando ser rodado de novo (o que gera outra e substitui a anterior).

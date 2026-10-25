> Mapa das páginas do app e de qual documento de Requisitos especifica cada uma. Substitui o antigo `01 - Visão Geral.canvas` (2.0.0), que só tinha os blocos de login, cadastro e painéis. Os nomes dos blocos abaixo são os mesmos do canvas, porque os demais documentos se referem a eles (ex.: "PAINEL DO *GM*, ferramenta 3"). Documentos não-funcionais, sem página própria: [[Requisitos - Técnico]] e [[Requisitos - Modelo de Dados]].

# Fluxo

```
LANDING PAGE ──► PÁGINA DE LOGIN ────┬──► PAINEL DO GM ─────► ferramentas do GM
             └─► PÁGINA DE CADASTRO ─┘
                                     └──► PAINEL DO JOGADOR ─► fichas e campanhas do jogador
Livro de Regras: aberto a todos, logado ou não.
```

# LANDING PAGE

Explicação breve do sistema, sinopse da lore e dois botões: cadastrar e entrar. Ver [[Requisitos - Login e Cadastro]].

# PÁGINA DE LOGIN

Nickname e senha; o GM vai para o PAINEL DO *GM*, o jogador para o PAINEL DO JOGADOR. Ver [[Requisitos - Login e Cadastro]].

# PÁGINA DE CADASTRO

Duas abas: **Cadastro do Gamemaster** (nickname, e-mail, senha, confirmação) e **Cadastro de jogadores** (o mesmo, mais o código de convite do GM). Ao concluir, cada um vai para o seu painel. Ver [[Requisitos - Login e Cadastro]] e [[Requisitos - Convite de Jogador]].

# PAINEL DO JOGADOR

- Lista das fichas do jogador (imagem, nome, nível, campanha) com o botão de abrir a ficha: [[Requisitos - Ficha de Personagem]]. Uma ficha só é criada ou excluída pelo GM.
- **Cemitério**: seção separada, abaixo da lista de fichas, com as fichas do jogador que estão no cemitério (imagem, nome, nível, campanha), abertas em modo leitura; não aparece quando vazia: [[Requisitos - Ficha de Personagem]] R0010.
- **Minhas Campanhas**: as campanhas de que é membro, com os anexos públicos, as fichas de NPC/Criatura concedidas a ele e as Notas Secretas: [[Requisitos - Campanha]].

# PAINEL DO *GM*

Ferramentas do GM (a numeração é a do canvas original; as demais vieram depois):

1. **Convidar jogador** (códigos de convite e a busca/redefinição de senha dos jogadores do GM): [[Requisitos - Convite de Jogador]].
2. **Bestiário do *GM***: [[Requisitos - Ficha de Criaturas]].
3. **NPCs do *GM***: [[Requisitos - Ficha de NPCs]].
4. **Itens e Equipamentos**: [[Requisitos - Catálogo de Itens e Equipamentos]].
5. **Campanhas**: membros, fichas concedidas, anexos públicos/privados, diário do GM, Notas Secretas e a aba **Cemitério** (fichas de personagem fora de jogo, em modo leitura): [[Requisitos - Campanha]] R0017. Dentro de uma campanha, os encontros: [[Requisitos - Gerenciador de Encontros]].
6. **Banco de Magias e Habilidades**: [[Requisitos - Banco de Magias e Habilidades]].
7. **Banco de Runas**: [[Requisitos - Banco de Runas]].
8. **Habilidades Raciais**: [[Requisitos - Habilidades Raciais]].
9. **Auditoria de Regras** (só para a conta de GM designada Auditor de Regras): [[Requisitos - Auditoria de Regras]].

# Consulta de regras

- **Livro de Regras** (menu, para todos): [[Requisitos - Livro de Regras]].
- **Compêndio de Regras** (busca no Sistema RPG; a página existe, mas saiu do menu em favor do Livro de Regras): [[Requisitos - Compêndio de Regras]].

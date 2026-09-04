> Esta página não corresponde a nenhum bloco do "[[01 - Visão Geral.canvas]]" ainda — é uma ferramenta de consulta nova.

  

> **Natureza do conteúdo**: ao contrário do Catálogo de Itens ou do Banco de Magias e Habilidades, o Compêndio não é curado por cada GM — é o mesmo conteúdo fixo para todo mundo, extraído diretamente dos documentos em `Docs/Sistema RPG`. Ninguém edita o Compêndio pela interface; ele muda quando os documentos fonte mudam.

  

> **Modelo de acesso**: GM e jogadores têm acesso de leitura ao Compêndio.

  

> **Status**: a entrada de menu do Compêndio foi ocultada em favor do "[[Requisitos - Livro de Regras]]" — a página, o endpoint e o índice de busca abaixo continuam existindo no código (nada foi removido), só deixaram de ser alcançáveis pelo menu. `IRulesDataProvider`, que também alimenta cálculos de nível/EAP/graduação em outras partes do sistema, não foi afetado.

  

# **R0001** - O Compêndio deve indexar as regras do sistema para busca textual.

**Descrição**: O conteúdo pesquisável cobre:

- **Características**: nome, descrição e custo de cada característica positiva/negativa (ver "[[Características]]").
- **Efeitos de Graus & Círculos**: os Efeitos Básicos e Especiais nomeados de cada Grau/Círculo, com nome, descrição, custo em PI e pré-requisitos (ver "[[GRAUS & CÍRCULOS]]").
- **Tabelas**: "[[Tabela de Níveis]]", "[[Tabela de Vocação]]", "[[Tabela de Classes]]", "[[Tabela de Arquetipos]]", "[[Tabela de Circulo e Grau por EAP]]" e "[[Tabelas de XP, Atributos, Características e EAP]]".
- **Regras de "[[Ruína RPG - Sistema Básico]]"**: atributos, perícias e progressão, Pontos de Adrenalina e escala de dados, ações em combate, defesa/esquiva, iniciativa, sistema de viagem, e linhagens/variantes.

  

# **R0002** - Os resultados de busca devem indicar categoria e origem.

**Descrição**: Cada resultado exibe a categoria a que pertence (Característica, Efeito, Tabela ou Regra) e, dentro dela, a origem específica (ex: "Efeito — 3º Grau/Círculo III", "Tabela de Níveis — Nível 10"). Selecionar um resultado leva ao conteúdo completo daquele item.

  

# **R0003** - A busca deve poder ser filtrada por categoria.

**Descrição**: Além da busca textual livre (R0001), o usuário pode restringir os resultados a uma ou mais categorias: Características, Efeitos, Tabelas ou Regras.

  

# **R0004** - O conteúdo do Compêndio é somente leitura.

**Descrição**: Nem GM nem jogadores podem editar, criar ou excluir entradas do Compêndio pela interface — ele reflete diretamente os documentos fonte em `Docs/Sistema RPG`.

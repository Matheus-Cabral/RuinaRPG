> Esta página não corresponde a nenhum bloco do "[[01 - Visão Geral.canvas]]" ainda — substitui a entrada de menu que antes apontava para o "[[Requisitos - Compêndio de Regras]]" (ver a nota de status naquele documento).

  

> **Natureza do conteúdo**: assim como o Compêndio, o Livro de Regras não é curado por cada GM — é o mesmo conteúdo fixo para todo mundo, extraído diretamente de documentos em `Docs/Sistema RPG`. Ninguém edita o Livro de Regras pela interface; ele muda quando os documentos fonte mudam.

  

> **Edição**: o conteúdo deixou de ser 100% fixo — ver "[[Requisitos - Auditoria de Regras]]" para como um GM designado Auditor de Regras pode editá-lo (a aba de Características, em particular, passa a refletir o catálogo de "[[Características]]" em vez de uma cópia própria do texto).

  

> **Modelo de acesso**: GM e jogadores têm acesso de leitura — e, ao contrário do Compêndio, também visitantes não autenticados (ver "[[Requisitos - Login e Cadastro]]"), pelo menu lateral ou por um botão em destaque na Landing Page.

  

# **R0001** - O Livro de Regras deve exibir, em abas, o conteúdo completo de quatro documentos do sistema.

**Descrição**: Cada aba renderiza o Markdown de um documento inteiro (não um trecho, ao contrário do Compêndio) como HTML formatado:

- "[[Características]]"
- "[[Ruína RPG - Sistema Básico]]"
- "[[GRAUS & CÍRCULOS]]"
- "[[Tabela de Níveis]]"

  

# **R0002** - A aba de Graus & Círculos deve incluir as imagens de referência do sistema de magia.

**Descrição**: As imagens `Escolas_de_Magia.png` e `Matriz_Elemental.png` (`Docs/Sistema RPG`) aparecem no topo dessa aba, antes das seções de Grau/Círculo — nenhuma das duas tem uma seção correspondente no documento fonte (não existe seção "Escolas de Magia", e a mais próxima de "Matriz Elemental" é só um efeito entre vários, "Encantamento Elemental" do 2º Grau), então funcionam como material de referência para o documento inteiro, não anexadas a uma seção específica.

  

# **R0003** - O conteúdo do Livro de Regras é somente leitura.

**Descrição**: Nem GM nem jogadores podem editar, criar ou excluir conteúdo do Livro de Regras pela interface — ele reflete diretamente os documentos fonte em `Docs/Sistema RPG`.

  

# **R0004** - Cada aba deve ser dividida em seções navegáveis, não um bloco único de texto corrido.

**Descrição**: Documentos longos (até 60 títulos) eram exibidos como um único bloco de HTML contínuo, sem nenhuma forma de navegação interna — difícil de ler e de localizar uma regra específica. Cada documento é dividido em seções a partir de um nível de título escolhido (ver a tabela abaixo), e cada seção é exibida como um cartão visualmente separado, não mais texto corrido:

| Aba | Nível de divisão | Seções resultantes |
|---|---|---|
| Sistema Básico | `##` | 7 (uma por seção numerada do documento) |
| Graus & Círculos | `#` | 9 (uma por Grau/Círculo; os efeitos de cada Grau, que são `##`, continuam dentro do cartão do próprio Grau) |
| Tabela de Níveis | — | nenhuma (o documento não tem títulos — é uma única tabela) |
| Características | `###`, agrupado por `#` | uma por característica (ver R0005) |

Para as abas com seções (Sistema Básico e Graus & Círculos), um sumário fixo ao lado do conteúdo lista o título de cada seção; clicar em uma entrada rola a página até aquela seção. Conteúdo anterior ao primeiro título de divisão (ex: a tabela de custo de Grau, em Graus & Círculos) continua sendo exibido no topo da aba, antes do sumário e das seções.

  

# **R0005** - A aba de Características deve ser uma lista filtrável por nome, não prosa corrida.

**Descrição**: Características tem mais de 50 entradas — um sumário lateral não é útil nesse volume. Em vez disso, a aba exibe um campo de busca no topo; digitar filtra a lista pelo nome da característica (busca parcial, sem diferenciar maiúsculas/minúsculas). As características continuam agrupadas por "Positivas" e "Negativas" (títulos `#` do documento fonte), cada uma exibida como seu próprio cartão.

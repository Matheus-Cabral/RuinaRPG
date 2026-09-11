> Esta página não corresponde a nenhum bloco do "[[01 - Visão Geral.canvas]]" ainda — é um subsistema novo, complementar a "[[Requisitos - Ficha de Personagem]]" (4.a) e §7 de "[[Ruína RPG - Sistema Básico]]".

  

> **Modelo de edição**: pertence à conta do GM (não a uma Campanha específica), assim como o Catálogo e o Banco de Magias. Diferente daqueles dois, tanto a leitura quanto a edição desta página são exclusivas do GM — um jogador nunca navega até aqui; ele só vê o resultado já resolvido na própria Ficha (ver "[[Requisitos - Ficha de Personagem]]" 4.a), calculado pela API a partir do que o GM cadastrou aqui.

  

> **Convenção de campos**: mesma de "[[Requisitos - Ficha de Personagem]]" — valor padrão do banco de dados, placeholder em itálico quando NULL, travessão para campos não aplicáveis.

  

# **R0001** - O GM pode sobrescrever o Nome/Descrição da habilidade racial de qualquer uma das 7 Variantes.

**Descrição**: A página lista as 7 Variantes (Sinir, Laonir, Phylac'tai, Es'Phylauc, Yavos, Koroanos, Alóra — ver §7 de "[[Ruína RPG - Sistema Básico]]"), cada uma com campos **Nome** e **Descrição** editáveis. O valor inicial de cada campo é o texto já descrito nas regras (ex: "Racial (Sobre Voo)" / "Passiva. Capacidade de voo livre." para Yavos/Koroanos) — editar e salvar sobrescreve esse padrão; um botão "Restaurar padrão" apaga a sobrescrita e volta a exibir o valor original das regras.

  

# **R0002** - O GM pode cadastrar a tabela de Arcas usada pela habilidade racial de Sinir/Laonir (Humano).

**Descrição**: O Racial de Sinir e Laonir diz "Role 1d18 na tabela de Arcas" (ver §7 de "[[Ruína RPG - Sistema Básico]]"), mas essa tabela não é uma regra fixa do sistema — é conteúdo livre que cada GM define. A página exibe 18 linhas fixas (uma por resultado de 1 a 18), cada uma com **Nome** e **Descrição** editáveis. Uma linha ainda não preenchida pelo GM aparece vazia; a Ficha de Personagem/NPC (ver "[[Requisitos - Ficha de Personagem]]" 4.a) exibe "Arca não cadastrada." para um número rolado que caia numa linha vazia.

  

# **R0003** - O app nunca rola dados automaticamente.

**Descrição**: Esta página e o campo "Número rolado" da Ficha (4.a) são só cadastro/referência — o app não simula a rolagem do 1d18 em nenhum momento. O jogador rola fisicamente (ou por qualquer outro meio fora do app) e digita o resultado na própria ficha.

  

# **R0004** - O GM pode sobrescrever as opções de Característica Gratuita/Obrigatória de qualquer uma das 7 Variantes.

**Descrição**: Além do Racial (R0001), a página lista, para cada Variante, as duas listas de opções descritas em §7 de "[[Ruína RPG - Sistema Básico]]": **Característica Gratuita** (sempre 2 ou mais opções, escolhidas dentre "[[Características]]") e **Característica Obrigatória** (vazia para Sinir/Laonir — essa Variante não tem essa exigência; uma única opção fixa para Alóra; duas opções para as demais). Cada lista é editável — adicionar, remover ou trocar uma opção (Nome da característica + Especificação, quando a característica exigir uma) — com um botão "Restaurar padrão" que apaga a sobrescrita e volta às opções de §7. O valor de cada opção é o Nome exato de uma característica de "[[Características]]" — a página não valida a existência do nome ao salvar, só na ficha do jogador/NPC, ao tentar consolidar a escolha (ver "[[Requisitos - Ficha de Personagem]]" 5.d).

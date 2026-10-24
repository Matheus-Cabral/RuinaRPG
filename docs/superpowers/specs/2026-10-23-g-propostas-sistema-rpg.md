# G — Propostas de conteúdo para `Docs/Sistema RPG/`

Levantadas na passada "só forma" do sub-projeto G (Task 2). **Nada aqui foi aplicado**: cada item muda
regra, número, nome de jogo ou texto que o app lê, e fica para o usuário decidir. Formato:
`arquivo — trecho — o que parece errado — sugestão`.

Atenção ao aplicar: 14 arquivos são embutidos e lidos pelo app (ver `CLAUDE.md`). Nomes de
Características, Históricos, Efeitos e títulos de seção são chaves de seed/parser; mudar um nome lá
pode criar uma linha nova no banco em vez de renomear a existente.

## Ruína RPG - Sistema Básico.md

1. Sistema Básico — `Racial (Arca): Role 1d18 na tabela de Arcas.` (Sinir e Laonir) — não existe "tabela de Arcas" em `Docs/Sistema RPG/` — criar a tabela ou apontar para onde ela está (wikilink).
2. Sistema Básico — Phylac'tai/Es'Phylauc: `Imunidade a Venenos` — a Característica se chama `Imunidade de Venenos` em [[Características]] — usar o mesmo nome.
3. Sistema Básico — Alóra: `Fraqueza Elemental (fogo)` — não há Característica com esse nome; a mais próxima é `Desvantagem Elemental` (-3, dano dobrado), mas aqui o efeito é "+1 de dano extra" — decidir se é a mesma Característica (e alinhar nome/efeito) ou uma racial própria.
4. Sistema Básico — Alóra, Amplificador Místico: `(ex: de d6 para d8, de d10 para d12)` — a escala de dados do sistema é d10 → d12 → d20 → … (seção 3); d6→d8 não existe nela — trocar o exemplo por degraus da Escala de Dados.
5. Sistema Básico — `Astucia` (atributo, várias vezes; também em [[Formulas]]) — sem acento ("Astúcia" em [[Características]], "Sábio") — padronizar a grafia (conferir antes o nome usado no app).
6. Sistema Básico — Koroanos: `Característica Obrigatória: Distração ou Covarde`, Yavos: `Curioso ou Covarde` — confirmar se a diferença entre as variantes é intencional.
7. Sistema Básico — Iniciativa: a numeração original era `1.`, (exemplo), `3.`, `4.` (o exemplo ocupava o item 2 numa exportação). Na passada de forma o exemplo foi aninhado sob o item 1 e a lista virou 1, 2, 3. Confirmar que não faltava um item 2 de verdade.

## GRAUS & CÍRCULOS.md

8. GRAUS & CÍRCULOS — `# 3º GRAU / CÍRCULO II` — o numeral romano deveria ser `III` — corrigir para `# 3º GRAU / CÍRCULO III`. (O parser só lê o `3º`; o título aparece no Livro e muda o id da âncora.)
9. GRAUS & CÍRCULOS — Detrito: `uma das seguintes condições: Atordoar, Congelar, Enraizar ou Selar` — o Efeito chama-se `Atordoamento` e não há Efeito `Selar`; o catálogo (EfeitoSeeder) exige Atordoamento/Congelar/Enraizar — alinhar a lista (tirar Selar ou criá-lo; usar "Atordoamento").
10. GRAUS & CÍRCULOS — Congelar: `Muito parecido com Enraizamento` — o Efeito chama-se `Enraizar` — trocar por "Enraizar".
11. GRAUS & CÍRCULOS — Julgamento: `O Alvo recebe 1d de dano por Turno quaisquer efeitos positivos…` — falta conectivo — "…por Turno e quaisquer efeitos positivos…".
12. GRAUS & CÍRCULOS — Marionete Arcana: `. a criatura terá…`, `custo ja pago pela ultilização` — ortografia — "A criatura", "já", "utilização".
13. GRAUS & CÍRCULOS — Ato Múltiplo: `Max. 1 Dado por Círculo, contando a partir do terceiro` — os demais usam "Grau/Círculo" — "por Grau/Círculo".
14. GRAUS & CÍRCULOS — Libra: `4 Pontos` / `6 Pontos` — o resto do documento usa "PI" — "4 PI" / "6 PI".
15. GRAUS & CÍRCULOS — Contrato Mágico: `Metade da Mana Total da criatura… Mana Total do Invocador` — o resto do sistema fala em Arcana (e [[Formulas]] em Foco) — escolher um termo (Arcana/Foco/Mana) e usar só ele.
16. GRAUS & CÍRCULOS — tabela inicial: a 3ª coluna (d2…d100) não tem cabeçalho — dar um nome (ex.: "Dado"). Não aplicado: é tabela de arquivo embutido.
17. GRAUS & CÍRCULOS — `**Gasto:**` em negrito só em Aumentar Atributo e Encantamento Elemental (os outros 50+ usam `Gasto:` simples) — é só forma, mas muda o HTML do Livro e o documento é sincronizado com o catálogo de Efeitos; não aplicado — padronizar sem negrito se quiser.

## Características.md

18. Características — Aparência Inofensiva: o segundo nível `3 pontos: Considere que o personagem automaticamente você recebe vantagem em testes de Iniciativa…` descreve outra coisa (iniciativa/surpresa), não aparência — parece uma Característica sem título que ficou colada; hoje vira "Aparência Inofensiva (3 pontos)" no banco — dar título próprio (o nome original) ou confirmar que é nível da mesma.
19. Características — Sono Leve: o segundo nível `2 pontos: sono reparador, você consegue dormir bem…` também parece outra Característica ("Sono Reparador") colada — mesma decisão do item 18.
20. Características — `2 ponto` (Aparência Inofensiva), `-2 ponto` (Alcoólatra, Fetiche Material, Fobia), `-3 ponto` (Coração Mole, Deficiente Físico) — concordância — "pontos". O parser aceita os dois; o nome gerado para níveis múltiplos vem só do número.
21. Características — `Lingüísta`, `conseqüências`, `paranóico`, `pêlo` — grafia anterior ao Acordo Ortográfico — atualizar; atenção: `Lingüísta` é chave do seed (Nome no banco e lista `TraitsRequiringEspecificacao` do `TraitSeedParser`), renomear exige migrar o nome junto.
22. Características — Inculto: `Além disso, tem a característica inculto impede adquirir posteriormente…` — frase quebrada — "Além disso, a característica Inculto impede adquirir posteriormente…".
23. Características — Detectar Magia: `com um Teste de Ins` — abreviação única no documento — "Teste de Instinto".
24. Características — ordem: `Imunidade de Venenos` vem depois de `Lingüísta` e `Cleptomaníaco` antes de `Canibal` — reordenar alfabeticamente (o Livro já ordena pelo banco; é só leitura no Obsidian).

## Historico.md

25. Historico — `Cada historico concede…` — sem acento — "histórico". (O nome do arquivo também é `Historico.md`; renomear exige mudar o `.csproj` e os wikilinks.)

## Equipagem.md

26. Equipagem — Patrulheiro: `Escudo de Madeira` — o item do catálogo é "Tampa de Madeira" (o `EquipmentKitSeedData` já anota o apelido) — usar o nome do catálogo no texto.
27. Equipagem — Sobrevivente: `1 Tônico de Vida` (sem "simples", como nos outros kits) — confirmar se é o tônico comum de propósito.
28. Equipagem — Arcano, Ocultista e Sobrevivente não têm linha de Ciclos — confirmar que é intencional (0 Ciclos).

## Relação de Vocacão e Classes.md, tabelas

29. Relação de Vocacão e Classes — nome do arquivo `Vocacão` — "Vocação"; renomear e atualizar `CLAUDE.md` e wikilinks.
30. Relação de Vocacão e Classes — `Paladino` aparece em Campeão e em Adepto — confirmar se a classe é compartilhada.
31. Relação de Vocacão e Classes × Tabela de Classes — `Cultista` e `Sábio` estão na relação mas não têm colunas na [[Tabela de Classes]]; `Lamina Holística` (relação) × `Lâmina Holistica` (tabela) — completar a tabela e padronizar o acento ("Lâmina Holística").
32. Relação de Vocacão e Classes — `Cavalheiro` — confirmar se não é "Cavaleiro" (o termo de classe de combate); o mesmo nome está na [[Tabela de Classes]].
33. Tabela de Arquetipos — níveis 20–50 todos `0` (e a [[Tabela de Classes]] tem 1–19 todos `0`) — confirmar que o Arquétipo deixa de dar Vida/Arcana a partir do nível 20 (quando a Classe assume).
34. Tabela de Níveis — `+1 Pontos de Pericia` (12), `+2 Ponto de Atributo` (44), `Pericia`/`Caracteristica`/`Ultima` sem acento, `+8 pontos de Ignição` (11) minúsculo — concordância e acentos. Não aplicado: tabela embutida (só semeia a Auditoria uma vez, mas alimenta o Compêndio).
35. Tabela de Circulo e Grau por EAP / Tabelas de XP… e EAP — os documentos usam "EAP"; os requisitos e o app usam "VIS" — escolher um termo (e o nome dos arquivos, que são recursos embutidos).

## Outros

36. `ruina-itens.docx` e `ruina-itens 1.docx` — idênticos (mesmo MD5) — apagar a cópia `ruina-itens 1.docx`.
37. As estrelas alkerianas, Sistema Básico — emojis nos títulos (`# 🌿 I — AEURER`, `#### 🛡️ Defender`, `### 👤 Humano`…) — a política do app é ícones, não emojis; tirar muda o título e o id da seção no Livro — decidir se ficam.
38. Formulas — as linhas sob `# Ficha de Jogador` não têm linha em branco nem marcador de lista: o Obsidian mostra uma por linha, mas um renderizador Markdown estrito junta tudo num parágrafo — transformar em lista. Não aplicado: o pedido foi tratar todo número/linha de fórmula como intocável.

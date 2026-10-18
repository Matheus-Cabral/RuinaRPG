# D — Inventário dos testes por tipo de ficha (Tasks 11–12)

Gerado a partir do código: Task 11 lê as classes por tipo em `964f969` (antes da consolidação, já
apagadas); Task 12 lê as classes atuais. Cada linha é um método de teste da classe; **Existe em** diz em
quais classes do mesmo recurso (pelo prefixo: Character, Npc, Creature) há um método de mesmo nome; **Destino** diz onde o
caso roda agora: `Classe genérica.Método` (tipos em que roda — uma subclasse selada por tipo) ou
`fica em <classe do tipo>`. Os caminhos são relativos a `tests/RuinaRPG.Tests.Integration/Controllers/`.

Regras da consolidação (spec D, "Test consolidation"): cada afirmação vai literal; quando dois tipos
afirmavam valores diferentes, o valor virou propriedade do tipo (`ITipoDeTeste` em
`Fichas/TiposDeTeste.cs`: `StatusNegado`, `QuantidadeDePericias`, `AtributosEmOrdem`), nunca um `if` no
teste. Casos que existiam em um tipo só ficam na subclasse daquele tipo; casos de dois tipos ficam numa
base intermediária (`…DePersonagemOuNpcTestes`, `AtributosDeNpcOuCriaturaTestes`).

As linhas da Task 12 têm o destino "(Task 12)" — a Task 12 preenche.


## Task 11 — Perícias, Atributos, Maestrias, Arsenal, Penalidades de equipamento


## Skills

### `CharacterSkillsControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `List_returns_39_skills_all_zeroed` | Character/Npc | `Fichas.PericiasDaFichaTestes.List_returns_every_skill_of_the_kind_all_zeroed` (Personagem/NPC/Criatura) — contagem de `Tipo.QuantidadeDePericias` (39/20) |
| `Update_a_skill_sets_Gasto_and_the_response_computes_Modificador` | Character/Npc/Creature | `Fichas.PericiasDaFichaTestes.Update_a_skill_sets_Gasto_and_the_response_computes_Modificador` (Personagem/NPC/Criatura) |
| `Update_an_unknown_Pericia_key_returns_404` | Character | `Fichas.PericiasDaFichaTestes.Update_an_unknown_Pericia_key_returns_404` (Personagem/NPC/Criatura) |
| `List_adds_the_sheets_Historico_bonus_to_the_matching_Pericias_Modificador` | Character/Npc | `Fichas.PericiasDePersonagemOuNpcTestes.List_adds_the_sheets_Historico_bonus_to_the_matching_Pericias_Modificador` (Personagem/NPC) |
| `Update_with_AtributoEscolhido_persists_it_per_skill_and_List_computes_Total` | Character/Npc/Creature | `Fichas.PericiasDaFichaTestes.Update_with_AtributoEscolhido_persists_it_per_skill_and_List_computes_Total` (Personagem/NPC/Criatura) |
| `An_equipped_Pericia_Artefato_is_summed_into_that_Pericias_Total` | Character/Npc/Creature | `Fichas.PericiasDaFichaTestes.An_equipped_Pericia_Artefato_is_summed_into_that_Pericias_Total` (Personagem/NPC/Criatura) |
| `List_returns_skills_in_alphabetical_order_and_stays_stable_after_an_update` | Character/Npc/Creature | `Fichas.PericiasDaFichaTestes.List_returns_skills_in_alphabetical_order_and_stays_stable_after_an_update` (Personagem/NPC/Criatura) |
| `Update_by_an_unrelated_jogador_returns_403` | Character | `Fichas.PericiasDaFichaTestes.Update_by_someone_without_access_returns_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |
| `List_by_an_unrelated_jogador_returns_403` | Character | `Fichas.PericiasDaFichaTestes.List_by_someone_without_access_returns_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |

### `NpcSkillsControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `List_returns_39_skills_all_zeroed` | Character/Npc | `Fichas.PericiasDaFichaTestes.List_returns_every_skill_of_the_kind_all_zeroed` (Personagem/NPC/Criatura) — contagem de `Tipo.QuantidadeDePericias` (39/20) |
| `Update_a_skill_sets_Gasto_and_the_response_computes_Modificador` | Character/Npc/Creature | `Fichas.PericiasDaFichaTestes.Update_a_skill_sets_Gasto_and_the_response_computes_Modificador` (Personagem/NPC/Criatura) |
| `List_adds_the_sheets_Historico_bonus_to_the_matching_Pericias_Modificador` | Character/Npc | `Fichas.PericiasDePersonagemOuNpcTestes.List_adds_the_Historico_bonus_even_to_a_Pericia_with_no_Gasto` (Personagem/NPC) (a variante do NPC: Biblioteca sem gasto, Modificador 1) |
| `Update_with_AtributoEscolhido_persists_it_per_skill_and_List_computes_Total` | Character/Npc/Creature | `Fichas.PericiasDaFichaTestes.Update_with_AtributoEscolhido_persists_it_per_skill_and_List_computes_Total` (Personagem/NPC/Criatura) |
| `An_equipped_Pericia_Artefato_is_summed_into_that_Pericias_Total` | Character/Npc/Creature | `Fichas.PericiasDaFichaTestes.An_equipped_Pericia_Artefato_is_summed_into_that_Pericias_Total` (Personagem/NPC/Criatura) |
| `List_returns_skills_in_alphabetical_order_and_stays_stable_after_an_update` | Character/Npc/Creature | `Fichas.PericiasDaFichaTestes.List_returns_skills_in_alphabetical_order_and_stays_stable_after_an_update` (Personagem/NPC/Criatura) |
| `Update_by_a_different_gm_returns_404` | Npc/Creature | `Fichas.PericiasDaFichaTestes.Update_by_someone_without_access_returns_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |
| `List_by_a_different_gm_returns_404` | Npc/Creature | `Fichas.PericiasDaFichaTestes.List_by_someone_without_access_returns_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |

### `CreatureSkillsControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `List_returns_20_skills_all_zeroed` | Creature | `Fichas.PericiasDaFichaTestes.List_returns_every_skill_of_the_kind_all_zeroed` (Personagem/NPC/Criatura) — contagem de `Tipo.QuantidadeDePericias` (39/20) |
| `Update_a_skill_sets_Gasto_and_the_response_computes_Modificador` | Character/Npc/Creature | `Fichas.PericiasDaFichaTestes.Update_a_skill_sets_Gasto_and_the_response_computes_Modificador` (Personagem/NPC/Criatura) |
| `Update_with_AtributoEscolhido_persists_it_per_skill_and_List_computes_Total` | Character/Npc/Creature | `Fichas.PericiasDaFichaTestes.Update_with_AtributoEscolhido_persists_it_per_skill_and_List_computes_Total` (Personagem/NPC/Criatura) |
| `An_equipped_Pericia_Artefato_is_summed_into_that_Pericias_Total` | Character/Npc/Creature | `Fichas.PericiasDaFichaTestes.An_equipped_Pericia_Artefato_is_summed_into_that_Pericias_Total` (Personagem/NPC/Criatura) |
| `List_returns_skills_in_alphabetical_order_and_stays_stable_after_an_update` | Character/Npc/Creature | `Fichas.PericiasDaFichaTestes.List_returns_skills_in_alphabetical_order_and_stays_stable_after_an_update` (Personagem/NPC/Criatura) |
| `Update_by_a_different_gm_returns_404` | Npc/Creature | `Fichas.PericiasDaFichaTestes.Update_by_someone_without_access_returns_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |
| `List_by_a_different_gm_returns_404` | Npc/Creature | `Fichas.PericiasDaFichaTestes.List_by_someone_without_access_returns_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |
| `Update_a_disallowed_Pericia_returns_400` | Creature | fica em `Fichas.PericiasDaFichaTestesCriatura.Update_a_disallowed_Pericia_returns_400` |

## Attributes

### `CharacterAttributesControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `List_returns_8_attributes_all_zeroed` | Character/Npc | `Fichas.AtributosDaFichaTestes.List_returns_every_attribute_of_the_kind_all_zeroed` (Personagem/NPC/Criatura) — contagem de `Tipo.AtributosEmOrdem.Length` (8/6) |
| `Update_an_attribute_by_the_owner_recomputes_Total` | Character | `Fichas.AtributosDaFichaTestes.Update_an_attribute_by_the_owner_recomputes_Total` (Personagem/NPC/Criatura) |
| `Total_sums_the_Valor_of_equipped_Artefatos_targeting_that_Atributo` | Character | `Fichas.AtributosDaFichaTestes.Total_sums_the_Valor_of_equipped_Artefatos_targeting_that_Atributo` (Personagem/NPC/Criatura) |
| `List_returns_attributes_in_canonical_order_and_stays_stable_after_an_update` | Character/Npc/Creature | `Fichas.AtributosDaFichaTestes.List_returns_attributes_in_canonical_order_and_stays_stable_after_an_update` (Personagem/NPC/Criatura) — ordem de `Tipo.AtributosEmOrdem` |
| `Budget_reports_GastoTotal_and_the_points_received_from_creation_and_levels` | Character | `Fichas.AtributosDaFichaTestes.Budget_reports_GastoTotal_and_the_points_received_from_creation_and_levels` (Personagem/NPC/Criatura) |
| `Update_rejects_a_Gasto_that_would_make_the_8_attribute_sum_exceed_the_point_budget` | Character | fica em `Fichas.AtributosDaFichaTestesPersonagem.Update_rejects_a_Gasto_that_would_make_the_8_attribute_sum_exceed_the_point_budget` |
| `Update_rejects_when_this_attribute_plus_the_others_already_spent_exceeds_the_budget` | Character | fica em `Fichas.AtributosDaFichaTestesPersonagem.Update_rejects_when_this_attribute_plus_the_others_already_spent_exceeds_the_budget` |
| `Update_an_attribute_by_an_unrelated_jogador_returns_403` | Character | `Fichas.AtributosDaFichaTestes.Update_an_attribute_by_someone_without_access_returns_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |
| `List_by_an_unrelated_jogador_returns_403` | Character | `Fichas.AtributosDaFichaTestes.List_by_someone_without_access_returns_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |

### `NpcAttributesControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `List_returns_8_attributes_all_zeroed` | Character/Npc | `Fichas.AtributosDaFichaTestes.List_returns_every_attribute_of_the_kind_all_zeroed` (Personagem/NPC/Criatura) — contagem de `Tipo.AtributosEmOrdem.Length` (8/6) |
| `Update_an_attribute_by_the_owning_gm_recomputes_Total` | Npc/Creature | `Fichas.AtributosDaFichaTestes.Update_an_attribute_by_the_owner_recomputes_Total` (Personagem/NPC/Criatura) |
| `List_returns_attributes_in_canonical_order_and_stays_stable_after_an_update` | Character/Npc/Creature | `Fichas.AtributosDaFichaTestes.List_returns_attributes_in_canonical_order_and_stays_stable_after_an_update` (Personagem/NPC/Criatura) — ordem de `Tipo.AtributosEmOrdem` |
| `Update_an_attribute_by_a_different_gm_returns_404` | Npc/Creature | `Fichas.AtributosDaFichaTestes.Update_an_attribute_by_someone_without_access_returns_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |
| `List_by_a_different_gm_returns_404` | Npc/Creature | `Fichas.AtributosDaFichaTestes.List_by_someone_without_access_returns_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |
| `Update_an_attribute_by_the_player_the_sheet_was_granted_to_recomputes_Total` | Npc/Creature | `Fichas.AtributosDaFichaTestes.Update_an_attribute_by_the_player_the_sheet_was_granted_to_recomputes_Total` (Personagem/NPC/Criatura) — no Personagem, a ficha do próprio Jogador (`Tipo.CriarFichaDeJogadorAsync`) |
| `An_equipped_Atributo_Artefato_is_summed_into_that_Atributos_Total` | Npc/Creature | `Fichas.AtributosDaFichaTestes.An_equipped_Atributo_Artefato_is_summed_into_that_Atributos_Total` (Personagem/NPC/Criatura) |
| `Budget_at_level_1_offers_9_points_and_reports_zero_spent` | Npc/Creature | `Fichas.AtributosDaFichaTestes.Budget_at_level_1_offers_9_points_and_reports_zero_spent` (Personagem/NPC/Criatura) |
| `Budget_adds_the_Pontos_de_Atributo_of_every_level_up_to_the_sheets_Nivel` | Npc/Creature | `Fichas.AtributosDeNpcOuCriaturaTestes.Budget_adds_the_Pontos_de_Atributo_of_every_level_up_to_the_sheets_Nivel` (NPC/Criatura) |
| `Budget_GastoTotal_is_the_sum_of_Gasto_across_attributes` | Npc/Creature | `Fichas.AtributosDaFichaTestes.Budget_GastoTotal_is_the_sum_of_Gasto_across_attributes` (Personagem/NPC/Criatura) |
| `Update_above_the_budget_is_still_accepted_and_the_budget_reports_the_overspend` | Npc/Creature | `Fichas.AtributosDeNpcOuCriaturaTestes.Update_above_the_budget_is_still_accepted_and_the_budget_reports_the_overspend` (NPC/Criatura) |
| `Budget_by_a_different_gm_returns_404` | Npc/Creature | `Fichas.AtributosDaFichaTestes.Budget_by_someone_without_access_returns_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |

### `CreatureAttributesControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `List_returns_6_attributes_all_zeroed` | Creature | `Fichas.AtributosDaFichaTestes.List_returns_every_attribute_of_the_kind_all_zeroed` (Personagem/NPC/Criatura) — contagem de `Tipo.AtributosEmOrdem.Length` (8/6) |
| `Update_an_attribute_by_the_owning_gm_recomputes_Total` | Npc/Creature | `Fichas.AtributosDaFichaTestes.Update_an_attribute_by_the_owner_recomputes_Total` (Personagem/NPC/Criatura) |
| `List_returns_attributes_in_canonical_order_and_stays_stable_after_an_update` | Character/Npc/Creature | `Fichas.AtributosDaFichaTestes.List_returns_attributes_in_canonical_order_and_stays_stable_after_an_update` (Personagem/NPC/Criatura) — ordem de `Tipo.AtributosEmOrdem` |
| `Update_an_attribute_by_a_different_gm_returns_404` | Npc/Creature | `Fichas.AtributosDaFichaTestes.Update_an_attribute_by_someone_without_access_returns_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |
| `List_by_a_different_gm_returns_404` | Npc/Creature | `Fichas.AtributosDaFichaTestes.List_by_someone_without_access_returns_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |
| `Update_an_attribute_by_the_player_the_sheet_was_granted_to_recomputes_Total` | Npc/Creature | `Fichas.AtributosDaFichaTestes.Update_an_attribute_by_the_player_the_sheet_was_granted_to_recomputes_Total` (Personagem/NPC/Criatura) — no Personagem, a ficha do próprio Jogador (`Tipo.CriarFichaDeJogadorAsync`) |
| `An_equipped_Atributo_Artefato_is_summed_into_that_Atributos_Total` | Npc/Creature | `Fichas.AtributosDaFichaTestes.An_equipped_Atributo_Artefato_is_summed_into_that_Atributos_Total` (Personagem/NPC/Criatura) |
| `Budget_at_level_1_offers_9_points_and_reports_zero_spent` | Npc/Creature | `Fichas.AtributosDaFichaTestes.Budget_at_level_1_offers_9_points_and_reports_zero_spent` (Personagem/NPC/Criatura) |
| `Budget_adds_the_Pontos_de_Atributo_of_every_level_up_to_the_sheets_Nivel` | Npc/Creature | `Fichas.AtributosDeNpcOuCriaturaTestes.Budget_adds_the_Pontos_de_Atributo_of_every_level_up_to_the_sheets_Nivel` (NPC/Criatura) |
| `Budget_replaces_the_level_1_grant_with_the_Ranks_starting_points` | Creature | fica em `Fichas.AtributosDaFichaTestesCriatura.Budget_replaces_the_level_1_grant_with_the_Ranks_starting_points` |
| `Budget_GastoTotal_is_the_sum_of_Gasto_across_attributes` | Npc/Creature | `Fichas.AtributosDaFichaTestes.Budget_GastoTotal_is_the_sum_of_Gasto_across_attributes` (Personagem/NPC/Criatura) |
| `Update_above_the_budget_is_still_accepted_and_the_budget_reports_the_overspend` | Npc/Creature | `Fichas.AtributosDeNpcOuCriaturaTestes.Update_above_the_budget_is_still_accepted_and_the_budget_reports_the_overspend` (NPC/Criatura) |
| `Budget_by_a_different_gm_returns_404` | Npc/Creature | `Fichas.AtributosDaFichaTestes.Budget_by_someone_without_access_returns_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |

## Masteries

### `CharacterMasteriesControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `Add_a_valid_mastery_returns_201` | Character/Npc/Creature | `Fichas.MaestriasDaFichaTestes.Add_a_valid_mastery_returns_201` (Personagem/NPC/Criatura) |
| `Add_a_mastery_includes_the_Historico_bonus_in_Total` | Character/Npc | `Fichas.MaestriasDePersonagemOuNpcTestes.Add_a_mastery_includes_the_Historico_bonus_in_Total` (Personagem/NPC) |
| `List_returns_the_masteries_on_the_sheet` | Character/Npc/Creature | `Fichas.MaestriasDaFichaTestes.List_returns_the_masteries_on_the_sheet` (Personagem/NPC/Criatura) |
| `Delete_an_existing_mastery_returns_204_and_it_no_longer_appears_on_list` | Character/Npc/Creature | `Fichas.MaestriasDaFichaTestes.Delete_an_existing_mastery_returns_204_and_it_no_longer_appears_on_list` (Personagem/NPC/Criatura) |
| `Add_by_an_unrelated_jogador_returns_403` | Character | `Fichas.MaestriasDaFichaTestes.Add_by_someone_without_access_returns_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |
| `Add_computes_Total_from_the_matching_skill_and_attribute_rows` | Character/Npc/Creature | `Fichas.MaestriasDaFichaTestes.Add_computes_Total_from_the_matching_skill_and_attribute_rows` (Personagem/NPC/Criatura) |
| `Add_with_an_out_of_range_numeric_Pericia_or_Atributo_returns_400_and_does_not_poison_the_list` | Character/Npc/Creature | `Fichas.MaestriasDaFichaTestes.Add_with_an_out_of_range_numeric_Pericia_or_Atributo_returns_400_and_does_not_poison_the_list` (Personagem/NPC/Criatura) |
| `Update_an_existing_mastery_returns_200_and_recomputes_Total` | Character | fica em `Fichas.MaestriasDaFichaTestesPersonagem.Update_an_existing_mastery_returns_200_and_recomputes_Total` |
| `Update_by_an_unrelated_jogador_returns_403` | Character | fica em `Fichas.MaestriasDaFichaTestesPersonagem.Update_by_an_unrelated_jogador_returns_403` |
| `Update_a_nonexistent_mastery_returns_404` | Character | fica em `Fichas.MaestriasDaFichaTestesPersonagem.Update_a_nonexistent_mastery_returns_404` |
| `Update_with_an_out_of_range_numeric_Pericia_or_Atributo_returns_400_and_does_not_change_the_stored_row` | Character | fica em `Fichas.MaestriasDaFichaTestesPersonagem.Update_with_an_out_of_range_numeric_Pericia_or_Atributo_returns_400_and_does_not_change_the_stored_row` |

### `NpcMasteriesControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `Add_a_valid_mastery_returns_201` | Character/Npc/Creature | `Fichas.MaestriasDaFichaTestes.Add_a_valid_mastery_returns_201` (Personagem/NPC/Criatura) |
| `Add_a_mastery_includes_the_Historico_bonus_in_Total` | Character/Npc | `Fichas.MaestriasDePersonagemOuNpcTestes.Add_a_mastery_includes_the_Historico_bonus_in_Total` (Personagem/NPC) |
| `List_returns_the_masteries_on_the_sheet` | Character/Npc/Creature | `Fichas.MaestriasDaFichaTestes.List_returns_the_masteries_on_the_sheet` (Personagem/NPC/Criatura) |
| `Delete_an_existing_mastery_returns_204_and_it_no_longer_appears_on_list` | Character/Npc/Creature | `Fichas.MaestriasDaFichaTestes.Delete_an_existing_mastery_returns_204_and_it_no_longer_appears_on_list` (Personagem/NPC/Criatura) |
| `Add_by_a_different_gm_returns_404` | Npc/Creature | `Fichas.MaestriasDaFichaTestes.Add_by_someone_without_access_returns_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |
| `Add_computes_Total_from_the_matching_skill_and_attribute_rows` | Character/Npc/Creature | `Fichas.MaestriasDaFichaTestes.Add_computes_Total_from_the_matching_skill_and_attribute_rows` (Personagem/NPC/Criatura) |
| `Add_with_an_out_of_range_numeric_Pericia_or_Atributo_returns_400_and_does_not_poison_the_list` | Character/Npc/Creature | `Fichas.MaestriasDaFichaTestes.Add_with_an_out_of_range_numeric_Pericia_or_Atributo_returns_400_and_does_not_poison_the_list` (Personagem/NPC/Criatura) |

### `CreatureMasteriesControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `Add_a_valid_mastery_returns_201` | Character/Npc/Creature | `Fichas.MaestriasDaFichaTestes.Add_a_valid_mastery_returns_201` (Personagem/NPC/Criatura) |
| `List_returns_the_masteries_on_the_sheet` | Character/Npc/Creature | `Fichas.MaestriasDaFichaTestes.List_returns_the_masteries_on_the_sheet` (Personagem/NPC/Criatura) |
| `Delete_an_existing_mastery_returns_204_and_it_no_longer_appears_on_list` | Character/Npc/Creature | `Fichas.MaestriasDaFichaTestes.Delete_an_existing_mastery_returns_204_and_it_no_longer_appears_on_list` (Personagem/NPC/Criatura) |
| `Add_by_a_different_gm_returns_404` | Npc/Creature | `Fichas.MaestriasDaFichaTestes.Add_by_someone_without_access_returns_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |
| `Add_computes_Total_from_the_matching_skill_and_attribute_rows` | Character/Npc/Creature | `Fichas.MaestriasDaFichaTestes.Add_computes_Total_from_the_matching_skill_and_attribute_rows` (Personagem/NPC/Criatura) |
| `Add_with_an_out_of_range_numeric_Pericia_or_Atributo_returns_400_and_does_not_poison_the_list` | Character/Npc/Creature | `Fichas.MaestriasDaFichaTestes.Add_with_an_out_of_range_numeric_Pericia_or_Atributo_returns_400_and_does_not_poison_the_list` (Personagem/NPC/Criatura) |
| `Add_with_a_disallowed_Pericia_returns_400_and_does_not_poison_the_list` | Creature | fica em `Fichas.MaestriasDaFichaTestesCriatura.Add_with_a_disallowed_Pericia_returns_400_and_does_not_poison_the_list` |

## Arsenal

### `CharacterArsenalControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `AddWeapon_links_the_item_and_initializes_durability_from_its_maximo` | Character/Npc | `Fichas.ArsenalDePersonagemOuNpcTestes.AddWeapon_links_the_item_and_initializes_durability_from_its_maximo` (Personagem/NPC) |
| `Equipping_a_second_weapon_unequips_the_first_by_default` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.Equipping_a_second_weapon_unequips_the_first_by_default` (Personagem/NPC/Criatura) |
| `ArmorSlots_list_returns_all_3_slots_even_when_empty` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.ArmorSlots_list_returns_all_3_slots_even_when_empty` (Personagem/NPC/Criatura) |
| `Update_an_armor_slot_links_the_item_and_initializes_durability` | Character/Npc/Creature | `Fichas.ArsenalDePersonagemOuNpcTestes.Update_an_armor_slot_links_the_item_and_initializes_durability` (Personagem/NPC) |
| `AddShield_and_list_returns_it_with_live_catalog_fields` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.AddShield_and_list_returns_it_with_live_catalog_fields` (Personagem/NPC/Criatura) |
| `Weapon_armor_slot_and_shield_responses_include_the_catalog_items_ImageUrl_and_Descricao` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.Weapon_armor_slot_and_shield_responses_include_the_catalog_items_ImageUrl_and_Descricao` (Personagem/NPC/Criatura) |
| `Delete_an_existing_weapon_returns_204_and_it_no_longer_appears_on_list` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.Delete_an_existing_weapon_returns_204_and_it_no_longer_appears_on_list` (Personagem/NPC/Criatura) |
| `Unlinking_an_armor_slot_clears_its_item_and_durability` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.Unlinking_an_armor_slot_clears_its_item_and_durability` (Personagem/NPC/Criatura) |
| `Delete_an_existing_shield_returns_204_and_it_no_longer_appears_on_list` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.Delete_an_existing_shield_returns_204_and_it_no_longer_appears_on_list` (Personagem/NPC/Criatura) |
| `Weapon_actions_by_an_unrelated_jogador_return_403` | Character | `Fichas.ArsenalDaFichaTestes.Weapon_actions_by_someone_without_access_return_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |
| `ListWeapons_by_an_unrelated_jogador_returns_403` | Character | `Fichas.ArsenalDaFichaTestes.List_by_someone_without_access_returns_the_denied_status(lista: "weapons")` (Personagem/NPC/Criatura) — `Tipo.StatusNegado` |
| `ListArmorSlots_by_an_unrelated_jogador_returns_403` | Character | `Fichas.ArsenalDaFichaTestes.List_by_someone_without_access_returns_the_denied_status(lista: "armor-slots")` (Personagem/NPC/Criatura) — `Tipo.StatusNegado` |
| `ListShields_by_an_unrelated_jogador_returns_403` | Character | `Fichas.ArsenalDaFichaTestes.List_by_someone_without_access_returns_the_denied_status(lista: "shields")` (Personagem/NPC/Criatura) — `Tipo.StatusNegado` |
| `AddWeapon_with_a_malformed_ItemId_returns_400` | Character/Npc | `Fichas.ArsenalDaFichaTestes.AddWeapon_with_a_malformed_ItemId_returns_400` (Personagem/NPC/Criatura) |
| `UpdateArmorSlot_with_a_malformed_ItemId_returns_400` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.UpdateArmorSlot_with_a_malformed_ItemId_returns_400` (Personagem/NPC/Criatura) |
| `AddShield_with_a_malformed_ItemId_returns_400` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.AddShield_with_a_malformed_ItemId_returns_400` (Personagem/NPC/Criatura) |
| `Equipping_a_second_shield_unequips_the_first` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.Equipping_a_second_shield_unequips_the_first` (Personagem/NPC/Criatura) |
| `UpdateWeaponDurability_sets_DurabilidadeAtual` | Character | `Fichas.ArsenalDaFichaTestes.UpdateWeaponDurability_sets_DurabilidadeAtual` (Personagem/NPC/Criatura) |
| `UpdateShieldDurability_sets_DurabilidadeAtual` | Character | `Fichas.ArsenalDaFichaTestes.UpdateShieldDurability_sets_DurabilidadeAtual` (Personagem/NPC/Criatura) |
| `UpdateArmorSlotDurability_sets_DurabilidadeAtual_when_a_slot_has_an_item` | Character | `Fichas.ArsenalDaFichaTestes.UpdateArmorSlotDurability_sets_DurabilidadeAtual_when_a_slot_has_an_item` (Personagem/NPC/Criatura) |
| `UpdateWeaponDurability_clamps_to_the_item_DurabilidadeMaxima_instead_of_rejecting` | Character | `Fichas.ArsenalDaFichaTestes.UpdateWeaponDurability_clamps_to_the_item_DurabilidadeMaxima_instead_of_rejecting` (Personagem/NPC/Criatura) |
| `UpdateShieldDurability_clamps_to_the_item_DurabilidadeMaxima_instead_of_rejecting` | Character | `Fichas.ArsenalDaFichaTestes.UpdateShieldDurability_clamps_to_the_item_DurabilidadeMaxima_instead_of_rejecting` (Personagem/NPC/Criatura) |
| `UpdateArmorSlotDurability_clamps_to_the_item_DurabilidadeMaxima_instead_of_rejecting` | Character | `Fichas.ArsenalDaFichaTestes.UpdateArmorSlotDurability_clamps_to_the_item_DurabilidadeMaxima_instead_of_rejecting` (Personagem/NPC/Criatura) |
| `UpdateArmorSlotDurability_on_an_empty_slot_returns_400` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.UpdateArmorSlotDurability_on_an_empty_slot_returns_400` (Personagem/NPC/Criatura) |

### `NpcArsenalControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `AddWeapon_links_the_item_and_initializes_durability_from_its_maximo` | Character/Npc | `Fichas.ArsenalDePersonagemOuNpcTestes.AddWeapon_links_the_item_and_initializes_durability_from_its_maximo` (Personagem/NPC) |
| `Equipping_a_second_weapon_unequips_the_first_by_default` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.Equipping_a_second_weapon_unequips_the_first_by_default` (Personagem/NPC/Criatura) |
| `ArmorSlots_list_returns_all_3_slots_even_when_empty` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.ArmorSlots_list_returns_all_3_slots_even_when_empty` (Personagem/NPC/Criatura) |
| `Update_an_armor_slot_links_the_item_and_initializes_durability` | Character/Npc/Creature | `Fichas.ArsenalDePersonagemOuNpcTestes.Update_an_armor_slot_links_the_item_and_initializes_durability` (Personagem/NPC) |
| `AddShield_and_list_returns_it_with_live_catalog_fields` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.AddShield_and_list_returns_it_with_live_catalog_fields` (Personagem/NPC/Criatura) |
| `Weapon_armor_slot_and_shield_responses_include_the_catalog_items_ImageUrl_and_Descricao` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.Weapon_armor_slot_and_shield_responses_include_the_catalog_items_ImageUrl_and_Descricao` (Personagem/NPC/Criatura) |
| `Delete_an_existing_weapon_returns_204_and_it_no_longer_appears_on_list` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.Delete_an_existing_weapon_returns_204_and_it_no_longer_appears_on_list` (Personagem/NPC/Criatura) |
| `Unlinking_an_armor_slot_clears_its_item_and_durability` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.Unlinking_an_armor_slot_clears_its_item_and_durability` (Personagem/NPC/Criatura) |
| `Delete_an_existing_shield_returns_204_and_it_no_longer_appears_on_list` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.Delete_an_existing_shield_returns_204_and_it_no_longer_appears_on_list` (Personagem/NPC/Criatura) |
| `Weapon_actions_by_a_different_gm_return_404` | Npc/Creature | `Fichas.ArsenalDaFichaTestes.Weapon_actions_by_someone_without_access_return_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |
| `ListWeapons_by_a_different_gm_returns_404` | Npc/Creature | `Fichas.ArsenalDaFichaTestes.List_by_someone_without_access_returns_the_denied_status(lista: "weapons")` (Personagem/NPC/Criatura) — `Tipo.StatusNegado` |
| `ListArmorSlots_by_a_different_gm_returns_404` | Npc/Creature | `Fichas.ArsenalDaFichaTestes.List_by_someone_without_access_returns_the_denied_status(lista: "armor-slots")` (Personagem/NPC/Criatura) — `Tipo.StatusNegado` |
| `ListShields_by_a_different_gm_returns_404` | Npc/Creature | `Fichas.ArsenalDaFichaTestes.List_by_someone_without_access_returns_the_denied_status(lista: "shields")` (Personagem/NPC/Criatura) — `Tipo.StatusNegado` |
| `AddWeapon_with_a_malformed_ItemId_returns_400` | Character/Npc | `Fichas.ArsenalDaFichaTestes.AddWeapon_with_a_malformed_ItemId_returns_400` (Personagem/NPC/Criatura) |
| `UpdateArmorSlot_with_a_malformed_ItemId_returns_400` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.UpdateArmorSlot_with_a_malformed_ItemId_returns_400` (Personagem/NPC/Criatura) |
| `AddShield_with_a_malformed_ItemId_returns_400` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.AddShield_with_a_malformed_ItemId_returns_400` (Personagem/NPC/Criatura) |
| `Equipping_a_second_shield_unequips_the_first` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.Equipping_a_second_shield_unequips_the_first` (Personagem/NPC/Criatura) |
| `UpdateWeaponDurability_clamps_to_the_item_DurabilidadeMaxima` | Npc | `Fichas.ArsenalDaFichaTestes.UpdateWeaponDurability_clamps_to_the_item_DurabilidadeMaxima_instead_of_rejecting` (Personagem/NPC/Criatura) |
| `UpdateShieldDurability_clamps_to_the_item_DurabilidadeMaxima` | Npc/Creature | `Fichas.ArsenalDaFichaTestes.UpdateShieldDurability_clamps_to_the_item_DurabilidadeMaxima_instead_of_rejecting` (Personagem/NPC/Criatura) |
| `UpdateArmorSlotDurability_clamps_to_the_item_DurabilidadeMaxima` | Npc/Creature | `Fichas.ArsenalDaFichaTestes.UpdateArmorSlotDurability_clamps_to_the_item_DurabilidadeMaxima_instead_of_rejecting` (Personagem/NPC/Criatura) |
| `UpdateArmorSlotDurability_on_an_empty_slot_returns_400` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.UpdateArmorSlotDurability_on_an_empty_slot_returns_400` (Personagem/NPC/Criatura) |

### `CreatureArsenalControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `AddWeapon_from_the_catalogo_links_the_item_same_as_a_character` | Creature | fica em `Fichas.ArsenalDaFichaTestesCriatura.AddWeapon_from_the_catalogo_links_the_item_same_as_a_character` (lê `DurabilidadeMaximo`) |
| `AddWeapon_manual_creates_a_natural_attack_with_no_durability_or_alcance` | Creature | fica em `Fichas.ArsenalDaFichaTestesCriatura.AddWeapon_manual_creates_a_natural_attack_with_no_durability_or_alcance` |
| `AddWeapon_with_both_ItemId_and_manual_fields_returns_400` | Creature | fica em `Fichas.ArsenalDaFichaTestesCriatura.AddWeapon_with_both_ItemId_and_manual_fields_returns_400` |
| `AddWeapon_with_neither_ItemId_nor_manual_fields_returns_400` | Creature | fica em `Fichas.ArsenalDaFichaTestesCriatura.AddWeapon_with_neither_ItemId_nor_manual_fields_returns_400` |
| `Equipping_a_second_weapon_unequips_the_first_by_default` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.Equipping_a_second_weapon_unequips_the_first_by_default` (Personagem/NPC/Criatura) (duas armas do catálogo) e fica em `Fichas.ArsenalDaFichaTestesCriatura.Equipping_a_natural_attack_unequips_the_catalog_weapon` (o cenário original: catálogo + ataque natural) |
| `ArmorSlots_list_returns_all_3_slots_even_when_empty` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.ArmorSlots_list_returns_all_3_slots_even_when_empty` (Personagem/NPC/Criatura) |
| `Update_an_armor_slot_links_the_item_and_initializes_durability` | Character/Npc/Creature | fica em `Fichas.ArsenalDaFichaTestesCriatura.Update_an_armor_slot_links_the_item_and_initializes_durability` (lê `DurabilidadeMaximo`) |
| `AddShield_and_list_returns_it_with_live_catalog_fields` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.AddShield_and_list_returns_it_with_live_catalog_fields` (Personagem/NPC/Criatura) |
| `Weapon_armor_slot_and_shield_responses_include_the_catalog_items_ImageUrl_and_Descricao` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.Weapon_armor_slot_and_shield_responses_include_the_catalog_items_ImageUrl_and_Descricao` (Personagem/NPC/Criatura) |
| `A_manual_natural_attack_weapon_has_no_ImageUrl_or_Descricao` | Creature | fica em `Fichas.ArsenalDaFichaTestesCriatura.A_manual_natural_attack_weapon_has_no_ImageUrl_or_Descricao` |
| `Delete_an_existing_weapon_returns_204_and_it_no_longer_appears_on_list` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.Delete_an_existing_weapon_returns_204_and_it_no_longer_appears_on_list` (Personagem/NPC/Criatura) |
| `Unlinking_an_armor_slot_clears_its_item_and_durability` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.Unlinking_an_armor_slot_clears_its_item_and_durability` (Personagem/NPC/Criatura) |
| `Delete_an_existing_shield_returns_204_and_it_no_longer_appears_on_list` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.Delete_an_existing_shield_returns_204_and_it_no_longer_appears_on_list` (Personagem/NPC/Criatura) |
| `Weapon_actions_by_a_different_gm_return_404` | Npc/Creature | `Fichas.ArsenalDaFichaTestes.Weapon_actions_by_someone_without_access_return_the_denied_status` (Personagem/NPC/Criatura) — status de `Tipo.StatusNegado` (403/404) |
| `ListWeapons_by_a_different_gm_returns_404` | Npc/Creature | `Fichas.ArsenalDaFichaTestes.List_by_someone_without_access_returns_the_denied_status(lista: "weapons")` (Personagem/NPC/Criatura) — `Tipo.StatusNegado` |
| `ListArmorSlots_by_a_different_gm_returns_404` | Npc/Creature | `Fichas.ArsenalDaFichaTestes.List_by_someone_without_access_returns_the_denied_status(lista: "armor-slots")` (Personagem/NPC/Criatura) — `Tipo.StatusNegado` |
| `ListShields_by_a_different_gm_returns_404` | Npc/Creature | `Fichas.ArsenalDaFichaTestes.List_by_someone_without_access_returns_the_denied_status(lista: "shields")` (Personagem/NPC/Criatura) — `Tipo.StatusNegado` |
| `AddWeapon_from_catalogo_with_a_malformed_ItemId_returns_400` | Creature | `Fichas.ArsenalDaFichaTestes.AddWeapon_with_a_malformed_ItemId_returns_400` (Personagem/NPC/Criatura) |
| `UpdateArmorSlot_with_a_malformed_ItemId_returns_400` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.UpdateArmorSlot_with_a_malformed_ItemId_returns_400` (Personagem/NPC/Criatura) |
| `AddShield_with_a_malformed_ItemId_returns_400` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.AddShield_with_a_malformed_ItemId_returns_400` (Personagem/NPC/Criatura) |
| `Equipping_a_second_shield_unequips_the_first` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.Equipping_a_second_shield_unequips_the_first` (Personagem/NPC/Criatura) |
| `UpdateWeaponDurability_clamps_to_the_item_DurabilidadeMaxima_for_a_catalog_weapon` | Creature | `Fichas.ArsenalDaFichaTestes.UpdateWeaponDurability_clamps_to_the_item_DurabilidadeMaxima_instead_of_rejecting` (Personagem/NPC/Criatura) |
| `UpdateWeaponDurability_on_a_manual_natural_attack_returns_400` | Creature | fica em `Fichas.ArsenalDaFichaTestesCriatura.UpdateWeaponDurability_on_a_manual_natural_attack_returns_400` |
| `UpdateShieldDurability_clamps_to_the_item_DurabilidadeMaxima` | Npc/Creature | `Fichas.ArsenalDaFichaTestes.UpdateShieldDurability_clamps_to_the_item_DurabilidadeMaxima_instead_of_rejecting` (Personagem/NPC/Criatura) |
| `UpdateArmorSlotDurability_clamps_to_the_item_DurabilidadeMaxima` | Npc/Creature | `Fichas.ArsenalDaFichaTestes.UpdateArmorSlotDurability_clamps_to_the_item_DurabilidadeMaxima_instead_of_rejecting` (Personagem/NPC/Criatura) |
| `UpdateArmorSlotDurability_on_an_empty_slot_returns_400` | Character/Npc/Creature | `Fichas.ArsenalDaFichaTestes.UpdateArmorSlotDurability_on_an_empty_slot_returns_400` (Personagem/NPC/Criatura) |

## EquipmentPenalties

### `CharacterEquipmentPenaltiesTests`

| Método | Existe em | Destino |
|---|---|---|
| `An_equipped_weapon_with_an_unmet_requirement_reduces_the_attribute_total_and_recovers_when_met` | Character | `Fichas.PenalidadesDeEquipamentoTestes.An_equipped_weapon_with_an_unmet_requirement_reduces_the_attribute_total_and_recovers_when_met` (Personagem/NPC/Criatura) |
| `The_weapon_line_reports_requirements_pendencias_and_penalty` | Character | `Fichas.PenalidadesDeEquipamentoTestes.The_weapon_line_reports_requirements_pendencias_and_penalty` (Personagem/NPC/Criatura) |
| `Armor_in_a_slot_shield_equipped_and_artifact_on_the_list_all_penalize` | Character | `Fichas.PenalidadesDeEquipamentoTestes.Armor_in_a_slot_shield_equipped_and_artifact_on_the_list_all_penalize` (Personagem/NPC/Criatura) |
| `A_penalty_never_makes_another_item_or_a_passiva_fail_its_requirements` | Character | fica em `Fichas.PenalidadesDeEquipamentoTestesPersonagem.A_penalty_never_makes_another_item_or_a_passiva_fail_its_requirements` |
| `Skill_and_sub_attribute_penalties_reach_their_totals` | Character | `Fichas.PenalidadesDeEquipamentoTestes.Skill_and_sub_attribute_penalties_reach_their_totals` (Personagem/NPC/Criatura) |
| `A_penalty_larger_than_the_value_follows_the_formula_floor_and_does_not_fail` | Character | fica em `Fichas.PenalidadesDeEquipamentoTestesPersonagem.A_penalty_larger_than_the_value_follows_the_formula_floor_and_does_not_fail` |
| `A_penalty_on_a_pericia_the_auditor_removed_is_ignored` | Character | fica em `Fichas.PenalidadesDeEquipamentoTestesPersonagem.A_penalty_on_a_pericia_the_auditor_removed_is_ignored` |
| `Equipment_penalties_is_forbidden_to_a_jogador_who_does_not_own_the_sheet` | Character | fica em `Fichas.PenalidadesDeEquipamentoTestesPersonagem.Equipment_penalties_is_forbidden_to_a_jogador_who_does_not_own_the_sheet` |
| `An_item_without_requirements_reports_its_penalty_on_the_line_and_penalizes_nothing` | Character/Npc/Creature | `Fichas.PenalidadesDeEquipamentoTestes.An_item_without_requirements_reports_its_penalty_on_the_line_and_penalizes_nothing` (Personagem/NPC/Criatura) |
| `An_item_whose_requirements_are_met_still_reports_its_penalty_on_the_line` | Character/Npc/Creature | `Fichas.PenalidadesDeEquipamentoTestes.An_item_whose_requirements_are_met_still_reports_its_penalty_on_the_line` (Personagem/NPC/Criatura) |

### `NpcEquipmentPenaltiesTests`

| Método | Existe em | Destino |
|---|---|---|
| `An_equipped_weapon_with_an_unmet_requirement_reduces_the_npc_attribute_total_and_an_unequipped_one_does_not` | Npc | `Fichas.PenalidadesDeEquipamentoTestes.An_equipped_weapon_with_an_unmet_requirement_reduces_the_attribute_total_and_recovers_when_met` (Personagem/NPC/Criatura) — mesmas duas afirmações, na mesma ordem; o caso genérico acrescenta a recuperação ao cumprir o requisito |
| `Armor_shield_and_artifact_penalize_an_npc_and_are_listed_as_active` | Npc | `Fichas.PenalidadesDeEquipamentoTestes.Armor_shield_and_artifact_penalize_and_are_listed_as_active_and_lines_report_requirements` (Personagem/NPC/Criatura) — o caso da Criatura, que tem as mesmas afirmações do NPC e mais duas |
| `The_npc_lines_report_requirements_pendencias_and_penalty` | Npc | `Fichas.PenalidadesDeEquipamentoTestes.The_weapon_line_reports_requirements_pendencias_and_a_text_only_penalty` (Personagem/NPC/Criatura) |
| `Npc_skill_and_sub_attribute_totals_follow_the_penalty` | Npc | `Fichas.PenalidadesDeEquipamentoTestes.Skill_and_sub_attribute_penalties_reach_their_totals` (Personagem/NPC/Criatura) |
| `A_granted_npc_evaluates_requirements_for_the_player_too` | Npc | fica em `Fichas.PenalidadesDeEquipamentoTestesNpc.A_granted_npc_evaluates_requirements_for_the_player_too` |
| `An_item_without_requirements_reports_its_penalty_on_the_line_and_penalizes_nothing` | Character/Npc/Creature | `Fichas.PenalidadesDeEquipamentoTestes.An_item_without_requirements_reports_its_penalty_on_the_line_and_penalizes_nothing` (Personagem/NPC/Criatura) |
| `An_item_whose_requirements_are_met_still_reports_its_penalty_on_the_line` | Character/Npc/Creature | `Fichas.PenalidadesDeEquipamentoTestes.An_item_whose_requirements_are_met_still_reports_its_penalty_on_the_line` (Personagem/NPC/Criatura) |

### `CreatureEquipmentPenaltiesTests`

| Método | Existe em | Destino |
|---|---|---|
| `An_equipped_weapon_with_an_unmet_attribute_requirement_penalizes_the_creature` | Creature | fica em `Fichas.PenalidadesDeEquipamentoTestesCriatura.An_equipped_weapon_with_an_unmet_attribute_requirement_penalizes_the_creature` |
| `Vocacao_classe_and_estrela_requirements_are_ignored_on_a_creature` | Creature | fica em `Fichas.PenalidadesDeEquipamentoTestesCriatura.Vocacao_classe_and_estrela_requirements_are_ignored_on_a_creature` |
| `A_natural_attack_without_a_catalog_item_is_ignored` | Creature | fica em `Fichas.PenalidadesDeEquipamentoTestesCriatura.A_natural_attack_without_a_catalog_item_is_ignored` |
| `A_requirement_on_an_attribute_the_creature_does_not_have_is_ignored` | Creature | fica em `Fichas.PenalidadesDeEquipamentoTestesCriatura.A_requirement_on_an_attribute_the_creature_does_not_have_is_ignored` |
| `Armor_shield_and_artifact_penalize_a_creature_and_lines_report_requirements` | Creature | `Fichas.PenalidadesDeEquipamentoTestes.Armor_shield_and_artifact_penalize_and_are_listed_as_active_and_lines_report_requirements` (Personagem/NPC/Criatura) |
| `Creature_skill_and_sub_attribute_totals_follow_the_penalty` | Creature | `Fichas.PenalidadesDeEquipamentoTestes.Skill_and_sub_attribute_penalties_reach_their_totals` (Personagem/NPC/Criatura) |
| `An_item_without_requirements_reports_its_penalty_on_the_line_and_penalizes_nothing` | Character/Npc/Creature | `Fichas.PenalidadesDeEquipamentoTestes.An_item_without_requirements_reports_its_penalty_on_the_line_and_penalizes_nothing` (Personagem/NPC/Criatura) |
| `An_item_whose_requirements_are_met_still_reports_its_penalty_on_the_line` | Character/Npc/Creature | `Fichas.PenalidadesDeEquipamentoTestes.An_item_whose_requirements_are_met_still_reports_its_penalty_on_the_line` (Personagem/NPC/Criatura) |

## Task 12 — Posses, Afeições e Características, Magias e Habilidades, Runas, Afinidades, Equipagem, Passivas, Fichas


## Possessions

### `CharacterPossessionsControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `AddInventoryItem_links_the_item_and_computes_total_as_peso_times_qtd` | Character/Npc | (Task 12) |
| `ListInventory_returns_the_added_item` | Character/Npc | (Task 12) |
| `Delete_an_existing_inventory_item_returns_204_and_it_no_longer_appears_on_list` | Character/Npc | (Task 12) |
| `UpdateInventoryItemQtd_sets_Qtd_and_recomputes_Total` | Character/Npc | (Task 12) |
| `AddArtifact_links_the_item_and_list_returns_it_with_live_catalog_fields` | Character/Npc/Creature | (Task 12) |
| `Inventory_and_artifact_responses_include_the_catalog_items_ImageUrl_and_Descricao` | Character/Npc | (Task 12) |
| `Delete_an_existing_artifact_returns_204_and_it_no_longer_appears_on_list` | Character/Npc/Creature | (Task 12) |
| `AddArtifact_a_4th_of_the_same_TipoDeAlvo_returns_400` | Character/Npc/Creature | (Task 12) |
| `Possession_actions_by_an_unrelated_jogador_return_403` | Character | (Task 12) |
| `AddInventoryItem_rejects_a_catalog_id_that_is_not_an_ItemGeral` | Character | (Task 12) |
| `AddInventoryItem_with_a_malformed_ItemId_returns_400` | Character/Npc | (Task 12) |
| `AddArtifact_with_a_malformed_ArtifactItemId_returns_400` | Character/Npc/Creature | (Task 12) |

### `NpcPossessionsControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `AddInventoryItem_links_the_item_and_computes_total_as_peso_times_qtd` | Character/Npc | (Task 12) |
| `ListInventory_returns_the_added_item` | Character/Npc | (Task 12) |
| `Delete_an_existing_inventory_item_returns_204_and_it_no_longer_appears_on_list` | Character/Npc | (Task 12) |
| `UpdateInventoryItemQtd_sets_Qtd_and_recomputes_Total` | Character/Npc | (Task 12) |
| `AddArtifact_links_the_item_and_list_returns_it_with_live_catalog_fields` | Character/Npc/Creature | (Task 12) |
| `Inventory_and_artifact_responses_include_the_catalog_items_ImageUrl_and_Descricao` | Character/Npc | (Task 12) |
| `Delete_an_existing_artifact_returns_204_and_it_no_longer_appears_on_list` | Character/Npc/Creature | (Task 12) |
| `AddArtifact_a_4th_of_the_same_TipoDeAlvo_returns_400` | Character/Npc/Creature | (Task 12) |
| `Possession_actions_by_a_different_gm_return_404` | Npc/Creature | (Task 12) |
| `AddInventoryItem_with_a_malformed_ItemId_returns_400` | Character/Npc | (Task 12) |
| `AddArtifact_with_a_malformed_ArtifactItemId_returns_400` | Character/Npc/Creature | (Task 12) |

### `CreaturePossessionsControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `AddSpoil_computes_CustoTotal_from_the_items_Preco_and_Qtd` | Creature | (Task 12) |
| `ListSpoils_returns_the_added_spoil` | Creature | (Task 12) |
| `Delete_an_existing_spoil_returns_204_and_it_no_longer_appears_on_list` | Creature | (Task 12) |
| `UpdateSpoilQtd_sets_Qtd_and_recomputes_CustoTotal` | Creature | (Task 12) |
| `AddArtifact_links_the_item_and_list_returns_it_with_live_catalog_fields` | Character/Npc/Creature | (Task 12) |
| `Spoil_and_artifact_responses_include_the_catalog_items_ImageUrl_and_Descricao` | Creature | (Task 12) |
| `Delete_an_existing_artifact_returns_204_and_it_no_longer_appears_on_list` | Character/Npc/Creature | (Task 12) |
| `AddArtifact_a_4th_of_the_same_TipoDeAlvo_returns_400` | Character/Npc/Creature | (Task 12) |
| `Possession_actions_by_a_different_gm_return_404` | Npc/Creature | (Task 12) |
| `AddSpoil_with_a_malformed_ItemId_returns_400` | Creature | (Task 12) |
| `AddArtifact_with_a_malformed_ArtifactItemId_returns_400` | Character/Npc/Creature | (Task 12) |

## AffectionsAndTraits

### `CharacterAffectionsAndTraitsControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `AddAffection_then_ListAffections_returns_it_then_DeleteAffection_removes_it` | Character/Npc/Creature | (Task 12) |
| `UpdateAffection_returns_200_and_the_list_reflects_the_change` | Character | (Task 12) |
| `UpdateAffection_by_an_unrelated_jogador_returns_403` | Character | (Task 12) |
| `UpdateAffection_for_a_nonexistent_id_returns_404` | Character | (Task 12) |
| `AddTrait_lands_in_the_correct_bucket_with_correct_totals_and_DeleteTrait_removes_it` | Character/Npc/Creature | (Task 12) |
| `AddTrait_with_a_nonexistent_TraitId_returns_400` | Character/Npc/Creature | (Task 12) |
| `AddTrait_rejects_a_Positiva_that_would_push_the_side_total_past_the_budget` | Character/Npc/Creature | (Task 12) |
| `AddTrait_accepts_Negativas_up_to_twice_the_positive_budget_and_rejects_past_it` | Character/Npc/Creature | (Task 12) |
| `AddTrait_rejects_a_RequerEspecificacao_trait_added_without_an_Especificacao` | Character | (Task 12) |
| `AddTrait_persists_and_returns_the_Especificacao_for_a_RequerEspecificacao_trait` | Character | (Task 12) |
| `Affection_and_trait_actions_by_an_unrelated_jogador_return_403` | Character | (Task 12) |
| `AddTrait_with_a_creature_exclusive_characteristic_Id_returns_400` | Character/Npc | (Task 12) |

### `NpcAffectionsAndTraitsControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `AddAffection_then_ListAffections_returns_it_then_DeleteAffection_removes_it` | Character/Npc/Creature | (Task 12) |
| `AddTrait_lands_in_the_correct_bucket_with_correct_totals_and_DeleteTrait_removes_it` | Character/Npc/Creature | (Task 12) |
| `AddTrait_with_a_nonexistent_TraitId_returns_400` | Character/Npc/Creature | (Task 12) |
| `AddTrait_rejects_a_Positiva_that_would_push_the_side_total_past_the_budget` | Character/Npc/Creature | (Task 12) |
| `AddTrait_accepts_Negativas_up_to_twice_the_positive_budget_and_rejects_past_it` | Character/Npc/Creature | (Task 12) |
| `AddTrait_rejects_a_RequerEspecificacao_trait_added_without_an_Especificacao_and_persists_it_when_provided` | Npc/Creature | (Task 12) |
| `Affection_and_trait_actions_by_a_different_gm_return_404` | Npc/Creature | (Task 12) |
| `AddTrait_with_a_creature_exclusive_characteristic_Id_returns_400` | Character/Npc | (Task 12) |

### `CreatureAffectionsAndTraitsControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `AddAffection_then_ListAffections_returns_it_then_DeleteAffection_removes_it` | Character/Npc/Creature | (Task 12) |
| `AddTrait_lands_in_the_correct_bucket_with_correct_totals_and_DeleteTrait_removes_it` | Character/Npc/Creature | (Task 12) |
| `AddTrait_rejects_a_characteristic_of_the_normal_catalog` | Creature | (Task 12) |
| `AddTrait_with_a_nonexistent_TraitId_returns_400` | Character/Npc/Creature | (Task 12) |
| `AddTrait_rejects_a_Positiva_that_would_push_the_side_total_past_the_budget` | Character/Npc/Creature | (Task 12) |
| `AddTrait_accepts_Negativas_up_to_twice_the_positive_budget_and_rejects_past_it` | Character/Npc/Creature | (Task 12) |
| `AddTrait_rejects_a_RequerEspecificacao_trait_added_without_an_Especificacao_and_persists_it_when_provided` | Npc/Creature | (Task 12) |
| `AddTrait_resolves_a_creature_exclusive_characteristic_and_it_lands_in_ListTraits` | Creature | (Task 12) |
| `AddTrait_sums_a_normal_and_a_creature_exclusive_characteristic_into_the_same_Positivas_budget` | Creature | (Task 12) |
| `AddTrait_rejects_a_creature_exclusive_pick_that_requires_Especificacao_without_one` | Creature | (Task 12) |
| `Affection_and_trait_actions_by_a_different_gm_return_404` | Npc/Creature | (Task 12) |

## SpellAbilities

### `CharacterSpellAbilitiesControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `AddFromScratch_creates_the_sheet_entry_and_an_independent_bank_copy` | Character/Npc/Creature | (Task 12) |
| `AddFromBankEntry_copies_every_field_and_reuses_the_bank_entry_without_duplicating_it` | Character/Npc/Creature | (Task 12) |
| `Delete_removes_only_the_sheet_copy` | Character/Npc/Creature | (Task 12) |
| `Add_without_either_SourceBankEntryId_or_from_scratch_fields_returns_400` | Character/Npc/Creature | (Task 12) |
| `Add_by_an_unrelated_jogador_returns_403` | Character | (Task 12) |
| `AddFromScratch_by_the_player_auto_attaches_the_bank_copy_as_public` | Character | (Task 12) |
| `Add_from_scratch_with_an_effect_missing_a_prerequisite_returns_400` | Character/Npc/Creature | (Task 12) |
| `AddFromScratch_by_the_gm_does_not_auto_attach` | Character | (Task 12) |
| `AddFromBankEntry_by_a_player_requires_the_entry_to_be_public_in_the_campaign` | Character/Npc/Creature | (Task 12) |
| `AddFromBankEntry_never_accepts_another_gms_entry_not_even_for_the_gm` | Character/Npc/Creature | (Task 12) |
| `AddFromScratch_links_the_sheet_entry_to_the_bank_entry_it_created` | Character/Npc/Creature | (Task 12) |
| `An_entry_built_from_scratch_is_flagged_CriadaNaFicha_and_one_from_the_bank_is_not` | Character/Npc/Creature | (Task 12) |
| `Update_by_the_owner_changes_a_from_scratch_entry_and_leaves_the_bank_copy_alone` | Character/Npc/Creature | (Task 12) |
| `Update_refuses_an_entry_that_came_from_the_bank` | Character/Npc/Creature | (Task 12) |
| `Update_validates_like_a_from_scratch_add` | Character/Npc/Creature | (Task 12) |
| `Update_by_someone_who_cannot_edit_the_sheet_is_refused_and_an_unknown_id_is_404` | Character | (Task 12) |
| `Add_is_refused_when_the_Gasto_em_PI_exceeds_the_balance_even_for_the_gm` | Character/Npc/Creature | (Task 12) |
| `Add_with_exactly_enough_debits_the_balance_to_zero_and_Delete_refunds_it` | Character/Npc/Creature | (Task 12) |
| `Add_from_the_bank_also_checks_and_debits` | Character | (Task 12) |
| `Update_settles_only_the_difference_in_both_directions` | Character/Npc/Creature | (Task 12) |
| `Deleting_an_entry_that_was_never_debited_still_refunds_its_Gasto_em_PI` | Character | (Task 12) |
| `Update_refuses_a_from_scratch_entry_whose_stored_Tipo_does_not_consume_PI` | Character/Npc/Creature | (Task 12) |
| `A_stale_sheet_form_cannot_erase_a_debit` | Character/Npc/Creature | (Task 12) |

### `NpcSpellAbilitiesControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `AddFromScratch_creates_the_sheet_entry_and_an_independent_bank_copy` | Character/Npc/Creature | (Task 12) |
| `AddFromBankEntry_copies_every_field_and_reuses_the_bank_entry_without_duplicating_it` | Character/Npc/Creature | (Task 12) |
| `Delete_removes_only_the_sheet_copy` | Character/Npc/Creature | (Task 12) |
| `Add_without_either_SourceBankEntryId_or_from_scratch_fields_returns_400` | Character/Npc/Creature | (Task 12) |
| `Add_by_a_different_gm_returns_404` | Npc/Creature | (Task 12) |
| `AddFromScratch_by_the_granted_player_auto_attaches_the_bank_copy_as_public` | Npc/Creature | (Task 12) |
| `Add_from_scratch_with_an_effect_missing_a_prerequisite_returns_400` | Character/Npc/Creature | (Task 12) |
| `AddFromScratch_by_the_gm_on_their_own_npc_does_not_auto_attach` | Npc | (Task 12) |
| `AddFromBankEntry_by_a_player_requires_the_entry_to_be_public_in_the_campaign` | Character/Npc/Creature | (Task 12) |
| `AddFromBankEntry_never_accepts_another_gms_entry_not_even_for_the_gm` | Character/Npc/Creature | (Task 12) |
| `AddFromScratch_links_the_sheet_entry_to_the_bank_entry_it_created` | Character/Npc/Creature | (Task 12) |
| `An_entry_built_from_scratch_is_flagged_CriadaNaFicha_and_one_from_the_bank_is_not` | Character/Npc/Creature | (Task 12) |
| `Update_by_the_owner_changes_a_from_scratch_entry_and_leaves_the_bank_copy_alone` | Character/Npc/Creature | (Task 12) |
| `Update_refuses_an_entry_that_came_from_the_bank` | Character/Npc/Creature | (Task 12) |
| `Update_validates_like_a_from_scratch_add` | Character/Npc/Creature | (Task 12) |
| `Update_by_someone_who_cannot_edit_the_sheet_is_404_and_an_unknown_id_is_404` | Npc/Creature | (Task 12) |
| `Add_is_refused_when_the_Gasto_em_PI_exceeds_the_balance_even_for_the_gm` | Character/Npc/Creature | (Task 12) |
| `Add_with_exactly_enough_debits_the_balance_to_zero_and_Delete_refunds_it` | Character/Npc/Creature | (Task 12) |
| `Update_settles_only_the_difference_in_both_directions` | Character/Npc/Creature | (Task 12) |
| `Update_refuses_a_from_scratch_entry_whose_stored_Tipo_does_not_consume_PI` | Character/Npc/Creature | (Task 12) |
| `A_stale_sheet_form_cannot_erase_a_debit` | Character/Npc/Creature | (Task 12) |

### `CreatureSpellAbilitiesControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `AddFromScratch_creates_the_sheet_entry_and_an_independent_bank_copy` | Character/Npc/Creature | (Task 12) |
| `AddFromBankEntry_copies_every_field_and_reuses_the_bank_entry_without_duplicating_it` | Character/Npc/Creature | (Task 12) |
| `Delete_removes_only_the_sheet_copy` | Character/Npc/Creature | (Task 12) |
| `Add_without_either_SourceBankEntryId_or_from_scratch_fields_returns_400` | Character/Npc/Creature | (Task 12) |
| `Add_by_a_different_gm_returns_404` | Npc/Creature | (Task 12) |
| `AddFromScratch_by_the_granted_player_auto_attaches_the_bank_copy_as_public` | Npc/Creature | (Task 12) |
| `Add_from_scratch_with_an_effect_missing_a_prerequisite_returns_400` | Character/Npc/Creature | (Task 12) |
| `AddFromScratch_by_the_gm_on_their_own_creature_does_not_auto_attach` | Creature | (Task 12) |
| `AddFromBankEntry_by_a_player_requires_the_entry_to_be_public_in_the_campaign` | Character/Npc/Creature | (Task 12) |
| `AddFromBankEntry_never_accepts_another_gms_entry_not_even_for_the_gm` | Character/Npc/Creature | (Task 12) |
| `AddFromScratch_links_the_sheet_entry_to_the_bank_entry_it_created` | Character/Npc/Creature | (Task 12) |
| `An_entry_built_from_scratch_is_flagged_CriadaNaFicha_and_one_from_the_bank_is_not` | Character/Npc/Creature | (Task 12) |
| `Update_by_the_owner_changes_a_from_scratch_entry_and_leaves_the_bank_copy_alone` | Character/Npc/Creature | (Task 12) |
| `Update_refuses_an_entry_that_came_from_the_bank` | Character/Npc/Creature | (Task 12) |
| `Update_validates_like_a_from_scratch_add` | Character/Npc/Creature | (Task 12) |
| `Update_by_someone_who_cannot_edit_the_sheet_is_404_and_an_unknown_id_is_404` | Npc/Creature | (Task 12) |
| `Add_is_refused_when_the_Gasto_em_PI_exceeds_the_balance_even_for_the_gm` | Character/Npc/Creature | (Task 12) |
| `Add_with_exactly_enough_debits_the_balance_to_zero_and_Delete_refunds_it` | Character/Npc/Creature | (Task 12) |
| `Update_settles_only_the_difference_in_both_directions` | Character/Npc/Creature | (Task 12) |
| `Update_refuses_a_from_scratch_entry_whose_stored_Tipo_does_not_consume_PI` | Character/Npc/Creature | (Task 12) |
| `A_stale_sheet_form_cannot_erase_a_debit` | Character/Npc/Creature | (Task 12) |

## Runes

### `CharacterRunesControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `Add_a_valid_rune_returns_201` | Character/Npc | (Task 12) |
| `List_returns_the_runes_on_the_sheet` | Character/Npc | (Task 12) |
| `Delete_an_existing_rune_returns_204_and_it_no_longer_appears_on_list` | Character/Npc | (Task 12) |
| `Add_by_an_unrelated_jogador_returns_403` | Character | (Task 12) |
| `A_rune_on_the_sheet_is_read_only_there_is_no_update_endpoint` | Character | (Task 12) |
| `A_rune_added_by_the_gm_lands_in_the_bank_but_is_not_published_to_the_campaign` | Character | (Task 12) |
| `A_rune_added_by_the_player_lands_in_the_bank_and_is_published_to_the_campaign` | Character | (Task 12) |
| `A_rune_picked_from_the_bank_copies_its_fields_without_duplicating_the_bank_entry` | Character/Npc | (Task 12) |
| `A_rune_from_scratch_with_a_tipo_returns_it_and_the_bank_copy_carries_it` | Character/Npc | (Task 12) |
| `A_rune_without_tipo_has_a_null_tipo` | Character | (Task 12) |
| `A_rune_with_an_unknown_tipo_returns_400_and_creates_nothing` | Character/Npc | (Task 12) |
| `A_rune_picked_from_the_bank_copies_the_tipo` | Character/Npc | (Task 12) |
| `Available_runes_for_a_player_include_the_tipo` | Character | (Task 12) |
| `A_player_can_only_pick_bank_entries_the_gm_published_to_the_campaign` | Character/Npc | (Task 12) |
| `Adding_with_both_paths_or_with_neither_returns_400` | Character/Npc | (Task 12) |
| `Picking_a_malformed_or_unknown_bank_entry_returns_400` | Character/Npc | (Task 12) |
| `Picking_another_gms_bank_entry_returns_400` | Character | (Task 12) |
| `A_rune_from_scratch_with_the_callers_own_image_returns_201_and_the_list_has_the_ImageUrl` | Character/Npc | (Task 12) |
| `A_rune_without_image_has_a_null_ImageUrl` | Character | (Task 12) |
| `The_automatic_bank_copy_carries_the_image` | Character/Npc | (Task 12) |
| `A_jogador_can_use_an_image_the_gm_published_in_the_campaign` | Character/Npc | (Task 12) |
| `A_jogador_cannot_use_a_non_public_image_of_the_gm` | Character/Npc | (Task 12) |
| `A_public_image_of_another_campaign_is_not_usable` | Character | (Task 12) |
| `A_malformed_or_unknown_image_id_returns_400` | Character/Npc | (Task 12) |
| `A_rune_picked_from_the_bank_inherits_the_entrys_image` | Character/Npc | (Task 12) |
| `Picking_from_the_bank_together_with_an_image_id_returns_400` | Character/Npc | (Task 12) |
| `A_rune_added_from_scratch_is_linked_to_the_bank_entry_it_created` | Character/Npc | (Task 12) |
| `Add_from_scratch_requires_a_disciplina_and_returns_it` | Character/Npc | (Task 12) |
| `Add_from_scratch_rejects_an_unknown_disciplina` | Character/Npc | (Task 12) |
| `Add_from_scratch_copies_the_disciplina_to_the_automatic_bank_entry` | Character/Npc | (Task 12) |
| `Add_from_the_bank_copies_the_entrys_disciplina_and_ignores_the_requests` | Character/Npc | (Task 12) |
| `Add_from_a_legacy_bank_entry_without_disciplina_is_accepted_and_stays_without` | Character/Npc | (Task 12) |

### `NpcRunesControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `Add_a_valid_rune_returns_201` | Character/Npc | (Task 12) |
| `List_returns_the_runes_on_the_sheet` | Character/Npc | (Task 12) |
| `Delete_an_existing_rune_returns_204_and_it_no_longer_appears_on_list` | Character/Npc | (Task 12) |
| `Add_by_a_different_gm_returns_404` | Npc | (Task 12) |
| `A_rune_added_by_the_gm_lands_in_the_bank` | Npc | (Task 12) |
| `A_rune_added_by_the_player_a_npc_was_granted_to_is_also_published_to_the_campaign` | Npc | (Task 12) |
| `A_rune_picked_from_the_bank_copies_its_fields_without_duplicating_the_bank_entry` | Character/Npc | (Task 12) |
| `A_rune_from_scratch_with_a_tipo_returns_it_and_the_bank_copy_carries_it` | Character/Npc | (Task 12) |
| `A_rune_with_an_unknown_tipo_returns_400_and_creates_nothing` | Character/Npc | (Task 12) |
| `A_rune_picked_from_the_bank_copies_the_tipo` | Character/Npc | (Task 12) |
| `A_player_can_only_pick_bank_entries_the_gm_published_to_the_campaign` | Character/Npc | (Task 12) |
| `Adding_with_both_paths_or_with_neither_returns_400` | Character/Npc | (Task 12) |
| `Picking_a_malformed_or_unknown_bank_entry_returns_400` | Character/Npc | (Task 12) |
| `A_rune_from_scratch_with_the_callers_own_image_returns_201_and_the_list_has_the_ImageUrl` | Character/Npc | (Task 12) |
| `The_automatic_bank_copy_carries_the_image` | Character/Npc | (Task 12) |
| `A_jogador_can_use_an_image_the_gm_published_in_the_campaign` | Character/Npc | (Task 12) |
| `A_jogador_cannot_use_a_non_public_image_of_the_gm` | Character/Npc | (Task 12) |
| `A_malformed_or_unknown_image_id_returns_400` | Character/Npc | (Task 12) |
| `A_rune_picked_from_the_bank_inherits_the_entrys_image` | Character/Npc | (Task 12) |
| `Picking_from_the_bank_together_with_an_image_id_returns_400` | Character/Npc | (Task 12) |
| `A_rune_added_from_scratch_is_linked_to_the_bank_entry_it_created` | Character/Npc | (Task 12) |
| `Add_from_scratch_requires_a_disciplina_and_returns_it` | Character/Npc | (Task 12) |
| `Add_from_scratch_rejects_an_unknown_disciplina` | Character/Npc | (Task 12) |
| `Add_from_scratch_copies_the_disciplina_to_the_automatic_bank_entry` | Character/Npc | (Task 12) |
| `Add_from_the_bank_copies_the_entrys_disciplina_and_ignores_the_requests` | Character/Npc | (Task 12) |
| `Add_from_a_legacy_bank_entry_without_disciplina_is_accepted_and_stays_without` | Character/Npc | (Task 12) |

## Affinities

### `CharacterAffinitiesControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `Add_a_valid_combination_returns_201` | Character/Npc | (Task 12) |
| `Delete_an_existing_affinity_returns_204_and_it_no_longer_appears_on_list` | Character/Npc | (Task 12) |
| `Add_by_an_unrelated_jogador_returns_403` | Character | (Task 12) |
| `List_by_an_unrelated_jogador_returns_403` | Character | (Task 12) |
| `Add_with_every_field_null_returns_201_and_a_blank_row` | Character/Npc | (Task 12) |
| `Add_with_only_Elemento_given_returns_201_without_checking_a_combination` | Character | (Task 12) |
| `Update_an_existing_affinity_returns_200_and_the_list_reflects_the_change` | Character/Npc | (Task 12) |
| `Update_can_clear_fields_back_to_null` | Character | (Task 12) |
| `Update_by_an_unrelated_jogador_returns_403` | Character | (Task 12) |
| `Update_a_nonexistent_affinity_returns_404` | Character | (Task 12) |
| `Update_does_not_flag_a_duplicate_against_its_own_row` | Character/Npc | (Task 12) |
| `Update_does_not_reject_a_pre_existing_duplicate_left_unchanged` | Character/Npc | (Task 12) |
| `Add_derives_the_SubElemento_from_the_two_essencias` | Character/Npc | (Task 12) |
| `Add_with_essencias_that_dont_cross_returns_400` | Character/Npc | (Task 12) |
| `Add_with_SegundaEssencia_but_no_Elemento_returns_400` | Character/Npc | (Task 12) |
| `Add_rejects_a_second_row_with_the_same_SubElemento_but_allows_repeating_Essencia1` | Character | (Task 12) |
| `Any_Vocacao_can_pick_any_essencias` | Character/Npc | (Task 12) |
| `Update_changing_only_values_keeps_a_legacy_SubElemento` | Character/Npc | (Task 12) |
| `Update_changing_an_essencia_recomputes_the_SubElemento` | Character/Npc | (Task 12) |
| `Add_up_to_the_vocacao_arcana_maximum_is_accepted_and_beyond_it_is_rejected` | Character | (Task 12) |
| `A_repeated_essencia_only_costs_its_highest_value` | Character | (Task 12) |
| `AfinidadeAdicional_raises_the_maximum` | Character | (Task 12) |
| `A_martial_vocacao_only_has_the_afinidade_adicional` | Character | (Task 12) |
| `Lowering_the_maximum_is_allowed_and_then_only_updates_that_lower_the_gasto_go_through` | Character | (Task 12) |
| `Negative_AfinidadeAdicional_is_rejected` | Character/Npc | (Task 12) |

### `NpcAffinitiesControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `Add_a_valid_combination_returns_201` | Character/Npc | (Task 12) |
| `Delete_an_existing_affinity_returns_204_and_it_no_longer_appears_on_list` | Character/Npc | (Task 12) |
| `Add_by_a_different_gm_returns_404` | Npc | (Task 12) |
| `List_by_a_different_gm_returns_404` | Npc | (Task 12) |
| `Add_with_every_field_null_returns_201_and_a_blank_row` | Character/Npc | (Task 12) |
| `Update_an_existing_affinity_returns_200_and_the_list_reflects_the_change` | Character/Npc | (Task 12) |
| `Update_by_a_different_gm_returns_404` | Npc | (Task 12) |
| `Update_does_not_flag_a_duplicate_against_its_own_row` | Character/Npc | (Task 12) |
| `Update_does_not_reject_a_pre_existing_duplicate_left_unchanged` | Character/Npc | (Task 12) |
| `Add_derives_the_SubElemento_from_the_two_essencias` | Character/Npc | (Task 12) |
| `Add_with_essencias_that_dont_cross_returns_400` | Character/Npc | (Task 12) |
| `Add_rejects_a_duplicate_SubElemento` | Npc | (Task 12) |
| `Add_with_SegundaEssencia_but_no_Elemento_returns_400` | Character/Npc | (Task 12) |
| `Any_Vocacao_can_pick_any_essencias` | Character/Npc | (Task 12) |
| `Update_changing_only_values_keeps_a_legacy_SubElemento` | Character/Npc | (Task 12) |
| `Update_changing_an_essencia_recomputes_the_SubElemento` | Character/Npc | (Task 12) |
| `Npc_affinities_beyond_the_vocacao_arcana_maximum_are_accepted_and_reported` | Npc | (Task 12) |
| `Negative_AfinidadeAdicional_is_rejected` | Character/Npc | (Task 12) |
| `SubAttributes_derive_EficienciaElemental_and_DanoElemental_from_the_points_in_the_Afinidade` | Npc | (Task 12) |

## Equipagem

### `CharacterEquipagemControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `ListKits_returns_every_seeded_kit_with_choice_slot_options_resolved_against_the_GM_s_catalog` | Character/Npc | (Task 12) |
| `Choose_a_kit_with_no_choice_slots_places_every_fixed_item_and_adds_Ciclos` | Character/Npc | (Task 12) |
| `Choose_a_kit_with_a_choice_slot_places_the_selected_weapon` | Character/Npc | (Task 12) |
| `Choose_Cacador_with_the_Arco_alternative_also_grants_10_Flechas_de_Madeira` | Character/Npc | (Task 12) |
| `Choose_a_second_time_on_an_already_chosen_sheet_returns_400` | Character/Npc | (Task 12) |
| `Choose_with_ChoiceSelections_omitted_from_the_request_body_returns_400_not_500` | Character/Npc | (Task 12) |
| `Choose_a_kit_that_would_push_an_Artefato_TipoDeAlvo_past_3_returns_400_and_grants_nothing` | Character/Npc | (Task 12) |
| `ListKits_and_Choose_by_an_unrelated_jogador_return_403` | Character | (Task 12) |
| `Choose_auto_attaches_every_granted_item_to_the_sheet_s_campaign_as_public` | Character/Npc | (Task 12) |
| `Choose_a_kit_with_an_Armadura_choice_slot_updates_the_target_armor_slot` | Character/Npc | (Task 12) |
| `Choose_a_kit_that_would_CREATE_an_Artefato_past_the_cap_of_3_returns_400_and_persists_nothing` | Character/Npc | (Task 12) |

### `NpcEquipagemControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `ListKits_returns_every_seeded_kit_with_choice_slot_options_resolved_against_the_GM_s_catalog` | Character/Npc | (Task 12) |
| `Choose_a_kit_with_no_choice_slots_places_every_fixed_item_and_adds_Ciclos` | Character/Npc | (Task 12) |
| `Choose_a_kit_with_a_choice_slot_places_the_selected_weapon` | Character/Npc | (Task 12) |
| `Choose_Cacador_with_the_Arco_alternative_also_grants_10_Flechas_de_Madeira` | Character/Npc | (Task 12) |
| `Choose_a_second_time_on_an_already_chosen_sheet_returns_400` | Character/Npc | (Task 12) |
| `Choose_with_ChoiceSelections_omitted_from_the_request_body_returns_400_not_500` | Character/Npc | (Task 12) |
| `Choose_a_kit_that_would_push_an_Artefato_TipoDeAlvo_past_3_returns_400_and_grants_nothing` | Character/Npc | (Task 12) |
| `ListKits_and_Choose_by_a_different_GM_return_404` | Npc | (Task 12) |
| `Choose_auto_attaches_every_granted_item_to_the_sheet_s_campaign_as_public` | Character/Npc | (Task 12) |
| `Choose_on_a_GM_owned_Npc_not_attached_to_any_campaign_skips_the_auto_attach_step_without_erroring` | Npc | (Task 12) |
| `Choose_a_kit_with_an_Armadura_choice_slot_updates_the_target_armor_slot` | Character/Npc | (Task 12) |
| `Choose_a_kit_that_would_CREATE_an_Artefato_past_the_cap_of_3_returns_400_and_persists_nothing` | Character/Npc | (Task 12) |

## Passivas

### `CharacterPassivasTests`

| Método | Existe em | Destino |
|---|---|---|
| `Building_a_passiva_from_scratch_on_a_sheet_is_refused` | Character | (Task 12) |
| `Unmet_requisitos_block_the_gm_and_the_player_alike` | Character | (Task 12) |
| `Met_requisitos_copy_categoria_and_requisitos_and_a_later_change_shows_the_warning` | Character | (Task 12) |
| `A_passiva_without_requisitos_is_always_addable` | Character | (Task 12) |
| `A_player_never_sees_the_requisitos_of_a_passiva_that_is_not_public` | Character | (Task 12) |
| `Passivas_disponiveis_lists_reachable_passivas_with_their_pendencias` | Character | (Task 12) |
| `Adding_the_same_passiva_twice_is_refused` | Character | (Task 12) |
| `A_historico_requisito_is_checked_and_named` | Character | (Task 12) |
| `A_passiva_does_not_touch_the_Pontos_de_Ignicao_balance_even_at_zero` | Character | (Task 12) |

### `NpcCreaturePassivasTests`

| Método | Existe em | Destino |
|---|---|---|
| `Npc_add_from_bank_checks_requisitos_and_from_scratch_passiva_is_refused` | NpcCreature | (Task 12) |
| `Creature_ignores_personagem_only_requisitos_but_checks_nivel` | NpcCreature | (Task 12) |
| `Npc_adding_the_same_passiva_twice_is_refused` | NpcCreature | (Task 12) |
| `Creature_adding_the_same_passiva_twice_is_refused` | NpcCreature | (Task 12) |
| `Granting_an_npc_copies_its_passivas_categoria_and_requisitos` | NpcCreature | (Task 12) |
| `Npc_passivas_disponiveis_for_the_granted_player_lists_only_the_campaign_public_ones` | NpcCreature | (Task 12) |
| `Granting_an_npc_whose_passiva_left_the_bank_recreates_it_with_categoria_and_requisitos` | NpcCreature | (Task 12) |

## Sheets

### `CharacterSheetsControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `Create_without_a_token_returns_401` | Character/Npc/Creature | (Task 12) |
| `Create_for_a_campaign_member_returns_201_with_Nivel_1_and_no_other_field_set` | Character | (Task 12) |
| `Create_for_a_non_member_returns_400` | Character | (Task 12) |
| `Create_by_a_jogador_returns_403` | Character/Npc/Creature | (Task 12) |
| `Create_for_a_campaign_owned_by_another_gm_returns_404` | Character | (Task 12) |
| `Create_with_a_malformed_OwnerId_returns_400` | Character | (Task 12) |
| `Delete_an_owned_sheet_returns_204` | Character | (Task 12) |
| `Delete_a_sheet_in_a_campaign_owned_by_another_gm_returns_404` | Character | (Task 12) |
| `Get_returns_the_sheet` | Character | (Task 12) |
| `Get_by_an_unrelated_jogador_returns_404` | Character | (Task 12) |
| `Get_by_an_unrelated_gm_returns_404` | Character | (Task 12) |
| `Get_a_nonexistent_sheet_returns_404` | Character/Npc/Creature | (Task 12) |
| `ListForCampaign_returns_the_campaigns_sheets` | Character | (Task 12) |
| `ListForCampaign_for_a_campaign_owned_by_another_gm_returns_404` | Character | (Task 12) |
| `Update_by_the_owner_returns_204_and_persists_every_field` | Character | (Task 12) |
| `Update_by_the_campaigns_gm_returns_204` | Character | (Task 12) |
| `Update_by_an_unrelated_jogador_returns_403` | Character | (Task 12) |
| `Update_with_a_Variante_that_does_not_belong_to_the_Linhagem_returns_400` | Character/Npc | (Task 12) |
| `EAPAtual_is_the_level_base_plus_the_Ambares_by_Rank` | Character | (Task 12) |
| `Get_labels_Graduacao_as_Grau_for_Campeao_and_computes_it_from_EAP` | Character/Npc | (Task 12) |
| `Get_gives_a_fresh_Campeao_at_zero_XP_Grau_1` | Character | (Task 12) |
| `Get_labels_Graduacao_as_Circulo_for_a_magic_vocacao` | Character | (Task 12) |
| `Get_computes_vitalidade_and_foco_maximo_from_vigor_astucia_and_vocacao` | Character/Npc | (Task 12) |
| `Get_computes_AdrenalinaMaximo_with_an_equipped_SubAtributo_Artefato` | Character/Npc/Creature | (Task 12) |
| `ModificadorDeDano_sums_equipped_Dano_Artefatos_per_TipoDeDano` | Character/Npc/Creature | (Task 12) |
| `Update_clamps_every_Atual_resource_to_its_own_Maximo_instead_of_rejecting` | Character | (Task 12) |
| `LevelUpNotice_lists_bonus_text_for_every_level_gained_since_the_last_dismissal` | Character/Npc/Creature | (Task 12) |
| `LevelUpNotice_after_jumping_from_Nivel_1_to_3_sums_Pontos_de_Atributo_into_one_line` | Character/Npc | (Task 12) |
| `Dismiss_stops_the_dismissed_levels_from_reappearing` | Character/Npc/Creature | (Task 12) |
| `Dismiss_by_an_unrelated_jogador_returns_403` | Character | (Task 12) |
| `Update_with_a_malformed_ImageId_returns_400` | Character | (Task 12) |
| `Update_with_a_malformed_Cobertura_returns_400` | Character | (Task 12) |
| `Update_with_a_garbage_Vocacao_returns_400_instead_of_silently_saving_null` | Character | (Task 12) |
| `Update_with_a_garbage_Afinidade_returns_400_instead_of_silently_saving_null` | Character | (Task 12) |
| `Update_with_a_garbage_Estrela_returns_400_instead_of_silently_saving_null` | Character/Npc | (Task 12) |
| `Update_with_SinaAtual_outside_0_to_3_returns_400` | Character/Npc | (Task 12) |
| `Update_persists_Estrela_and_SinaAtual` | Character/Npc | (Task 12) |
| `Update_with_a_garbage_HistoricoId_returns_400` | Character/Npc | (Task 12) |
| `Update_with_a_HistoricoId_that_does_not_exist_returns_400` | Character/Npc | (Task 12) |
| `Update_persists_a_valid_HistoricoId` | Character/Npc | (Task 12) |
| `Update_never_changes_EquipmentKitId_even_if_the_request_tries_to` | Character/Npc | (Task 12) |
| `GetSheet_surfaces_the_sheet_s_real_EquipmentKitId_once_one_is_set` | Character/Npc | (Task 12) |
| `Update_accepts_any_Afinidade_whatever_the_Vocacao` | Character/Npc | (Task 12) |
| `Update_allows_an_Afinidade_liberada_pela_Vocacao_atual` | Character/Npc | (Task 12) |
| `Update_keeps_an_old_Afinidade_that_no_longer_fits_a_new_Vocacao_when_resubmitted_unchanged` | Character/Npc | (Task 12) |
| `Create_seeds_8_zeroed_attributes_and_39_zeroed_skills` | Character | (Task 12) |
| `SubAttributes_computes_from_attributes_and_arsenal` | Character/Npc/Creature | (Task 12) |
| `SubAttributes_includes_Bruto_Prontidao_Reflexos_and_Fortitude_from_their_skill_Modificador` | Character/Npc/Creature | (Task 12) |
| `SubAttributes_includes_the_Historico_bonus_on_Prontidao_and_Reflexos` | Character/Npc | (Task 12) |
| `SubAttributes_includes_equipped_armor_RF_and_RM` | Character/Npc/Creature | (Task 12) |
| `SubAttributes_sums_Artefatos_targeting_SubAtributo_terms` | Character | (Task 12) |
| `SubAttributes_PesoAtual_includes_Inventario_items` | Character/Npc | (Task 12) |
| `SubAttributes_PesoAtual_excludes_equipped_weapons_and_shields_but_includes_unequipped_ones` | Character/Npc | (Task 12) |
| `SubAttributes_PesoAtual_never_counts_armor` | Character/Npc | (Task 12) |
| `SubAttributes_campaign_BonusDeCarga_is_added_to_PesoMaximo_and_reported` | Character | (Task 12) |
| `SubAttributes_negative_campaign_BonusDeCarga_lowers_PesoMaximo_but_never_below_zero` | Character | (Task 12) |
| `SubAttributes_Movimentacao_overweight_penalty_follows_the_campaign_BonusDeCarga` | Character | (Task 12) |
| `SubAttributes_Capacidade_Extra_raises_PesoMaximo_and_its_own_Peso_is_excluded_from_PesoAtual` | Character/Npc | (Task 12) |
| `SubAttributes_by_an_unrelated_jogador_returns_403` | Character | (Task 12) |
| `SubAttributes_computes_EficienciaElemental_and_DanoElemental_from_the_matching_Afinidade_row` | Character | (Task 12) |
| `SubAttributes_follow_an_edit_made_by_the_Auditor_to_the_Tabela_de_Afinidades` | Character | (Task 12) |
| `SubAttributes_EficienciaElemental_and_DanoElemental_are_0_when_no_row_matches_Afinidade` | Character | (Task 12) |
| `RacialAbility_is_null_before_a_Variante_is_chosen_and_populated_after` | Character/Npc | (Task 12) |
| `RacialAbility_uses_the_GMs_override_instead_of_the_default_when_one_exists` | Character | (Task 12) |
| `RacialAbility_resolves_the_Arca_for_a_Humano_sheet_that_has_rolled` | Character/Npc | (Task 12) |
| `RacialAbility_lists_only_Arca_evolucoes_unlocked_by_the_character_level` | Character | (Task 12) |
| `RacialAbility_reports_a_null_Arca_when_the_GM_hasnt_registered_that_roll` | Character | (Task 12) |
| `Update_accepts_ArcaRolada_20_and_rejects_21_under_the_default_die` | Character/Npc | (Task 12) |
| `Update_rejects_ArcaRolada_7_under_D6` | Character/Npc | (Task 12) |
| `A_stored_ArcaRolada_above_a_shrunken_die_is_reported_out_of_the_table_and_does_not_block_saving_other_fields` | Character/Npc | (Task 12) |
| `RacialAbility_default_text_follows_the_GMs_die` | Character | (Task 12) |
| `ListMine_returns_the_players_own_sheets_across_every_campaign_with_the_campaigns_name` | Character | (Task 12) |
| `ListMine_does_not_include_another_players_sheets` | Character | (Task 12) |
| `ListMine_by_a_gm_returns_403` | Character | (Task 12) |
| `Update_rejects_a_new_Afinidade_of_Alma_or_Vida` | Character/Npc/Creature | (Task 12) |
| `Update_keeps_a_legacy_Afinidade_of_Vida_when_resubmitted_unchanged` | Character/Npc/Creature | (Task 12) |
| `A_new_sheet_starts_with_the_level_1_Pontos_de_Ignicao` | Character | (Task 12) |
| `Levelling_up_adds_the_gained_levels_Pontos_de_Ignicao_to_Atual` | Character | (Task 12) |
| `Saving_again_at_the_same_level_does_not_add_the_points_twice` | Character | (Task 12) |
| `Losing_levels_subtracts_the_same_points_and_never_goes_below_zero` | Character | (Task 12) |
| `A_manual_bonus_delta_reaches_Atual_as_well_as_the_total` | Character | (Task 12) |
| `The_generic_update_ignores_the_PontosDeIgnicaoAtual_of_the_request` | Character | (Task 12) |
| `A_stale_form_after_a_level_up_applies_the_level_delta_to_the_stored_balance` | Character | (Task 12) |
| `The_pontos_de_ignicao_endpoint_sets_the_balance_for_the_owning_player_and_the_gm` | Character | (Task 12) |
| `The_pontos_de_ignicao_endpoint_refuses_a_negative_value` | Character/Npc | (Task 12) |
| `The_pontos_de_ignicao_endpoint_refuses_an_unrelated_jogador_and_an_unknown_sheet` | Character | (Task 12) |
| `UpdateHistoria_by_the_owner_persists_sanitized_html_and_Get_returns_it` | Character | (Task 12) |
| `UpdateHistoria_by_the_campaign_gm_is_allowed` | Character | (Task 12) |
| `UpdateHistoria_by_an_unrelated_jogador_returns_403` | Character | (Task 12) |
| `UpdateHistoria_on_a_nonexistent_sheet_returns_404` | Character | (Task 12) |
| `UpdateHistoria_accepts_exactly_200000_characters_and_rejects_200001` | Character | (Task 12) |
| `UpdateHistoria_rejects_input_that_grows_past_200000_characters_when_sanitized` | Character | (Task 12) |
| `UpdateHistoria_with_only_empty_markup_stores_null` | Character | (Task 12) |
| `The_general_sheet_Update_leaves_Historia_untouched` | Character | (Task 12) |

### `NpcSheetsControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `Create_without_a_token_returns_401` | Character/Npc/Creature | (Task 12) |
| `Create_returns_201_with_Nivel_1` | Npc/Creature | (Task 12) |
| `Create_by_a_jogador_returns_403` | Character/Npc/Creature | (Task 12) |
| `Get_by_the_owning_gm_returns_200` | Npc/Creature | (Task 12) |
| `Get_by_a_different_gm_returns_404` | Npc/Creature | (Task 12) |
| `Get_and_Update_by_the_player_the_sheet_was_granted_to_both_succeed` | Npc/Creature | (Task 12) |
| `Get_by_a_jogador_the_sheet_was_never_granted_to_returns_404` | Npc/Creature | (Task 12) |
| `Update_by_the_owning_gm_returns_204_and_persists_every_field` | Npc/Creature | (Task 12) |
| `Update_persists_Estrela_and_SinaAtual` | Character/Npc | (Task 12) |
| `Update_with_a_garbage_HistoricoId_returns_400` | Character/Npc | (Task 12) |
| `Update_with_a_HistoricoId_that_does_not_exist_returns_400` | Character/Npc | (Task 12) |
| `Update_persists_a_valid_HistoricoId` | Character/Npc | (Task 12) |
| `Update_never_changes_EquipmentKitId_even_if_the_request_tries_to` | Character/Npc | (Task 12) |
| `GetSheet_surfaces_the_sheet_s_real_EquipmentKitId_once_one_is_set` | Character/Npc | (Task 12) |
| `Update_with_a_garbage_Estrela_returns_400_instead_of_silently_saving_null` | Character/Npc | (Task 12) |
| `Update_with_SinaAtual_outside_0_to_3_returns_400` | Character/Npc | (Task 12) |
| `Get_by_the_gm_computes_Abate_and_Assistencia_from_ExperienciaAtual` | Npc | (Task 12) |
| `Abate_and_Assistencia_follow_ExperienciaAtual_and_Nivel_changes` | Npc | (Task 12) |
| `Get_by_the_player_the_sheet_was_granted_to_hides_Abate_and_Assistencia` | Npc | (Task 12) |
| `UpdateNivel_pulls_ExperienciaAtual_to_that_levels_minimum_XP` | Npc/Creature | (Task 12) |
| `UpdateExperienciaAtual_pulls_Nivel_via_the_same_rule_as_Ficha_de_Personagem` | Npc/Creature | (Task 12) |
| `UpdateNivel_by_a_different_gm_returns_404` | Npc/Creature | (Task 12) |
| `LevelUpNotice_lists_bonus_text_for_every_level_gained_since_the_last_dismissal` | Character/Npc/Creature | (Task 12) |
| `LevelUpNotice_after_jumping_from_Nivel_1_to_3_sums_Pontos_de_Atributo_into_one_line` | Character/Npc | (Task 12) |
| `Dismiss_stops_the_dismissed_levels_from_reappearing` | Character/Npc/Creature | (Task 12) |
| `Dismiss_by_a_different_gm_returns_404` | Npc/Creature | (Task 12) |
| `Update_by_a_different_gm_returns_404` | Npc/Creature | (Task 12) |
| `Delete_by_the_owning_gm_returns_204` | Npc/Creature | (Task 12) |
| `Delete_by_a_different_gm_returns_404` | Npc/Creature | (Task 12) |
| `Get_a_nonexistent_sheet_returns_404` | Character/Npc/Creature | (Task 12) |
| `Update_with_a_Variante_that_does_not_belong_to_the_Linhagem_returns_400` | Character/Npc | (Task 12) |
| `Update_accepts_any_Afinidade_whatever_the_Vocacao` | Character/Npc | (Task 12) |
| `Update_allows_an_Afinidade_liberada_pela_Vocacao_atual` | Character/Npc | (Task 12) |
| `Update_keeps_an_old_Afinidade_that_no_longer_fits_a_new_Vocacao_when_resubmitted_unchanged` | Character/Npc | (Task 12) |
| `Create_seeds_8_zeroed_attributes_39_zeroed_skills_and_3_armor_slots` | Npc | (Task 12) |
| `Get_labels_Graduacao_as_Grau_for_Campeao_and_computes_it_from_EAP` | Character/Npc | (Task 12) |
| `Graduacao_of_a_Cacador_at_Nivel_1_is_Grau_1_and_a_Feiticeiro_without_coracao_stays_0` | Npc | (Task 12) |
| `EAPAtual_is_the_level_base_ignoring_the_EAPAtual_sent` | Npc | (Task 12) |
| `EAPAtual_adds_the_Ambares_by_Rank` | Npc | (Task 12) |
| `UpdateNivel_changes_the_computed_EAPAtual` | Npc | (Task 12) |
| `Graduacao_follows_the_computed_EAP_not_the_EAPAtual_sent` | Npc | (Task 12) |
| `List_returns_only_the_callers_own_npcs` | Npc | (Task 12) |
| `List_shows_OwnerNickname_for_a_granted_sheet_and_null_for_the_GMs_own` | Npc/Creature | (Task 12) |
| `List_can_filter_by_Variante` | Npc | (Task 12) |
| `List_can_filter_by_partial_Nome_Linhagem_and_Nivel_together` | Npc | (Task 12) |
| `RacialAbility_is_null_before_a_Variante_is_chosen_and_populated_after` | Character/Npc | (Task 12) |
| `RacialAbility_resolves_the_Arca_for_a_Humano_sheet_that_has_rolled` | Character/Npc | (Task 12) |
| `Update_accepts_ArcaRolada_20_and_rejects_21_under_the_default_die` | Character/Npc | (Task 12) |
| `Update_rejects_ArcaRolada_7_under_D6` | Character/Npc | (Task 12) |
| `A_stored_ArcaRolada_above_a_shrunken_die_is_reported_out_of_the_table_and_does_not_block_saving_other_fields` | Character/Npc | (Task 12) |
| `RacialAbility_lists_only_Arca_evolucoes_unlocked_by_the_npc_level` | Npc | (Task 12) |
| `RacialAbility_has_no_Arca_evolucoes_when_the_npc_is_not_Humano` | Npc | (Task 12) |
| `RacialAbility_treats_an_Arca_created_only_by_an_evolucao_as_not_registered_but_lists_the_evolucao` | Npc | (Task 12) |
| `RacialAbility_by_a_different_gm_returns_404` | Npc | (Task 12) |
| `SubAttributes_computes_from_attributes_and_arsenal` | Character/Npc/Creature | (Task 12) |
| `SubAttributes_includes_Bruto_Prontidao_Reflexos_and_Fortitude_from_their_skill_Modificador` | Character/Npc/Creature | (Task 12) |
| `SubAttributes_includes_the_Historico_bonus_on_Prontidao_and_Reflexos` | Character/Npc | (Task 12) |
| `SubAttributes_sums_an_equipped_SubAtributo_Artefato_into_the_matching_term` | Npc/Creature | (Task 12) |
| `SubAttributes_includes_equipped_armor_RF_and_RM` | Character/Npc/Creature | (Task 12) |
| `SubAttributes_PesoAtual_includes_Inventario_items` | Character/Npc | (Task 12) |
| `SubAttributes_PesoAtual_excludes_equipped_weapons_and_shields_but_includes_unequipped_ones` | Character/Npc | (Task 12) |
| `SubAttributes_PesoAtual_never_counts_armor` | Character/Npc | (Task 12) |
| `SubAttributes_Capacidade_Extra_raises_PesoMaximo_and_its_own_Peso_is_excluded_from_PesoAtual` | Character/Npc | (Task 12) |
| `SubAttributes_PesoMaximo_ignores_the_BonusDeCarga_of_the_gms_campaigns` | Npc | (Task 12) |
| `SubAttributes_by_a_different_gm_returns_404` | Npc/Creature | (Task 12) |
| `Get_computes_vitalidade_and_foco_maximo_from_vigor_astucia_and_vocacao` | Character/Npc | (Task 12) |
| `Get_computes_AdrenalinaMaximo_with_an_equipped_SubAtributo_Artefato` | Character/Npc/Creature | (Task 12) |
| `ModificadorDeDano_sums_equipped_Dano_Artefatos_per_TipoDeDano` | Character/Npc/Creature | (Task 12) |
| `CampaignId_is_populated_for_a_granted_sheet_and_null_for_an_ungranted_one` | Npc/Creature | (Task 12) |
| `Update_rejects_a_new_Afinidade_of_Alma_or_Vida` | Character/Npc/Creature | (Task 12) |
| `Update_keeps_a_legacy_Afinidade_of_Vida_when_resubmitted_unchanged` | Character/Npc/Creature | (Task 12) |
| `A_new_npc_starts_with_the_level_1_Pontos_de_Ignicao` | Npc | (Task 12) |
| `Changing_the_npc_level_through_the_nivel_endpoint_moves_Atual` | Npc | (Task 12) |
| `Changing_the_npc_experience_moves_Atual_with_the_level_it_computes` | Npc | (Task 12) |
| `The_full_npc_update_applies_the_level_delta_and_ignores_PontosDeIgnicaoTotal` | Npc | (Task 12) |
| `The_full_npc_update_ignores_the_PontosDeIgnicaoAtual_of_the_request` | Npc | (Task 12) |
| `The_pontos_de_ignicao_endpoint_sets_the_balance_for_the_gm_and_the_granted_player` | Npc | (Task 12) |
| `The_pontos_de_ignicao_endpoint_refuses_a_negative_value` | Character/Npc | (Task 12) |
| `The_pontos_de_ignicao_endpoint_is_404_for_a_stranger_and_for_an_unknown_sheet` | Npc | (Task 12) |
| `A_blank_npc_granted_to_a_player_also_starts_with_the_level_1_points` | Npc | (Task 12) |
| `UpdateHistoria_by_the_gm_persists_sanitized_html` | Npc | (Task 12) |
| `UpdateHistoria_by_a_different_gm_returns_404` | Npc | (Task 12) |
| `UpdateHistoria_by_the_player_the_npc_was_granted_to_is_allowed` | Npc | (Task 12) |
| `UpdateHistoria_longer_than_200000_characters_returns_400` | Npc | (Task 12) |
| `The_general_npc_Update_leaves_Historia_untouched` | Npc | (Task 12) |

### `CreatureSheetsControllerTests`

| Método | Existe em | Destino |
|---|---|---|
| `Create_without_a_token_returns_401` | Character/Npc/Creature | (Task 12) |
| `Create_returns_201_with_Nivel_1` | Npc/Creature | (Task 12) |
| `Create_by_a_jogador_returns_403` | Character/Npc/Creature | (Task 12) |
| `Get_by_the_owning_gm_returns_200` | Npc/Creature | (Task 12) |
| `Get_by_a_different_gm_returns_404` | Npc/Creature | (Task 12) |
| `Get_and_Update_by_the_player_the_sheet_was_granted_to_both_succeed` | Npc/Creature | (Task 12) |
| `Get_by_a_jogador_the_sheet_was_never_granted_to_returns_404` | Npc/Creature | (Task 12) |
| `Update_by_the_owning_gm_returns_204_and_persists_every_field` | Npc/Creature | (Task 12) |
| `Update_does_not_change_PontosDeIgnicao` | Creature | (Task 12) |
| `UpdatePontosDeIgnicao_sets_the_balance_for_the_gm_and_the_granted_player` | Creature | (Task 12) |
| `UpdatePontosDeIgnicao_refuses_a_negative_value` | Creature | (Task 12) |
| `UpdatePontosDeIgnicao_is_404_for_a_stranger_and_for_an_unknown_sheet` | Creature | (Task 12) |
| `UpdateNivel_pulls_ExperienciaAtual_to_that_levels_minimum_XP` | Npc/Creature | (Task 12) |
| `UpdateExperienciaAtual_pulls_Nivel_via_the_same_rule_as_Ficha_de_Personagem` | Npc/Creature | (Task 12) |
| `UpdateNivel_by_a_different_gm_returns_404` | Npc/Creature | (Task 12) |
| `LevelUpNotice_lists_bonus_text_for_every_level_gained_since_the_last_dismissal` | Character/Npc/Creature | (Task 12) |
| `Dismiss_stops_the_dismissed_levels_from_reappearing` | Character/Npc/Creature | (Task 12) |
| `Dismiss_by_a_different_gm_returns_404` | Npc/Creature | (Task 12) |
| `Update_by_a_different_gm_returns_404` | Npc/Creature | (Task 12) |
| `Delete_by_the_owning_gm_returns_204` | Npc/Creature | (Task 12) |
| `Delete_by_a_different_gm_returns_404` | Npc/Creature | (Task 12) |
| `Get_a_nonexistent_sheet_returns_404` | Character/Npc/Creature | (Task 12) |
| `Create_seeds_6_zeroed_attributes_20_zeroed_skills_and_3_armor_slots` | Creature | (Task 12) |
| `Get_computes_Abate_and_Assistencia_from_ExperienciaAtual` | Creature | (Task 12) |
| `Get_computes_Vitalidade_Foco_and_Adrenalina_Maximo_for_Fisico_Arquetipo` | Creature | (Task 12) |
| `Get_computes_AdrenalinaMaximo_with_an_equipped_SubAtributo_Artefato` | Character/Npc/Creature | (Task 12) |
| `ModificadorDeDano_sums_equipped_Dano_Artefatos_per_TipoDeDano` | Character/Npc/Creature | (Task 12) |
| `Get_computes_Vitalidade_and_Foco_Maximo_for_Arcano_Arquetipo` | Creature | (Task 12) |
| `Get_computes_Vitalidade_and_Foco_Maximo_from_real_Vigor_and_Astucia` | Creature | (Task 12) |
| `SubAttributes_computes_from_attributes_and_arsenal` | Character/Npc/Creature | (Task 12) |
| `SubAttributes_includes_Bruto_Prontidao_Reflexos_and_Fortitude_from_their_skill_Modificador` | Character/Npc/Creature | (Task 12) |
| `SubAttributes_sums_an_equipped_SubAtributo_Artefato_into_the_matching_term` | Npc/Creature | (Task 12) |
| `SubAttributes_includes_equipped_armor_RF_and_RM` | Character/Npc/Creature | (Task 12) |
| `SubAttributes_by_a_different_gm_returns_404` | Npc/Creature | (Task 12) |
| `SubAttributes_EficienciaElemental_and_DanoElemental_are_null_for_Criatura` | Creature | (Task 12) |
| `List_returns_only_the_callers_own_creatures` | Creature | (Task 12) |
| `List_shows_OwnerNickname_for_a_granted_sheet_and_null_for_the_GMs_own` | Npc/Creature | (Task 12) |
| `List_can_filter_by_partial_Nome_Raca_Arquetipo_and_Rank_together` | Creature | (Task 12) |
| `CampaignId_is_populated_for_a_granted_sheet_and_null_for_an_ungranted_one` | Npc/Creature | (Task 12) |
| `Update_rejects_a_new_Afinidade_of_Alma_or_Vida` | Character/Npc/Creature | (Task 12) |
| `Update_keeps_a_legacy_Afinidade_of_Vida_when_resubmitted_unchanged` | Character/Npc/Creature | (Task 12) |

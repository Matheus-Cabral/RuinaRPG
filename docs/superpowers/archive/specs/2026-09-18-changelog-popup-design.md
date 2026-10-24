# Popup de Changelog para GM — Design

## Contexto

Muitas mudanças foram entregues desde 2026-09-14 (catálogo de Efeitos com custeio automático,
Afinidades restritas por Vocação, cadastro inline de itens, Características exclusivas de
Criatura, reorganização das Fichas de NPC/Criatura, aviso de Level Up estendido, e várias
correções) sem nenhum mecanismo para avisar o GM sobre o que mudou. O usuário pediu uma popup,
exibida uma única vez por versão, com esse changelog — só na conta de GM.

Não existe hoje nenhum conceito de "versão do app" no código — este design introduz o primeiro.

## Objetivo

1. Uma constante única de versão atual (`"1.2.0"`, cobrindo os commits desde 2026-09-14).
2. Uma popup exibida ao GM assim que ele acessa o Painel após o login, contendo o changelog dessa
   versão, **uma única vez** — depois de fechada, nunca mais aparece para essa versão.
3. Persistência por usuário (banco de dados, não localStorage) — assim como
   `ApplicationUser.IsRulesAuditor`/`CharacterSheet.LastDismissedLevelUpLevel` já fazem, o estado
   "já viu" sobrevive a troca de navegador/dispositivo.
4. Exclusivo para contas GM — um Jogador nunca vê essa popup.

## Fora de escopo

- Changelog não é editável pelo GM nem por ninguém em runtime — é conteúdo estático no código
  (uma lista `const`/hardcoded), igual a outros textos fixos da UI. Uma futura versão que precise
  de changelog editável pelo Auditor é trabalho futuro, não coberto aqui.
- Nenhuma UI de "histórico de versões anteriores" — a popup mostra só a versão atual pendente.
- Jogadores não têm equivalente algum desta feature.

## Parte 1 — Versão atual (constante)

Novo tipo puro, `RuinaRPG.Domain.AppVersionInfo`:

```csharp
namespace RuinaRPG.Domain;

/// <summary>Single source of truth for the app's current version — see ChangelogController.</summary>
public static class AppVersionInfo
{
    public const string Current = "1.2.0";
}
```

## Parte 2 — Persistência por usuário

`ApplicationUser` (`src/RuinaRPG.Infrastructure/Identity/ApplicationUser.cs`) ganha um novo campo,
no mesmo padrão de `IsRulesAuditor`:

```csharp
/// <summary>
/// The last app version (AppVersionInfo.Current) this user has seen and dismissed the changelog
/// popup for — null means never dismissed any. Compared fresh on every Me() call, same pattern as
/// IsRulesAuditor above.
/// </summary>
public string? LastSeenAppVersion { get; set; }
```

Nova migração EF Core (`AddLastSeenAppVersion` ou nome similar) adicionando essa coluna
(`nullable text`) à tabela do Identity (`AspNetUsers`).

## Parte 3 — API

`MeResponse` (`src/RuinaRPG.Contracts/Auth/MeResponse.cs`) ganha um campo:

```csharp
public record MeResponse(string Id, string Nickname, string Role, bool IsRulesAuditor, string? PendingChangelogVersion);
```

`PendingChangelogVersion` é `AppVersionInfo.Current` quando `Role == "GM"` **e**
`LastSeenAppVersion != AppVersionInfo.Current`; caso contrário `null` (Jogador, ou GM que já viu
essa versão). `AuthController.Me()` computa isso lendo `LastSeenAppVersion` do banco (mesma query
que já busca `IsRulesAuditor` — um único `SingleOrDefaultAsync` pode trazer os dois campos juntos).

Novo endpoint:

```
POST api/auth/dismiss-changelog
```

Autenticado (qualquer usuário logado — não precisa checar Role aqui, já que só um GM com
`PendingChangelogVersion` não-nulo jamais chama isso na prática, e chamar sem necessidade não tem
efeito colateral nocivo). Seta `LastSeenAppVersion = AppVersionInfo.Current` para o usuário
autenticado e salva. Retorna `204 No Content`.

## Parte 4 — Cliente

Novo componente compartilhado `src/RuinaRPG.Client/Shared/ChangelogDialog.razor`:

```csharp
[Parameter, EditorRequired] public string Version { get; set; } = "";
[Parameter] public EventCallback OnDismissed { get; set; }
```

Renderiza um `<MudDialog>` (padrão inline já usado no app — `@bind-Visible`, sem
`IDialogService`) sempre visível quando montado (o componente só é montado quando o chamador
decide mostrá-lo), com o texto do changelog da versão 1.2.0 hardcoded (ver Parte 5) e um botão
"Fechar" que invoca `OnDismissed`.

`Painel.razor`, na já existente checagem de `_me.Role == "GM"`, adiciona:

```razor
@if (_me.PendingChangelogVersion is not null)
{
    <ChangelogDialog Version="@_me.PendingChangelogVersion" OnDismissed="DismissChangelogAsync" />
}
```

```csharp
private async Task DismissChangelogAsync()
{
    await Http.PostAsync("auth/dismiss-changelog", null);
    _me = _me! with { PendingChangelogVersion = null };
}
```

## Parte 5 — Conteúdo do changelog 1.2.0 (aprovado pelo usuário)

Texto exato a hardcodar no `ChangelogDialog.razor` (título "Versão 1.2.0" + lista de tópicos):

- Efeitos de Magias/Habilidades: catálogo com cálculo automático de Custo em PI; ao comprar um
  Efeito sem o pré-requisito, ele é adicionado automaticamente em vez de bloquear; resumo dos
  Efeitos ("Dano: 3d6", etc.) agora aparece em toda lista de Magia/Habilidade.
- Afinidades elementais: Elemento/Sub-Elemento agora restritos pela Vocação (Escola de Magia);
  Eficiência e Dano Elemental calculados automaticamente.
- Cadastro de item direto na ficha: crie um item novo no catálogo sem sair da Ficha de
  NPC/Criatura.
- Características exclusivas de Criatura: novo catálogo só pra Criaturas.
- Fichas de NPC e Criatura reorganizadas para seguir a mesma ordem de abas/seções da Ficha de
  Personagem.
- Aviso de "Novo Nível" e contador de Pontos de Perícia gastos agora também em NPC/Criatura.
- Várias correções: mensagens de erro/nível mais legíveis, cor do título no tema claro, sessão
  expirada redireciona pro login, busca por nome e subcategoria no catálogo de itens, bug em que
  subir de nível não atualizava os limites de atributos/características.

## Testes

- `AuthControllerTests` (Integration): `Me` retorna `PendingChangelogVersion` igual a
  `AppVersionInfo.Current` para um GM recém-registrado (nunca viu nenhuma versão); retorna `null`
  para um Jogador mesmo sem nunca ter visto nada; `dismiss-changelog` seguido de `Me` faz
  `PendingChangelogVersion` virar `null`; um segundo GM (nunca chamou dismiss) continua recebendo
  a versão pendente — o dismiss é por usuário, não global.
- Sem teste de UI dedicado para `ChangelogDialog.razor`/`Painel.razor` (mesmo padrão já
  estabelecido nas 3 Fichas — sem bUnit de página, verificado por build + leitura cuidadosa do
  diff).

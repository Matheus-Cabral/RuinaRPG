# Notificação de Nota Secreta — design

Data: 2026-10-09. Branch: `release/1.4.4` (entra na própria versão 1.4.4, PR #64).

## Objetivo

Quando o GM publica uma Nota Secreta (Campanha R0011), cada jogador destinatário é avisado na hora,
em qualquer página do app: um aviso visual clicável, que leva à aba Notas Secretas da campanha, e o
som `harp_notification.mp3`. Além do aviso em tempo real, o jogador vê um contador persistente de
notas não lidas, que cobre o caso de ele não estar com o app aberto no momento do envio.

Decisões confirmadas com o usuário:

- Aviso **visual clicável** + **sonoro**, em **qualquer página**.
- **Tempo real + contador** persistente de não lidas (não há replay do som ao entrar depois).
- Uma nota deixa de ser não lida quando o jogador **abre a aba Notas Secretas** daquela campanha:
  todas as notas dele naquela campanha passam a lidas de uma vez.
- Entra na versão **1.4.4**, somando ao changelog dela.

Os demais membros da campanha não recebem evento, contagem nem qualquer sinal de que a nota existe
(R0011 continua valendo).

## Dados

`DiaryEntryRecipients` ganha a coluna `ReadAt` (`timestamp with time zone`, nula). Nula significa
não lida. A entidade `DiaryEntryRecipient` (Domain) ganha `DateTime? ReadAt`.

Migration `AddReadAtToDiaryEntryRecipients`: adiciona a coluna e, no mesmo `Up`, preenche `ReadAt`
de todas as linhas já existentes com o `CreatedAt` da respectiva `DiaryEntry`. Assim nenhuma nota
anterior à 1.4.4 aparece como não lida. Como a stack local roda em Production, o deploy exige
`make migrate`.

## API

### Endpoints (role Jogador)

- `GET /api/secret-notes/unread` → `List<UnreadSecretNotesResponse>`, com
  `UnreadSecretNotesResponse(string CampaignId, string CampaignName, int Count)`. Só campanhas com
  `Count > 0`. Conta linhas de `DiaryEntryRecipients` do usuário com `ReadAt` nulo cuja `DiaryEntry`
  tem `IsSecretNote = true`.
- `POST /api/campaigns/{campaignId}/secret-notes/mark-read` → `204`. Preenche `ReadAt = UtcNow` nas
  linhas não lidas do usuário naquela campanha. Idempotente. Usuário que não é membro da campanha
  recebe a mesma resposta que `ListSecretNotes` já dá hoje nesse caso.

Ambos ficam em `CampaignPlayerViewController`, junto do `ListSecretNotes` existente.

### Hub

`NotificationHub` em `/hubs/notifications`, `[Authorize]` (qualquer usuário autenticado), sem
métodos chamáveis pelo cliente. O envio é por usuário: `hub.Clients.Users(ids)`.

O JWT guarda o id do usuário na claim `sub` (`Program.cs` mantém os tipos de claim como o
`JwtTokenService` escreveu), e o `IUserIdProvider` padrão do SignalR lê `ClaimTypes.NameIdentifier`.
Por isso é registrado um `SubClaimUserIdProvider : IUserIdProvider` que devolve a claim `sub`.

O token já chega por `?access_token=` para rotas `/hubs/` (`OnMessageReceived` em `Program.cs`) e o
nginx já faz proxy de `/hubs/`. Nenhuma mudança de infra.

### Eventos

| Evento | Payload | Quando | Para quem |
|---|---|---|---|
| `SecretNoteReceived` | `SecretNoteNotification(string CampaignId, string CampaignName)` | `CreateSecretNote`; `UpdateSecretNote` quando há destinatário acrescentado | Na criação, todos os destinatários. Na edição, só os acrescentados |
| `SecretNotesChanged` | nenhum | `DeleteSecretNote`; `UpdateSecretNote` quando há destinatário removido | Os destinatários que perderam a nota |

O texto da nota nunca trafega no evento. `SecretNoteNotification` fica em `RuinaRPG.Contracts`.

Em `UpdateSecretNote`, os destinatários que permanecem conservam o `ReadAt` que tinham; os
acrescentados entram com `ReadAt` nulo. Editar só o texto ou as imagens não notifica ninguém.

O envio acontece depois do `SaveChangesAsync`, dentro de `try/catch` com log: falha no hub não
desfaz nem falha a gravação da nota (mesmo padrão de `CharacterSheetsController` com o
`EncounterHub`).

## Client

### `SecretNoteNotifier` (Services, scoped)

Dono da conexão com `/hubs/notifications` e do estado de não lidas.

- `StartAsync()`: busca `secret-notes/unread`, abre a conexão (`WithAutomaticReconnect`,
  `AccessTokenProvider` do `AuthStateService`, como em `GerenciadorDeEncontros.razor`). Idempotente.
- `StopAsync()`: fecha a conexão e zera o estado.
- `IReadOnlyDictionary<string, int> UnreadByCampaign`, `int TotalUnread`.
- `event Action? Changed`: contadores mudaram.
- `event Action<SecretNoteNotification>? Received`: chegou uma nota em tempo real.
- `MarkCampaignReadAsync(campaignId)`: chama o `mark-read` e zera o contador daquela campanha.
- Ao receber `SecretNoteReceived`: incrementa o contador da campanha, dispara `Changed` e `Received`.
- Ao receber `SecretNotesChanged`, e ao reconectar: recarrega `secret-notes/unread` e dispara
  `Changed` (sem som, sem aviso).

### `MainLayout`

- Inicia o `SecretNoteNotifier` quando o usuário autenticado tem role Jogador e o encerra quando
  deixa de estar autenticado (assina `AuthenticationStateChanged`).
- Assina `Received`: toca o som e abre um snackbar do MudBlazor, "Nova nota secreta em
  *{campanha}*", com ícone (`Icons.Material.Filled.MarkEmailUnread`, sem emoji), clicável. O clique
  navega para `/campanhas/{id}/jogador?aba=notas`.

### Som

`Docs/harp_notification.mp3` é copiado para `src/RuinaRPG.Client/wwwroot/audio/harp_notification.mp3`.
`wwwroot/js/notificationSound.js` expõe uma função que toca o arquivo e engole a rejeição do
`play()` (política de autoplay do navegador). O script é referenciado em `index.html` como os demais.

### Contador

- `NavMenu`: `MudBadge` com `TotalUnread` no link "Minhas Campanhas" (oculto quando zero).
- `MinhasCampanhas.razor`: badge por campanha.
- `MinhaCampanha.razor`: badge no título da aba "Notas Secretas".

Os três assinam `Changed` e se desinscrevem no `Dispose`.

### `MinhaCampanha.razor`

- Lê o parâmetro de query `aba`; `aba=notas` abre direto a aba Notas Secretas.
- Quando a aba Notas Secretas fica ativa, chama `MarkCampaignReadAsync`.
- Com a aba ativa, um `Received` da mesma campanha recarrega a lista e marca como lida em seguida.

## Limitações aceitas

- O navegador pode bloquear o som enquanto o jogador não tiver interagido com a página. O aviso
  visual aparece de qualquer forma.
- Com o app aberto em mais de uma aba do navegador, o som toca em cada uma.
- Quem entra depois do envio vê só o contador; o som não é reproduzido retroativamente.

## Documentação

- `Requisitos - Campanha.md`: novo **R0015**, notificação e contador de Notas Secretas, referenciando
  R0011 e R0009.
- `Requisitos - Modelo de Dados.md`: coluna `ReadAt` em `DiaryEntryRecipients`.
- `Requisitos - Técnico.md`: `NotificationHub` ao lado do hub de encontros.
- `ChangelogDialog`: item novo na lista da 1.4.4 (`AppVersionInfo` já está em 1.4.4).

## Testes (TDD)

Integração (`RuinaRPG.Tests.Integration`):

- `unread` conta por campanha, só para o destinatário; não destinatário e outro jogador recebem
  lista vazia.
- `mark-read` zera a contagem daquela campanha, não afeta outra campanha nem outro jogador, e é
  idempotente.
- Hub: destinatário conectado recebe `SecretNoteReceived` com a campanha certa ao criar a nota;
  membro não destinatário conectado não recebe nada.
- Edição: destinatário acrescentado recebe `SecretNoteReceived` e passa a contar como não lida; quem
  já era destinatário mantém o estado de leitura e não é notificado; destinatário removido recebe
  `SecretNotesChanged`.
- Exclusão: destinatários recebem `SecretNotesChanged` e a contagem cai.
- Conexão anônima ao hub é recusada.

Client (`RuinaRPG.Tests.Client`):

- `SecretNoteNotifier`: carga inicial, incremento ao receber, zerar ao marcar como lida.
- `NavMenu` mostra o badge com o total e o esconde em zero.
- `MinhaCampanha` abre na aba Notas Secretas com `aba=notas` e marca como lida ao ativá-la.

A migration é verificada por um teste que confirma `ReadAt` preenchido para destinatários
pré-existentes, se o padrão de testes de migration do projeto permitir; caso contrário, por
verificação manual no `make migrate` do deploy, registrada no PR.

> Este documento é diferente dos demais em `Docs/Requisitos/`: em vez de descrever uma funcionalidade visível ao usuário, registra decisões técnicas (stack, arquitetura, segurança, infraestrutura) — requisitos não-funcionais do projeto. Não corresponde a nenhum bloco do "[[01 - Visão Geral.canvas]]".

  

# **R0001** - Stack tecnológica.

**Descrição**:

- **API**: ASP.NET Core 8 Web API.
- **Logging**: Serilog.
- **Documentação de API**: Swashbuckle/Swagger.
- **Autenticação**: ASP.NET Core Identity + JWT Bearer + refresh tokens.
- **Tempo real**: SignalR, com o Hub autenticado pelo mesmo JWT da API — usado pelo "[[Requisitos - Gerenciador de Encontros]]" para refletir em tempo real PV/PF/PA de participantes vindos de Ficha de Personagem. Há um segundo hub, `/hubs/notifications`, autenticado para qualquer usuário, só de servidor para cliente e endereçado por usuário (claim `sub` do JWT), usado para os avisos de Nota Secreta (ver "[[Requisitos - Campanha]]" R0015): os eventos `SecretNoteReceived` (apenas id e nome da campanha, nunca o texto da nota) e `SecretNotesChanged` (sem conteúdo; faz o cliente recontar as não lidas).
- **ORM**: Entity Framework Core 8 + Npgsql (PostgreSQL).
- **Frontend**: Blazor WebAssembly 8 standalone + Blazored.LocalStorage.
- **Containers**: Docker + nginx 1.27-alpine.
- **Testes**: xUnit, Moq, FluentAssertions, Bogus, Testcontainers.PostgreSql.
- **Armazenamento de imagem**: disco local, em um volume Docker — sem serviço de objeto externo (ex: MinIO/S3) por enquanto. O banco guarda apenas o caminho/referência do arquivo, nunca o binário. Suporta diretamente o modelo de "referenciar em vez de reenviar" já definido em "[[Requisitos - Catálogo de Itens e Equipamentos]]" R0010.

  

# **R0002** - Estrutura da solution em camadas separadas.

**Descrição**: A solution é dividida em projetos por responsabilidade:

- **RuinaRPG.Domain**: entidades e regras de domínio puras (sem dependência de EF Core, ASP.NET ou qualquer infraestrutura) — ex: as fórmulas de "[[Formulas]]", validações de limite (3 Artefatos por Tipo, 3 slots de armadura, etc.).
- **RuinaRPG.Infrastructure**: implementação de acesso a dados (EF Core, `DbContext`, migrations, repositórios), armazenamento de arquivo, e qualquer outra integração externa.
- **RuinaRPG.Contracts**: DTOs/contratos de request e response da API, compartilhados por referência de projeto entre a Api e o Client — evita duplicar definição de modelo em dois lugares.
- **RuinaRPG.Api**: controllers, o Hub de SignalR, configuração de autenticação/autorização, Swagger, composição dos demais projetos.
- **RuinaRPG.Client**: o Blazor WebAssembly standalone.
- **RuinaRPG.Tests**: testes com xUnit; pode ser subdividido em projetos de teste unitário e de integração (Testcontainers.PostgreSql) conforme o projeto crescer.

  

# **R0003** - Variáveis de ambiente.

**Descrição**: Configuração via variáveis de ambiente (arquivo `.env` em desenvolvimento), sem segredos hardcoded no código-fonte:

- `ConnectionStrings__Postgres`: string de conexão do PostgreSQL.
- `Jwt__SigningKey`: chave de assinatura dos tokens JWT.
- `Jwt__Issuer` / `Jwt__Audience`.
- `Jwt__AccessTokenMinutes`: validade do access token.
- `Jwt__RefreshTokenDays`: validade do refresh token.
- `img_max_size`: tamanho máximo de upload de imagem, em MB — já referenciado em "[[Requisitos - Ficha de Personagem]]" 1.a, vale para toda imagem do sistema (fichas, itens, diários).
- `Storage__ImagesPath`: caminho no volume onde as imagens ficam salvas.
- `Invite__CodeExpirationHours`: validade do código de convite, em horas. Default **48**, conforme "[[Requisitos - Convite de Jogador]]" R0001.
- `Cors__AllowedOrigins`: quais URLs/origens a Api aceita receber requisição — inclui os endpoints de autenticação (login, refresh, cadastro), não só o Client.
- `ASPNETCORE_ENVIRONMENT`: `Development` ou `Production`.

`Jwt__AccessTokenMinutes`/`Jwt__RefreshTokenDays` (validade dos tokens de sessão), `img_max_size` (tamanho máximo de upload) e `Cors__AllowedOrigins` (de onde a autenticação aceita requisição) são as três variáveis explicitamente exigidas pelo projeto — as demais desta lista existem para suportá-las ou para completar a configuração.

  

# **R0004** - Autenticação e autorização.

**Descrição**: O JWT carrega o Id do usuário e um claim de papel (**GM** ou **Jogador**). A autorização segue duas camadas:

- **Baseada em papel**: endpoints exclusivos de GM (ex: criar/editar itens do Catálogo, criar campanhas) exigem o claim de GM.
- **Baseada em dono do recurso**: para fichas com "jogador dono" (Ficha de Personagem, ou Ficha de NPC/Criatura concedida — ver "[[Requisitos - Campanha]]" R0010), edição exige que o usuário autenticado seja o dono da ficha **ou** o GM dono da campanha; leitura de conteúdo público segue as regras de visibilidade já definidas em cada documento de Requisitos (ex: "[[Requisitos - Campanha]]" R0008/R0009, "[[Requisitos - Ficha de NPCs]]" R0004).

  

# **R0005** - Validação de upload de imagem.

**Descrição**: Todo upload de imagem (Imagem do Personagem, Imagem de item, imagens de diário) é validado no backend, independente do que o cliente já filtra:

- Formato: WebP, JPEG, JPG, PNG ou GIF, verificado pelos bytes do arquivo (magic bytes), não só pela extensão do nome.
- Tamanho: não pode exceder `img_max_size` (R0003).

  

# **R0006** - Rate limiting em endpoints sensíveis.

**Descrição**: Usando o rate limiting nativo do ASP.NET Core 8 (`System.Threading.RateLimiting`), os seguintes endpoints têm limite de requisições por IP/usuário: login, renovação de refresh token, e resgate de código de convite (ver "[[Requisitos - Convite de Jogador]]" R0001) — este último por ser uma string curta (8 caracteres) sujeita a força bruta.

  

# **R0007** - O deploy roda inteiramente em containers, sem dependências manuais no host.

**Descrição**: Um sistema recém-instalado só precisa ter **git**, **docker** (com o plugin Compose) e **make** para rodar o deploy completo — nada de SDK do .NET, Node, PostgreSQL etc. instalado diretamente no host. As imagens da api, do cliente e do smoke saem de um único `deploy/Dockerfile` de vários estágios, que instala tudo que precisa (runtime do .NET, dependências do build, etc.) e compila a solução uma vez por deploy; nenhuma etapa de instalação manual é necessária além dessas três ferramentas.

Um `docker-compose.yml` define os serviços:

- **api-blue** e **api-green**: duas "cores" da aplicação ASP.NET Core; só uma serve por vez e a outra é a destinada ao próximo deploy (R0014).
- **db**: PostgreSQL, com volume nomeado para persistência dos dados.
- **web** (nginx de longa duração): serve os arquivos estáticos do Blazor WebAssembly a partir de releases versionados, atua como reverse proxy para a cor ativa da api, e serve as imagens do volume de armazenamento (R0001).

O deploy normal é o pipeline (R0013–R0015). O build das imagens fica em `deploy/Dockerfile` e a configuração do nginx em `deploy/nginx/`.

O nginx entrega o cliente comprimido: os arquivos de `_framework` saem do `.gz` gerado pelo publish (`gzip_static`) e o restante do texto (CSS/JS fora de `_framework` e o JSON da api) é comprimido na hora, exceto `/api/auth/` (tokens) e `/hubs/`. Toda resposta do cliente e das imagens leva `Cache-Control` explícito: `no-cache` (revalidação por ETag) para tudo cujo nome não muda entre versões — inclusive `index.html` e `blazor.boot.json` —, um ano `immutable` só para os `dotnet.*.<hash>.js` de `_framework`, e `private`, um ano, `immutable` para `/images/` (cada upload ganha um nome novo). Assim nenhum navegador nem a borda do Cloudflare serve um arquivo antigo depois de um deploy.

Volumes nomeados separados para os dados do Postgres e para as imagens armazenadas, para facilitar backup independente de cada um.

  

# **R0008** - Utilitários de deploy via Makefile.

**Descrição**: Um `Makefile` na raiz do projeto expõe os comandos operacionais mais comuns como alvos de `make`, entre eles:

- `make deploy` (ou `make up`): caminho manual de emergência — chama `deploy/deploy.sh deploy <sha do HEAD>` (R0014). O deploy normal é o pipeline (R0013–R0015).
- `make down`: para os containers.
- `make clean`: remove containers, imagens e volumes não utilizados (sem apagar os volumes nomeados de dados/imagens, salvo um alvo separado e explícito para isso).
- `make migrate`: chama `deploy/deploy.sh migrate`, que aplica migrations e seeders pendentes com a imagem da cor ativa (ver "Aplicação de migrations do EF Core" abaixo). É o caminho manual; no deploy normal o `deploy.sh` já migra.
- `make status`: mostra a cor ativa, as releases e os backups mais recentes (`deploy.sh status`).
- `make rollback`: volta para a release anterior (`deploy.sh rollback`, R0015).
- `make grant-rules-auditor EMAIL=...` / `make revoke-rules-auditor EMAIL=...` (executados na cor ativa via `deploy.sh exec-api`): designa (ou revoga) uma conta de GM, localizada pelo e-mail (case-insensitive), como Auditor de Regras. Tem efeito imediato — checado a cada requisição direto no banco, sem precisar relogar. Conceder é recusado se a conta indicada não for de GM; revogar é sempre aceito.
- `make reset-gm-password EMAIL=...`: recuperação de senha de GM (ver "[[Requisitos - Login e Cadastro]]" R0005) — localiza a conta de GM pelo e-mail, gera uma senha temporária e imprime o Nickname e a senha no console. Também revoga todos os refresh tokens ativos daquela conta e marca a conta para exigir troca de senha no próximo login.
- `make logs`: acompanha os logs dos containers.

  

# **R0009** - Aplicação de migrations do EF Core.

**Descrição**: Em ambiente de **desenvolvimento**, migrations pendentes são aplicadas automaticamente na inicialização da Api. Em ambiente de **produção**, a aplicação de migrations nunca é automática na inicialização: o `deploy/deploy.sh` a executa como passo explícito do deploy, num container avulso com a **imagem nova** (`--migrate`), **antes** da troca de cor — para evitar uma migration destrutiva rodar sem revisão prévia (o pipeline a valida contra um clone da produção, R0013). `make migrate` (R0008) continua existindo como caminho manual.

Como a cor antiga segue atendendo enquanto a migration roda, toda migration deve ser retrocompatível com a release anterior; quando não for, o PR recebe o rótulo `deploy:janela` e o deploy ocorre em janela de manutenção (R0014).

**Versão 1.5.0**: esta versão traz migrations que o código usa já no primeiro request: todo salvamento de ficha grava o log de ficha ([[Requisitos - Campanha]] R0016, R0012 abaixo) e as tabelas de encontros e de Condições são novas. Por isso o `deploy.sh` migra antes de colocar a cor nova na frente do nginx; no caminho manual antigo (`make deploy` seguido de `make migrate`) salvar uma ficha falhava enquanto a migration não rodasse. A 1.5.0 não tem `deploy/deploy.sh`: o primeiro release do pipeline é validado em modo de bootstrap e roda em janela (R0013).

**Versão 2.0.0**: as 84 migrations da 1.5.0 foram consolidadas numa única migration de baseline (`20261017120000_Baseline_2_0_0`), que cria o mesmo schema e as mesmas Perícias base. O `--migrate` reconhece um banco que está exatamente na 1.5.0 e troca o histórico de migrations dele pela baseline, sem tocar em schema nem dados; qualquer outro estado antigo (parte da cadeia, migrations desconhecidas) é recusado sem alterar nada, e o deploy aborta. Migrations novas entram por cima da baseline, como antes.

  

# **R0010** - Hospedagem: VM Ubuntu Server num homelab Proxmox, atrás de um túnel Cloudflare.

**Descrição**: A aplicação roda numa VM Ubuntu Server dentro de um homelab que usa Proxmox. O acesso externo (fora da rede local) passa por um `cloudflared` (Cloudflare Tunnel) rodando num container LXC separado do Proxmox, que tuneleia até a aplicação — não há exposição direta de porta/IP público nem redirecionamento de porta no roteador.

Implicações para o deploy:

- O deploy é disparado pelo runner na VM de desenvolvimento (`192.168.80.16`) e chega à produção (`192.168.80.10`) por SSH, apenas através do porteiro (R0016).
- O **nginx** (R0007) não precisa gerenciar certificado TLS próprio nem terminar HTTPS para o domínio público — a Cloudflare faz a terminação TLS na borda do túnel. O nginx atende em HTTP simples na rede interna do homelab.
- `Cors__AllowedOrigins` (R0003) deve incluir o domínio público servido através do túnel Cloudflare, além de qualquer origem de desenvolvimento local.

  

# **R0011** - Desenvolvimento segue TDD.

**Descrição**: Toda unidade de comportamento (endpoint, serviço, regra de domínio) é implementada escrevendo primeiro um teste que falha, depois o código mínimo pra fazê-lo passar — não só onde for conveniente. Vale para todo o código de produção do projeto; os planos de implementação em `docs/superpowers/plans/` (fora deste `Docs/`, é a pasta usada pela skill de planejamento) já seguem esse ciclo passo a passo por padrão.

  

# **R0012** - O log de ficha é gravado por um único interceptor de salvamento.

**Descrição**: As linhas do log de alterações de ficha (tabela `SheetLogEntries`, [[Requisitos - Modelo de Dados]]; a tela do GM está em [[Requisitos - Campanha]] R0016) são gravadas por um único interceptor de salvamento do EF Core (`SheetLogInterceptor`, em `RuinaRPG.Infrastructure`), registrado no `RuinaRpgDbContext`. A cada salvamento ele lê do rastreador de alterações o que mudou nas tabelas de ficha e acrescenta as linhas do log ao **mesmo** salvamento: alteração e log são gravados juntos ou falham juntos, e nenhum controller de ficha grava o log por conta própria.

- O autor da linha é o usuário autenticado da requisição. Um salvamento sem usuário autenticado (seeders, inicialização da Api, utilitários de linha de comando) não gera log.
- **Escritas automáticas.** Dentro da ação de um usuário o servidor também escreve por conta própria: quando as Condições ativas de um participante mudam ([[Requisitos - Gerenciador de Encontros]]), o Atual da ficha acima do novo total é cortado até ele; os ajustes por vez e a expiração de Condições são do mesmo tipo. Essas escritas são marcadas uma a uma (serviço `SheetLogDoServidor`, com o mesmo escopo da requisição) e a linha que o interceptor monta delas sai com `Automatico = true` — o autor continua sendo o usuário da requisição, e o popup do log mostra o selo "automático" ao lado dele. A marca é por escrita, e não por salvamento, porque a ação do GM e o corte que ela provoca vão no mesmo salvamento. Uma escrita automática de Vitalidade, Foco ou Adrenalina não gera a linha `RecursoDaFicha` no log do encontro.
- **Linhas explícitas.** Aplicar, remover ou expirar uma Condição num participante ligado a uma ficha registra uma linha "Condição X aplicada/removida/expirada" (seção "Condições") que não vem de coluna nenhuma da ficha. Ela também passa pelo interceptor (mesmo instante, autor e recorte das demais): aplicar e remover são do GM; a expiração é automática.
- Fichas de NPC e de Criatura só entram no log enquanto estão concedidas a um jogador.
- O mesmo interceptor também grava uma linha no log de cada encontro em andamento de que a ficha participa quando a Vitalidade, o Foco ou a Adrenalina atual dela muda (ver "[[Requisitos - Gerenciador de Encontros]]").
- O que entra no log, e com qual rótulo, é definido numa tabela única no código (`SheetLogMap`): cada tabela de ficha e cada coluna dela tem um rótulo, está marcada como texto longo (registrado sem o conteúdo) ou está na lista de ignoradas. **Uma tabela ou coluna de ficha nova precisa ser acrescentada a essa tabela** — um teste automatizado falha enquanto ela não estiver classificada.
- Operações em massa que não passam pelo rastreador do EF Core (`ExecuteUpdate`, `ExecuteDelete`, SQL direto) **não** disparam o interceptor e, portanto, não geram log. O único uso atual sobre tabelas de ficha é o do Auditor de Regras ao remover uma Perícia, que zera o Gasto dela em todas as fichas ([[Requisitos - Auditoria de Regras]]): é manutenção do catálogo, não edição de ficha, e fica fora do log de propósito. Uma nova operação em massa sobre tabelas de ficha deve decidir explicitamente se precisa registrar log.
- Uma coluna zerada pelo próprio banco quando a linha que ela referencia é apagada (`ON DELETE SET NULL`: a imagem, o Histórico ou a Equipagem de uma ficha) também não passa pelo interceptor e não é registrada, como a operação em massa acima.

  

# **R0013** - Releases validados contra um clone da produção.

**Descrição**: Todo PR para `main` (somente de branch do próprio repositório e do dono) passa pelo workflow `release.yml`, que só libera o merge depois de:

- **CI**: `dotnet build -warnaserror`, os testes unitários, de cliente e de integração, os testes do pipeline (`python3 -m unittest deploy/tests/test_compare.py` e as suítes bats `gate.bats` e `fingerprint.bats`). As suítes bats que sobem stacks Docker (`deploy.bats`, `rollback.bats`, `clone.bats`) e a `provision.bats` rodam localmente (`deploy/tests/run-bats.sh`) e nos ensaios, não no CI.
- **Clone com dados reais**: `deploy/clone.sh` obtém um dump da produção pelo porteiro (R0016), restaura-o num projeto Compose descartável e faz nele o deploy da `main` (base) e, em seguida, o do PR (head) com o `deploy.sh` do próprio PR. Cada dump também fica guardado fora da máquina de produção (`/var/backups/ruinarpg-offsite`, os 5 mais recentes).
- **Impressão digital dos dados** antes e depois do deploy do head: perda ou alteração de dados fora do que está declarado em `deploy/data-changes.yml` reprova o release.
- **Smoke somente-leitura** (`tools/RuinaRPG.Smoke`) contra a cor nova.
- **Compatibilidade da versão anterior**: a API da base precisa continuar rodando sobre o schema já migrado. Se não rodar, o resultado é `janela` e o PR ganha o rótulo `deploy:janela` (R0014).

Reprovação: o PR recebe um comentário com o diagnóstico (a falha de cada etapa e o relatório do clone) e volta a rascunho. Aprovação: o PR sai de rascunho e o auto-merge é habilitado. O clone é sempre desmontado ao fim, mesmo se o job for cancelado. Ensaio sem PR: `workflow_dispatch` do `release.yml` (informando o sha) roda a validação sem comentar nem fazer merge.

Quando a base não tem `deploy/deploy.sh` (a 1.5.0), o clone entra em modo de **bootstrap**: parte do head, tira a impressão digital A do próprio dump restaurado, pula o deploy da base e a verificação de compatibilidade, e força `janela`.

  

# **R0014** - Deploy blue-green sem downtime.

**Descrição**: Após o merge, `deploy.yml` (um deploy por vez, ambiente `production`) pede à produção `deploy <sha da main>` (R0016), que executa `deploy/deploy.sh deploy <sha>`:

- Existem duas cores da API (`api-blue`, `api-green`). O deploy builda as imagens do sha, roda as migrations e seeders com a imagem nova, sobe a cor ociosa, espera o health check e só então troca o upstream do **nginx** (de longa duração, apenas recarregado — nunca recriado) para ela. A cor antiga é drenada por 30 s e parada.
- O cliente Blazor é publicado em releases versionados (`webroot/releases/<sha>`) e o `webroot/current` é trocado por symlink atômico; mantêm-se as 3 últimas releases e imagens (mais a atual e a anterior).
- **Migration incompatível** (rótulo `deploy:janela`, ou `deploy <sha> --window`): abre-se uma **janela de manutenção** — o nginx passa a responder **503 com o cabeçalho `X-Ruina-Manutencao`**, a cor antiga é parada, faz-se o backup da janela, migra-se e troca-se a cor.
- **Cliente**: o `RetryHandler` retenta, sem mostrar mensagem, qualquer requisição que receba o 503 de manutenção (e GET/HEAD em outros 502/503 ou falhas de rede), por até 90 s. Conexões SignalR reconectam por até **5 minutos** (`LongReconnectPolicy`). O `NewVersionWatcher` compara o sha do cliente com o de `GET /api/version` (a cada 5 minutos, ao reconectar e ao focar a aba) e exibe o aviso "Nova versão disponível" (`NewVersionNotice`, com ícone do MudBlazor).

  

# **R0015** - Backups e rollback.

**Descrição**: 

- **Backup antes de cada deploy**: `deploy.sh` grava `pg_dump` e o volume de imagens em `/var/backups/ruinarpg/<timestamp>-<sha7>` (diretório `root` `0700`, os dumps têm o hash das senhas). Mantêm-se **5** na produção — o backup da janela e o do último deploy nunca contam nem são podados — e **5** cópias fora da máquina nesta VM (R0013).
- **Smoke pós-deploy** na própria produção, contra a cor nova, antes de a cor antiga ser parada. O smoke inteiro tem prazo (`RUINA_SMOKE_TIMEOUT`, 300 s; o próprio smoke para de enviar requisições aos 4 minutos e reporta os paths restantes): estourar o prazo é uma falha do smoke e dispara o rollback.
- **Execução independente do chamador**: `deploy`, `rollback` e `restore` não morrem se o canal SSH do pipeline cair no meio (uma queda nunca pula o rollback automático), e cada execução, com a saída do docker, fica em `deploy/state/deploy.log` na produção.
- **Rollback automático**: qualquer falha entre o início da troca e o smoke (migration, health check, nginx, smoke) devolve a release anterior. No modo **sem downtime** o rollback **nunca restaura dados** (só reaponta nginx e cliente e para a cor nova, preservando o que foi escrito). No modo **janela** restaura o backup tirado dentro da janela, sob manutenção.
- **Rollback sob demanda** (`deploy.sh rollback` / `make rollback`): volta um nível, para a release anterior. Num deploy em janela restaura o backup da janela e, antes, **salva uma cópia dos dados que serão descartados** (se a cópia falhar, nada é alterado).
- `deploy.sh restore <backup>` carrega um backup de volta de forma atômica (o banco numa transação, depois as imagens), sob manutenção, sem cópia de segurança prévia.
- Uma falha irrecuperável deixa o nginx em manutenção e é reportada; `deploy.sh status` mostra o estado e os backups.

  

# **R0016** - Porteiro de comando forçado.

**Descrição**: O pipeline só alcança a produção por SSH com chaves cujo `authorized_keys` impõe um comando forçado (`sudo /opt/ruinarpg/gate.sh`, `deploy/gate.sh`). O porteiro aceita somente `dump`, `status` e `deploy <sha de 40 hex que já está na origin/main> [--window]`, validados por uma gramática exata antes de qualquer execução; todo o resto é recusado. O `sudoers` autoriza apenas o porteiro (preservando só `SSH_ORIGINAL_COMMAND`).

Há **duas chaves**: a de **dump**, no disco do runner (onde roda código de PR), é forçada a `gate.sh dump-only` e nunca faz deploy; a de **deploy** existe somente como segredo do Environment `production` e vive num `ssh-agent` apenas durante o job de deploy. O token de merge fica fora do runner (o job `report` roda em máquina hospedada pelo GitHub). Riscos aceitos e o passo a passo de provisionamento estão em `deploy/provision/README.md`.

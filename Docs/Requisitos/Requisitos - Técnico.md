> Este documento é diferente dos demais em `Docs/Requisitos/`: em vez de descrever uma funcionalidade visível ao usuário, registra decisões técnicas (stack, arquitetura, segurança, infraestrutura) — requisitos não-funcionais do projeto. Não corresponde a nenhum bloco do "[[01 - Visão Geral.canvas]]".

  

# **R0001** - Stack tecnológica.

**Descrição**:

- **API**: ASP.NET Core 8 Web API.
- **Logging**: Serilog.
- **Documentação de API**: Swashbuckle/Swagger.
- **Autenticação**: ASP.NET Core Identity + JWT Bearer + refresh tokens.
- **Tempo real**: SignalR, com o Hub autenticado pelo mesmo JWT da API — usado pelo "[[Requisitos - Gerenciador de Encontros]]" para refletir em tempo real PV/PF/PA de participantes vindos de Ficha de Personagem.
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

**Descrição**: Um sistema recém-instalado só precisa ter **git**, **docker** (com o plugin Compose) e **make** para rodar o deploy completo — nada de SDK do .NET, Node, PostgreSQL etc. instalado diretamente no host. Cada serviço builda sua própria imagem a partir de um `Dockerfile` que instala tudo que precisa (runtime do .NET, dependências do build, etc.); nenhuma etapa de instalação manual é necessária além dessas três ferramentas.

Um `docker-compose.yml` define os serviços:

- **api**: a aplicação ASP.NET Core.
- **db**: PostgreSQL, com volume nomeado para persistência dos dados.
- **nginx**: serve os arquivos estáticos do Blazor WebAssembly, atua como reverse proxy para a **api**, e serve as imagens do volume de armazenamento (R0001).

Volumes nomeados separados para os dados do Postgres e para as imagens armazenadas, para facilitar backup independente de cada um.

  

# **R0008** - Utilitários de deploy via Makefile.

**Descrição**: Um `Makefile` na raiz do projeto expõe os comandos operacionais mais comuns como alvos de `make`, entre eles:

- `make deploy` (ou `make up`): builda as imagens e sobe todos os containers.
- `make down`: para os containers.
- `make clean`: remove containers, imagens e volumes não utilizados (sem apagar os volumes nomeados de dados/imagens, salvo um alvo separado e explícito para isso).
- `make migrate`: aplica migrations pendentes em produção (ver "Aplicação de migrations do EF Core" abaixo).
- `make logs`: acompanha os logs dos containers.

  

# **R0009** - Aplicação de migrations do EF Core.

**Descrição**: Em ambiente de **desenvolvimento**, migrations pendentes são aplicadas automaticamente na inicialização da Api. Em ambiente de **produção**, a aplicação de migrations é um passo explícito do processo de deploy — `make migrate` (R0008), não automático na inicialização — para evitar uma migration destrutiva rodar sem revisão prévia.

  

# **R0010** - Hospedagem: VM Ubuntu Server num homelab Proxmox, atrás de um túnel Cloudflare.

**Descrição**: A aplicação roda numa VM Ubuntu Server dentro de um homelab que usa Proxmox. O acesso externo (fora da rede local) passa por um `cloudflared` (Cloudflare Tunnel) rodando num container LXC separado do Proxmox, que tuneleia até a aplicação — não há exposição direta de porta/IP público nem redirecionamento de porta no roteador.

Implicações para o deploy:

- O **nginx** (R0007) não precisa gerenciar certificado TLS próprio nem terminar HTTPS para o domínio público — a Cloudflare faz a terminação TLS na borda do túnel. O nginx atende em HTTP simples na rede interna do homelab.
- `Cors__AllowedOrigins` (R0003) deve incluir o domínio público servido através do túnel Cloudflare, além de qualquer origem de desenvolvimento local.

  

# **R0011** - Desenvolvimento segue TDD.

**Descrição**: Toda unidade de comportamento (endpoint, serviço, regra de domínio) é implementada escrevendo primeiro um teste que falha, depois o código mínimo pra fazê-lo passar — não só onde for conveniente. Vale para todo o código de produção do projeto; os planos de implementação em `docs/superpowers/plans/` (fora deste `Docs/`, é a pasta usada pela skill de planejamento) já seguem esse ciclo passo a passo por padrão.

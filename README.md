# UsersAPI

Microsserviço de gerenciamento de usuários desenvolvido como parte do **Tech Challenge – Fase 2** da pós-graduação FIAP, projeto **FCG Games (FIAPCloudGames2026)**.

Responsável pelo cadastro, autenticação e controle de acesso de usuários, publicando eventos de integração via RabbitMQ para comunicação com outros microsserviços da plataforma.

---

## Sumário

- [Contexto do Projeto](#contexto-do-projeto)
- [Arquitetura](#arquitetura)
- [Stack Tecnológica](#stack-tecnológica)
- [Endpoints](#endpoints)
- [Roles e Permissões](#roles-e-permissões)
- [Eventos de Integração](#eventos-de-integração)
- [Como Executar](#como-executar)
  - [Pré-requisitos](#pré-requisitos)
  - [Via Docker Compose (recomendado)](#via-docker-compose-recomendado)
  - [Via dotnet CLI (desenvolvimento local)](#via-dotnet-cli-desenvolvimento-local)
- [Configuração](#configuração)
- [Usuário Admin Padrão](#usuário-admin-padrão)

---

## Contexto do Projeto

A **UsersAPI** é um dos microsserviços que compõem a plataforma **FCG Games**. Sua responsabilidade é:

- Registrar novos usuários com atribuição de roles (`Admin`, `Manager`, `User`)
- Autenticar usuários e emitir tokens **JWT**
- Publicar o evento `UserCreatedEventV1` no **RabbitMQ** sempre que um novo usuário é criado, permitindo que outros microsserviços reajam ao evento de forma assíncrona

---

## Arquitetura

O projeto segue uma arquitetura em camadas (**Layered Architecture**), organizada em três projetos dentro da pasta `src/`:

```
UsersAPI/
├── src/
│   ├── UsersAPI.Domain/        # Entidades e contratos de domínio
│   │   ├── User.cs
│   │   └── RegisterRequest.cs
│   │
│   ├── UsersAPI.Infra/         # Infraestrutura: banco de dados e migrações
│   │   ├── AppDbContext.cs
│   │   └── Migrations/
│   │
│   └── UsersAPI.Web/           # Camada de apresentação: API, serviços e configuração
│       ├── Endpoints/
│       │   └── UserEndpoints.cs
│       ├── Services/
│       │   └── TokenService.cs
│       ├── Extensions/
│       │   └── MigrationExtensions.cs
│       ├── DTOs/
│       │   └── RegisterRequest.cs
│       └── Program.cs
│
├── Dockerfile
└── docker-compose.yml
```

### Responsabilidades por Camada

| Camada | Projeto | Responsabilidade |
|---|---|---|
| **Domain** | `UsersAPI.Domain` | Entidade `User` (extends `IdentityUser<Guid>`) e record `RegisterRequest` |
| **Infra** | `UsersAPI.Infra` | `AppDbContext` com ASP.NET Core Identity, migrações do EF Core para PostgreSQL |
| **Web** | `UsersAPI.Web` | Minimal API endpoints, geração de JWT, apply de migrações e seed, integração com RabbitMQ via MassTransit |

### Fluxo de Registro de Usuário

```
Client → POST /api/users/register
           │
           ├─► UserManager cria o usuário no PostgreSQL
           ├─► Atribui Role (padrão: "User")
           └─► Publica UserCreatedEventV1 → RabbitMQ → outros microsserviços
```

---

## Stack Tecnológica

| Tecnologia | Uso |
|---|---|
| **.NET 8** | Framework principal |
| **ASP.NET Core Minimal APIs** | Definição dos endpoints HTTP |
| **ASP.NET Core Identity** | Gerenciamento de usuários, senhas e roles |
| **Entity Framework Core 8** | ORM para acesso ao banco de dados |
| **PostgreSQL** | Banco de dados relacional |
| **Npgsql** | Provider do EF Core para PostgreSQL |
| **JWT (System.IdentityModel.Tokens.Jwt)** | Geração e validação de tokens de autenticação |
| **MassTransit + RabbitMQ** | Publicação de eventos de integração assíncronos |
| **Swagger / Swashbuckle** | Documentação interativa da API |
| **Docker / Docker Compose** | Containerização e orquestração local |

---

## Endpoints

Base URL: `http://localhost:5000/api/users`

### `POST /register` — Registrar usuário

**Body:**
```json
{
  "email": "usuario@exemplo.com",
  "password": "Senha@123",
  "fullName": "Nome Completo",
  "role": "User"
}
```

**Roles disponíveis:** `Admin`, `Manager`, `User` (padrão: `User`)

**Resposta de sucesso (201):**
```json
{
  "id": "guid",
  "email": "usuario@exemplo.com",
  "fullName": "Nome Completo",
  "message": "Usuário registrado com sucesso"
}
```

---

### `POST /login` — Autenticar usuário

**Body:**
```json
{
  "email": "usuario@exemplo.com",
  "password": "Senha@123"
}
```

**Resposta de sucesso (200):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "user": {
    "email": "usuario@exemplo.com",
    "fullName": "Nome Completo",
    "roles": ["User"]
  }
}
```

---

## Roles e Permissões

As roles são criadas automaticamente no seed da aplicação:

| Role | Descrição |
|---|---|
| `Admin` | Acesso administrativo completo |
| `Manager` | Acesso gerencial |
| `User` | Usuário padrão da plataforma |

---

## Eventos de Integração

Ao registrar um novo usuário, a API publica o evento `UserCreatedEventV1` no **RabbitMQ**:

| Campo | Tipo | Descrição |
|---|---|---|
| `EventId` | `Guid` | Identificador único do evento |
| `OccurredAt` | `DateTime` | Data e hora do evento |
| `UserId` | `Guid` | ID do usuário criado |
| `Email` | `string` | E-mail do usuário |
| `FullName` | `string` | Nome completo do usuário |

O contrato do evento é definido pelo pacote NuGet compartilhado `FIAPCloudGames2026.Contracts`.

---

## Como Executar

### Pré-requisitos

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) instalado e em execução
- _Ou_, para rodar localmente: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) e acesso a instâncias de PostgreSQL e RabbitMQ

---

### Via Docker Compose (recomendado)

1. Clone o repositório:
   ```bash
   git clone https://github.com/lucanunees/UsersAPI.git
   cd UsersAPI
   ```

2. Suba os containers:
   ```bash
   docker-compose up --build
   ```

   Isso irá subir:
   - **`users-api`** — a aplicação na porta `5000`
   - **`fcg-postgres2`** — PostgreSQL na porta `5432`

3. Acesse a documentação Swagger:
   ```
   http://localhost:5000/swagger
   ```

> As migrações do banco de dados são aplicadas automaticamente na inicialização da aplicação.

---

### Via dotnet CLI (desenvolvimento local)

1. Certifique-se de ter o PostgreSQL e RabbitMQ disponíveis localmente.

2. Ajuste a connection string e as configurações de RabbitMQ em `src/UsersAPI.Web/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=fcg_users_db;Username=fcg;Password=fcgpw"
     },
     "RabbitMq": {
       "Host": "localhost"
     }
   }
   ```

3. Restaure as dependências e execute:
   ```bash
   dotnet restore
   dotnet run --project src/UsersAPI.Web/UsersAPI.Web.csproj
   ```

4. Acesse a documentação Swagger:
   ```
   https://localhost:{porta}/swagger
   ```

---

## Configuração

Todas as configurações da aplicação estão em `src/UsersAPI.Web/appsettings.json`:

| Chave | Descrição |
|---|---|
| `ConnectionStrings:DefaultConnection` | String de conexão com o PostgreSQL |
| `Jwt:Issuer` | Emissor do token JWT |
| `Jwt:Audience` | Audiência do token JWT |
| `Jwt:Key` | Chave secreta para assinar os tokens (mínimo 32 caracteres) |
| `Jwt:ExpirationInMinutes` | Tempo de expiração do token em minutos |
| `RabbitMq:Host` | Host do RabbitMQ (padrão: `rabbitmq`) |

> ⚠️ **Atenção:** Em ambientes de produção, utilize variáveis de ambiente ou um gerenciador de segredos para as chaves sensíveis (`Jwt:Key`, credenciais do banco, etc.). Nunca versione segredos reais no repositório.

---

## Usuário Admin Padrão

Na primeira execução, a aplicação cria automaticamente um usuário administrador via seed:

| Campo | Valor |
|---|---|
| **E-mail** | `admin@techchallenge.com` |
| **Senha** | `##Password940@@` |
| **Role** | `Admin` |

> Recomenda-se alterar a senha do admin após o primeiro acesso em ambientes não locais.

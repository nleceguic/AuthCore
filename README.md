# AuthCore

[![CI](https://github.com/nleceguic/AuthCore/actions/workflows/ci.yml/badge.svg)](https://github.com/nleceguic/AuthCore/actions/workflows/ci.yml)

REST API de autenticación completa construida con ASP.NET Core 10 y Clean Architecture. Implementa registro, login y refresh de tokens JWT con rotación de refresh tokens, logout y un endpoint protegido de ejemplo.

## Stack

| Capa | Tecnología |
|------|-----------|
| API | ASP.NET Core 10, Swagger / Scalar |
| Autenticación | JWT Bearer (HS256), BCrypt.Net |
| Persistencia | EF Core 10, PostgreSQL 16 (Npgsql) |
| Tests | xUnit, Moq, WebApplicationFactory, EF Core InMemory |
| Infraestructura | Docker, docker-compose, GitHub Actions |

## Arquitectura

Clean Architecture en cuatro capas con dependencias siempre hacia el interior:

```
AuthCore/
├── src/
│   ├── AuthCore.Domain/            # Entidades (User, RefreshToken)
│   ├── AuthCore.Application/       # Interfaces, DTOs, AuthService
│   │   ├── DTOs/
│   │   ├── Interfaces/
│   │   └── Services/
│   ├── AuthCore.Infrastructure/    # EF Core, repositorios, TokenService
│   │   ├── Persistence/
│   │   │   ├── Migrations/
│   │   │   └── Repositories/
│   │   └── Services/
│   └── AuthCore.API/               # Controllers, middleware, Program.cs
│       ├── Controllers/
│       └── Middleware/
└── tests/
    └── AuthCore.Tests/
        ├── Unit/                   # AuthServiceTests (xUnit + Moq)
        └── Integration/            # AuthControllerTests (WebApplicationFactory)
```

**Dependencias entre proyectos:**
- `API` → `Application` + `Infrastructure`
- `Application` → `Domain`
- `Infrastructure` → `Application` + `Domain`
- `Tests` → todos

## Endpoints

### Autenticación

| Método | Ruta | Auth | Body | Respuesta |
|--------|------|------|------|-----------|
| `POST` | `/api/auth/register` | — | `{username, email, password}` | `201` `{accessToken, refreshToken, expiresAt}` |
| `POST` | `/api/auth/login` | — | `{email, password}` | `200` `{accessToken, refreshToken, expiresAt}` |
| `POST` | `/api/auth/refresh` | — | `{refreshToken}` | `200` `{accessToken, refreshToken, expiresAt}` |
| `POST` | `/api/auth/logout` | — | `{refreshToken}` | `204` |
| `GET`  | `/api/tasks` | Bearer | — | `200` `[{id, title, isCompleted, userId}]` |

**Códigos de error:**
- `400` — email/username ya registrado (`InvalidOperationException`)
- `401` — credenciales inválidas o token expirado/revocado (`UnauthorizedAccessException`)
- `500` — error interno

El refresh token se rota en cada uso: el token anterior queda revocado y se emite uno nuevo.

## Ejecutar con Docker Compose

```bash
git clone https://github.com/nleceguic/AuthCore.git
cd AuthCore
docker-compose up --build
```

La API queda disponible en `http://localhost:8080`.  
Swagger UI: `http://localhost:8080/swagger`

**Variables de entorno (docker-compose.yml):**
```
POSTGRES_DB=authcore
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
ConnectionStrings__DefaultConnection=Host=db;Port=5432;Database=authcore;Username=postgres;Password=postgres
JwtSettings__Secret=<mínimo 32 caracteres>
```

## Ejecutar en local

### Prerequisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL 16+](https://www.postgresql.org/download/)
- [dotnet-ef](https://learn.microsoft.com/en-us/ef/core/cli/dotnet): `dotnet tool install --global dotnet-ef`

### Pasos

1. **Clonar el repositorio**

```bash
git clone https://github.com/nleceguic/AuthCore.git
cd AuthCore
```

2. **Configurar la base de datos**

Crear la base de datos en PostgreSQL:

```sql
CREATE DATABASE authcore_dev;
```

3. **Actualizar `appsettings.Development.json`**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=authcore_dev;Username=postgres;Password=<tu_password>"
  },
  "JwtSettings": {
    "Secret": "dev-secret-key-not-for-production-must-be-at-least-32-chars",
    "Issuer": "AuthCore",
    "Audience": "AuthCoreClients",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

4. **Aplicar migraciones**

```bash
dotnet ef database update --project src/AuthCore.Infrastructure --startup-project src/AuthCore.API
```

5. **Arrancar la API**

```bash
dotnet run --project src/AuthCore.API
```

Swagger UI disponible en `https://localhost:<puerto>/swagger`.

## Tests

```bash
dotnet test AuthCore.sln
```

Los tests de integración usan EF Core InMemory — no requieren PostgreSQL.

| Suite | Tests | Tipo |
|-------|-------|------|
| `AuthServiceTests` | 9 | Unitarios (Moq) |
| `AuthControllerTests` | 8 | Integración (WebApplicationFactory) |

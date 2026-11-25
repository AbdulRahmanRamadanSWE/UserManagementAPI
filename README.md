# UserManagementAPI

A RESTful ASP.NET Core Web API for managing users, with token-based authentication, request/response logging, input validation, and centralized error handling.

![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4)
![EF Core](https://img.shields.io/badge/EF%20Core-SQLite-blue)
![License](https://img.shields.io/badge/license-MIT-green)

## Table of Contents

- [Features](#features)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Authentication](#authentication)
- [API Reference](#api-reference)
- [Validation Rules](#validation-rules)
- [Error Handling](#error-handling)
- [Middleware Pipeline](#middleware-pipeline)
- [Security Notes](#security-notes)
- [Roadmap](#roadmap)

## Features

- Full CRUD operations for users (`GET`, `POST`, `PUT`, `DELETE`)
- Paginated user listing for fast responses on large datasets
- Input validation with clear error messages
- Passwords hashed with ASP.NET Core `PasswordHasher` (never stored or returned in plain text)
- Unique email enforcement (case-insensitive) at both application and database level
- Bearer token authentication middleware
- Request and response logging for auditing (method, path, status code, duration)
- Consistent JSON error responses across all endpoints

## Tech Stack

| Component | Technology |
|---|---|
| Framework | ASP.NET Core Web API (.NET 8+) |
| ORM | Entity Framework Core |
| Database | SQLite |
| Auth | Custom bearer token middleware |
| API docs | OpenAPI |

## Project Structure

```
UserManagementAPI/
├── Controllers/
│   └── UsersController.cs        # CRUD endpoints
├── Data/
│   └── AppDbContext.cs           # EF Core context
├── DTOs/
│   └── UserDtos.cs               # Request/response models and validation
├── Middleware/
│   ├── ErrorHandlingMiddleware.cs
│   ├── TokenAuthenticationMiddleware.cs
│   └── RequestLoggingMiddleware.cs
├── Models/
│   └── User.cs                   # Database entity
├── Migrations/                   # EF Core migrations
├── appsettings.json
└── Program.cs
```

## Getting Started

### Prerequisites

- [.NET SDK 8.0](https://dotnet.microsoft.com/download) or later
- [EF Core CLI tools](https://learn.microsoft.com/ef/core/cli/dotnet)

```bash
dotnet tool install --global dotnet-ef
```

### Installation

```bash
# 1. Clone the repository
git clone https://github.com/YOUR_USERNAME/UserManagementAPI.git
cd UserManagementAPI

# 2. Restore dependencies
dotnet restore

# 3. Create the database
dotnet ef database update

# 4. Run the API
dotnet run
```

The console output shows the URL the API is listening on (for example `https://localhost:5001`).

## Configuration

Tokens are read from `appsettings.json`:

```json
{
  "Auth": {
    "Tokens": [ "dev-token-change-me" ]
  }
}
```

> **Important:** `dev-token-change-me` is for local development only. For real use, store tokens with User Secrets or environment variables and never commit them.

```bash
# User Secrets
dotnet user-secrets init
dotnet user-secrets set "Auth:Tokens:0" "your-real-token"

# Or an environment variable
export Auth__Tokens__0="your-real-token"
```

## Authentication

Every request (except OpenAPI documentation routes) must include a valid token in the `Authorization` header:

```
Authorization: Bearer <your-token>
```

Requests with a missing or invalid token receive:

```
HTTP/1.1 401 Unauthorized
{ "error": "Unauthorized." }
```

## API Reference

Base path: `/api/users`

| Method | Endpoint | Description | Success | Errors |
|---|---|---|---|---|
| `GET` | `/api/users?page=1&pageSize=20` | List users (paginated) | `200` | `400`, `401` |
| `GET` | `/api/users/{id}` | Get a user by ID | `200` | `401`, `404` |
| `POST` | `/api/users` | Create a user | `201` | `400`, `401`, `409` |
| `PUT` | `/api/users/{id}` | Update a user | `200` | `400`, `401`, `404`, `409` |
| `DELETE` | `/api/users/{id}` | Delete a user | `204` | `401`, `404` |

### Examples

**Create a user**

```bash
curl -X POST https://localhost:5001/api/users \
  -H "Authorization: Bearer dev-token-change-me" \
  -H "Content-Type: application/json" \
  -d '{"fullName":"Ahmad Ali","email":"ahmad@example.com","password":"Passw0rdOK"}'
```

Response `201 Created`:

```json
{
  "id": 1,
  "fullName": "Ahmad Ali",
  "email": "ahmad@example.com",
  "createdAt": "2026-11-25T10:30:00Z"
}
```

**List users**

```bash
curl "https://localhost:5001/api/users?page=1&pageSize=10" \
  -H "Authorization: Bearer dev-token-change-me"
```

Response `200 OK`:

```json
{
  "items": [
    { "id": 1, "fullName": "Ahmad Ali", "email": "ahmad@example.com", "createdAt": "2026-11-25T10:30:00Z" }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

**Update a user**

```bash
curl -X PUT https://localhost:5001/api/users/1 \
  -H "Authorization: Bearer dev-token-change-me" \
  -H "Content-Type: application/json" \
  -d '{"fullName":"Ahmad Updated","email":"ahmad@example.com"}'
```

**Delete a user**

```bash
curl -X DELETE https://localhost:5001/api/users/1 \
  -H "Authorization: Bearer dev-token-change-me"
```

## Validation Rules

| Field | Rules |
|---|---|
| `fullName` | Required, 2 to 100 characters |
| `email` | Required, valid email format, max 254 characters, unique (case-insensitive) |
| `password` | Required, 8 to 100 characters, must include an uppercase letter, a lowercase letter, and a digit |
| `page` | Integer, 1 or greater (default `1`) |
| `pageSize` | Integer, 1 to 100 (default `20`) |
| `id` (route) | Integer, 1 or greater |

## Error Handling

All errors share the same JSON shape:

```json
{ "error": "Description of the problem." }
```

| Status | Meaning |
|---|---|
| `400` | Validation failed |
| `401` | Missing or invalid token |
| `404` | User not found |
| `409` | Email already in use |
| `500` | Unexpected server error (details are logged, never exposed to the client) |

## Middleware Pipeline

Middleware runs in this order:

1. **ErrorHandlingMiddleware**: catches unhandled exceptions and returns a consistent 500 response.
2. **TokenAuthenticationMiddleware**: validates the bearer token and rejects invalid requests with 401.
3. **RequestLoggingMiddleware**: logs the HTTP method, path, status code, and duration of each authorized request.

Sample log output:

```
Incoming request: GET /api/users
Outgoing response: GET /api/users -> 200 (12 ms)
```

> Requests rejected with 401 are logged by the authentication middleware itself, since they never reach the logging middleware.

## Security Notes

- Passwords are hashed and never returned in any response.
- Request bodies are intentionally **not** logged, to avoid leaking passwords.
- Token comparison uses a constant-time check to prevent timing attacks.
- Always run behind HTTPS in production.
- Static tokens suit internal services and learning projects. For public-facing apps, use JWT with a login endpoint.

## Roadmap

- [ ] JWT authentication with a login endpoint
- [ ] Role-based authorization
- [ ] Service layer and unit tests
- [ ] Docker support
- [ ] CI pipeline with GitHub Actions

## License

This project is licensed under the MIT License.

# Optimizely CMS 12 — Local Development Setup

Local Optimizely CMS 12 sample project with SQL Server in Docker, pre-configured API access (Content Delivery + Content Management), and OpenID Connect authentication.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

## Quick start

```powershell
# 1. Restore NuGet packages
dotnet restore .\Cms12Local\Cms12Local.csproj --configfile .\Cms12Local\nuget.config

# 2. Start SQL Server in Docker
docker compose up -d

# 3. Create the database and SQL login
docker exec opti-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "Your_strong_SA_password_123!" -C -Q "IF DB_ID(N'Cms12Local') IS NULL CREATE DATABASE [Cms12Local];"

docker exec opti-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "Your_strong_SA_password_123!" -C -d Cms12Local -Q "IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'Cms12LocalUser') CREATE LOGIN [Cms12LocalUser] WITH PASSWORD = 'jP75ubr&m3wGdl4ZR7Y%%qi8p'; IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'Cms12LocalUser') CREATE USER [Cms12LocalUser] FOR LOGIN [Cms12LocalUser]; ALTER ROLE [db_owner] ADD MEMBER [Cms12LocalUser];"

# 4. Run the site (first run creates the database schema)
cd .\Cms12Local
dotnet run
```

The site starts at **https://localhost:5000**.

## Create the admin user

Stop the running app, then install the Optimizely CLI and create the admin account:

```powershell
dotnet tool install EPiServer.Net.Cli --global --add-source https://nuget.optimizely.com/feed/packages.svc/

dotnet-episerver add-admin-user .\Cms12Local\Cms12Local.csproj -u admin -p "Password123!" -e "admin@example.local" -c EPiServerDB
```

`EPiServerDB` is the connection string name from `appsettings.Development.json`. The password must satisfy the default ASP.NET Identity rules (uppercase, lowercase, digit, special character, minimum 6 characters).

Start the app again and sign in at **https://localhost:5000/util/login**.

## API authentication

Two OpenID Connect clients are pre-configured in `Startup.cs`. Choose the one that matches the Blackbird connection type you want to test.

### Resource Owner Password (username + password)

Use this when Blackbird is configured with the **Resource Owner Password** connection type.

```powershell
curl -k -X POST "https://localhost:5000/api/episerver/connect/token" -H "Content-Type: application/x-www-form-urlencoded" -d "grant_type=password&client_id=blackbird-local&client_secret=blackbird-local-secret&username=admin&password=Password123!&scope=epi_content_management roles"
```

| Setting | Value |
|---|---|
| Client ID | `blackbird-local` |
| Client Secret | `blackbird-local-secret` |
| Grant type | `password` |
| Scopes | `openid`, `offline_access`, `roles`, `epi_content_management` |

### Client Credentials (service account)

Use this when Blackbird is configured with the **Client Credentials** connection type. No CMS user account is required; the `BlackbirdLocalClientClaimsTransformation` injects editor roles automatically so the Content Management API accepts the token.

```powershell
curl -k -X POST "https://localhost:5000/api/episerver/connect/token" -H "Content-Type: application/x-www-form-urlencoded" -d "grant_type=client_credentials&client_id=blackbird-cc&client_secret=blackbird-cc-secret&scope=epi_content_management"
```

| Setting | Value |
|---|---|
| Client ID | `blackbird-cc` |
| Client Secret | `blackbird-cc-secret` |
| Grant type | `client_credentials` |
| Scopes | `epi_content_management` |

Both requests return a JSON response with an `access_token` (valid for 1 hour):

```json
{
  "access_token": "eyJhbG...",
  "token_type": "Bearer",
  "expires_in": 3599
}
```

## API usage

All examples below assume you have a valid `TOKEN` from the authentication step above.

### Content Delivery API (read-only)

Get the site definition (includes available languages):

```powershell
curl -k "https://localhost:5000/api/episerver/v3.0/site"
```

Get root children:

```powershell
curl -k "https://localhost:5000/api/episerver/v3.0/content/1/children" -H "Authorization: Bearer TOKEN"
```

Get a specific content item by ID:

```powershell
curl -k "https://localhost:5000/api/episerver/v3.0/content/5" -H "Authorization: Bearer TOKEN"
```

Get content in a specific language:

```powershell
curl -k "https://localhost:5000/api/episerver/v3.0/content/5" -H "Authorization: Bearer TOKEN" -H "Accept-Language: sv"
```

Get content by GUID:

```powershell
curl -k "https://localhost:5000/api/episerver/v3.0/content/7b08c7d5-7585-47e5-add7-5822da68c3ce" -H "Authorization: Bearer TOKEN"
```

Expand nested references:

```powershell
curl -k "https://localhost:5000/api/episerver/v3.0/content/5?$expand=*" -H "Authorization: Bearer TOKEN"
```

### Content Management API (read/write)

Get content (includes `previewUrl` and `editUrl`):

```powershell
curl -k "https://localhost:5000/api/episerver/v3.0/contentmanagement/5" -H "Authorization: Bearer TOKEN" -H "Accept-Language: en"
```

Update a content item (PATCH):

```powershell
curl -k -X PATCH "https://localhost:5000/api/episerver/v3.0/contentmanagement/5" -H "Authorization: Bearer TOKEN" -H "Content-Type: application/json" -H "X-EPiServer-Language: en" -d "{\"metaTitle\":{\"value\":\"Updated title\"}}"
```

Create or update a language branch (PATCH with language):

```powershell
curl -k -X PATCH "https://localhost:5000/api/episerver/v3.0/contentmanagement/5" -H "Authorization: Bearer TOKEN" -H "Content-Type: application/json" -H "X-EPiServer-Language: sv" -d "{\"name\":\"Start SV\",\"language\":{\"name\":\"sv\"},\"metaTitle\":{\"value\":\"Alloy - samarbete och projektledning online\"}}"
```

> **Important:** To update a specific language branch, you must include **both** the `X-EPiServer-Language` header **and** the `language` object in the request body. Without these, the update falls back to the master language.

Create new content (PUT):

```powershell
curl -k -X PUT "https://localhost:5000/api/episerver/v3.0/contentmanagement" -H "Authorization: Bearer TOKEN" -H "Content-Type: application/json" -d "{\"name\":\"My New Page\",\"language\":{\"name\":\"en\"},\"contentType\":[\"StandardPage\"],\"parentLink\":{\"id\":5},\"status\":\"Published\",\"mainBody\":{\"value\":\"<p>Hello world</p>\"}}"
```

Delete content (moves to Recycle Bin):

```powershell
curl -k -X DELETE "https://localhost:5000/api/episerver/v3.0/contentmanagement/CONTENT_ID" -H "Authorization: Bearer TOKEN"
```

Delete a specific language branch only:

```powershell
curl -k -X DELETE "https://localhost:5000/api/episerver/v3.0/contentmanagement/CONTENT_ID" -H "Authorization: Bearer TOKEN" -H "X-EPiServer-Language: sv"
```

### Working with blocks

Blocks referenced in content areas (e.g. `mainContentArea`) may not be accessible by numeric ID via the Content Management API. Use the block's GUID instead:

```powershell
curl -k -X PATCH "https://localhost:5000/api/episerver/v3.0/contentmanagement/3843b6e6-c614-415a-a4ff-ce39b32af13b" -H "Authorization: Bearer TOKEN" -H "Content-Type: application/json" -H "X-EPiServer-Language: en" -d "{\"heading\":{\"value\":\"Updated heading\"}}"
```

## Available languages

The Alloy demo site ships with English (`en`) and Swedish (`sv`) enabled. To add more languages, go to **CMS Admin** → **Config** → **Manage Website Languages** at:

**https://localhost:5000/EPiServer/Cms/Admin**

## Docker commands

| Command | Description |
|---|---|
| `docker compose up -d` | Start the SQL Server container |
| `docker compose down` | Stop the container (keeps data) |
| `docker compose down -v` | Stop and remove the database volume (clean reset) |

## Troubleshooting

**403 on Content Management API** — Make sure your token request includes `scope=epi_content_management roles`. Tokens without this scope are rejected.

**Login failed for user 'sa'** — On Windows, special characters like `!` in passwords can be mangled by PowerShell in double quotes. Use single quotes around passwords, or use a password without `!`.

**Empty response from `/content`** — The root endpoint returns top-level content. Use `/content/1/children` to browse the content tree, or fetch a specific ID like `/content/5`.

**404 on block IDs** — Some blocks are only accessible by GUID, not numeric ID. Check the `guidValue` from the parent's content area and use that instead.

**PATCH updates the wrong language** — Always include both the `X-EPiServer-Language` header and the `language` object in the request body when targeting a non-master language.
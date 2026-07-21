# Traccar-Csharp

[![.NET](https://github.com/ombasaMukhwami/traccar-csharp/actions/workflows/dotnet.yml/badge.svg??branch=master)](https://github.com/ombasaMukhwami/traccar-csharp/actions/workflows/dotnet.yml)

## Overview

A faithful C#/.NET 10 port of [Traccar](https://github.com/traccar/traccar) — an open-source GPS tracking server originally written in Java/Netty. This port preserves the core architecture and protocol decoding behaviour of the Java original while re-platforming onto ASP.NET Core, DotNetty, and EF Core.

The primary goal is a working server that real GPS devices can connect to, that stores positions via EF Core, exposes a REST API compatible with the Traccar web client, and can forward position data to external message brokers.

---

## Technology Stack

| Layer | Java (original) | C# (this port) |
|---|---|---|
| Network I/O | Netty | DotNetty |
| Dependency Injection | Guice | Microsoft.Extensions.DependencyInjection |
| REST API | Jersey / JAX-RS | ASP.NET Core MVC Controllers |
| Database ORM | Custom SQL + Liquibase | EF Core + PostgreSQL (Npgsql) |
| Authentication | Servlet session / cookie | ASP.NET Core Cookie Authentication |
| Configuration | `traccar.xml` / `Keys.java` | `appsettings.json` / `ConfigKeys.cs` |
| Protocol options | `IConfiguration` raw reads | `IOptionsMonitor<ProtocolOptions>` (named, per-protocol) |

---

## Supported Device Protocols

Each protocol runs on a configurable TCP (and optionally UDP) port. Enable a protocol by setting its port in `appsettings.json`:

| Protocol | Default Port | Notes |
|---|---|---|
| H02 | 5013 | TCP + UDP |
| GT06 | 5023 | TCP |
| Teltonika | 5027 | TCP + UDP; Codec 8, 8E, 12, 13, 16; BLE beacon/tag |
| GoSafe | 5165 | TCP + UDP |
| GL200 | 5093 | TCP + UDP |
| KHD | 5021 | TCP |
| JT808 | 5083 | TCP |
| JT1078 | 5076 | TCP (video streaming) |
| Meitrack | 5012 | TCP + UDP |
| Meiligao | 7700 | TCP + UDP |
| Niot | 5048 | TCP |
| Starcom | 5501 | TCP |

---

## REST API Endpoints

All endpoints (except `GET /api/server`, `POST /api/session`, and `POST /api/password/*`) require an authenticated session cookie.

### Session
| Method | Path | Description |
|---|---|---|
| `POST` | `/api/session` | Login (form: `email`, `password`) |
| `GET` | `/api/session` | Return current authenticated user |
| `DELETE` | `/api/session` | Logout |

### Users
| Method | Path | Description |
|---|---|---|
| `GET` | `/api/users` | List users (admin: all; non-admin: self only) |
| `GET` | `/api/users/{id}` | Get user by id |
| `POST` | `/api/users` | Create user (admin only) |
| `PUT` | `/api/users/{id}` | Update user (self or admin) |
| `DELETE` | `/api/users/{id}` | Delete user (admin only) |

### Devices
| Method | Path | Description |
|---|---|---|
| `GET` | `/api/devices` | List devices |
| `GET` | `/api/devices/{id}` | Get device by id |
| `POST` | `/api/devices` | Create device |
| `PUT` | `/api/devices/{id}` | Update device |
| `DELETE` | `/api/devices/{id}` | Delete device |

### Positions
| Method | Path | Description |
|---|---|---|
| `GET` | `/api/positions` | Latest positions, or filter by `deviceId`, `id[]`, `from`/`to` |
| `DELETE` | `/api/positions/{id}` | Delete single position |
| `DELETE` | `/api/positions?deviceId=&from=&to=` | Bulk delete by device + time range |
| `GET` | `/api/positions/gpx` | Export track as GPX |
| `GET` | `/api/positions/kml` | Export track as KML |
| `GET` | `/api/positions/kmz` | Export track as KMZ (KML in ZIP) |
| `GET` | `/api/positions/csv` | Export positions as CSV |

### Commands
| Method | Path | Description |
|---|---|---|
| `GET` | `/api/commands` | List saved commands |
| `POST` | `/api/commands` | Create saved command |
| `PUT` | `/api/commands/{id}` | Update saved command |
| `DELETE` | `/api/commands/{id}` | Delete saved command |
| `GET` | `/api/commands/send` | List sendable commands for a device |
| `POST` | `/api/commands/send` | Send command to a connected device |
| `GET` | `/api/commands/types` | List command types (device-specific or all) |

### Password
| Method | Path | Description |
|---|---|---|
| `POST` | `/api/password/reset` | Generate reset token for email (token logged; no mail system yet) |
| `POST` | `/api/password/update` | Verify token and set new password |

### Server
| Method | Path | Description |
|---|---|---|
| `GET` | `/api/server` | Version and server time (public) |

---

## Configuration

`appsettings.json` controls all runtime behaviour:

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=traccar;Username=postgres;Password=postgres"
  },
  "Database": {
    "Retry": {
      "Enable": true,
      "MaxRetryCount": 6,
      "MaxRetryDelaySeconds": 30,
      "CommandTimeoutSeconds": 30
    }
  },
  "Admin": {
    "Email": "admin",       // Seeded on first run if no users exist
    "Password": "admin"     // Change immediately after first login
  },
  "Server": {
    "Timeout": 1800,        // Global idle-disconnect timeout (seconds)
    "WebUrl": "http://localhost:5090"
  },
  "Protocols": {
    "h02":       { "Port": 5013 },
    "gt06":      { "Port": 5023 },
    "teltonika": { "Port": 5027, "Extended": true },
    "jt808":     { "Port": 5083, "Alternative": false }
    // Add "Timeout": N to override the global idle-disconnect for a single protocol
  },
  "Forward": {
    "Type": "kafka",          // "kafka" | "rabbitmq" — omit to disable forwarding
    "Url": "localhost:9092",  // Kafka: bootstrap servers; RabbitMQ: AMQP URI
    "Topic": "positions",     // Kafka topic / RabbitMQ routing key
    "Exchange": "traccar",    // RabbitMQ exchange (topic, durable)
    "Retry": {
      "Enable": true,
      "Delay": 100,           // Initial retry delay in milliseconds (doubles each attempt)
      "Count": 10,            // Max retry attempts
      "Limit": 100            // Max positions queued for retry
    }
  }
}
```

---

## Project Structure

```
Traccar.sln
src/
  Traccar.Model/          POCOs: Position, Device, User, Event, Command
  Traccar.Storage/        EF Core DbContext, migrations, JSON/attribute converters
  Traccar.Protocols/      DotNetty pipeline: BaseProtocol, BaseProtocolDecoder,
                          ProtocolOptions (IOptions), position forwarding (Kafka/RabbitMQ),
                          helpers (BitUtil, Checksum, Parser, DataConverter, etc.)
    H02/  Gt06/  Teltonika/  Khd/  GoSafe/  Gl200/
    Jt808/  Jt1078/  Meitrack/  Meiligao/  Niot/  Starcom/
  Traccar.Server/         ASP.NET Core host: Program.cs, Controllers/
test/
  Traccar.Protocols.Tests/  xUnit decoder tests ported from Java (hex frame → Position fields)
```

---

## Running Locally

```bash
cd src/Traccar.Server
dotnet run
```

The server starts on `http://localhost:5090`. Requires a running PostgreSQL instance reachable via `ConnectionStrings:DefaultConnection`. On first run:
- The `traccar` database schema is created and all migrations applied automatically.
- A default admin user is seeded (credentials from `Admin:Email`/`Admin:Password` in config).
- A warning is logged: **change the default password immediately**.

Login via the session endpoint before accessing any other API:
```bash
curl -c cookies.txt -X POST http://localhost:5090/api/session \
  -d "email=admin&password=admin"
```

---

## Authentication

Session-based cookie authentication (`traccar_session` cookie, 30-day sliding expiration). The cookie is `HttpOnly` and `SameSite=Lax`. Unauthenticated requests receive `401 Unauthorized` rather than a redirect — suitable for API clients and the Traccar web UI alike.

---

## Position Forwarding

When `Forward:Type` is configured, every decoded position is published to the broker after being persisted to the database. The payload is a JSON object matching the Java Traccar wire format:
```json
{ "position": { ... }, "device": { ... } }
```
Failed deliveries are optionally retried with exponential back-off (see `Forward:Retry` config).

---

## Out of Scope

The following Java Traccar features are intentionally deferred:
- **Fine-grained per-entity permissions** — there's no group/user-device/user-group link system. Access is scoped by `Device.ClientId` matching the caller's `User.ClientId` (administrators see everything); this only applies to the Reports endpoints today — `DevicesController`/`PositionsController` still return everything to any authenticated user.
- **SMS / text-channel commands** — no `SmsManager` equivalent.
- **Offline command queue** — commands can only be sent to currently-connected devices.
- **Reports** (trips, stops, events, summary, etc.).
- **Notifications** (push, email, Telegram, etc.) — no `NotificatorManager`.
- **Geofencing engine** — `Geofence` model not yet ported.
- **Geocoding** — `Geocoder` not ported.
- **Multi-instance / cluster** broadcast.
- **255 additional protocols** beyond the 12 listed above (267 total in upstream Traccar).

---

## License

Apache License 2.0 — same as the upstream [Traccar](https://github.com/traccar/traccar) project.

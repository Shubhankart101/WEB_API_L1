# The Auction House (WEB_API_L1)

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-239120?style=flat-square&logo=c-sharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-Web%20API-512BD4?style=flat-square&logo=dotnet)](https://learn.microsoft.com/aspnet/core/)
[![Swagger](https://img.shields.io/badge/Swagger-OpenAPI%203.0-85EA2D?style=flat-square&logo=swagger&logoColor=black)](https://swagger.io/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](LICENSE)

An enterprise-ready online auction RESTful Web API built on **ASP.NET Core (.NET 8)** following **Clean Layered Architecture** and **Domain-Driven Design (DDD)** principles. The platform empowers users to manage virtual assets, place items on scheduled auctions, place real-time bids with secured wallet balance holding, and settle ownership transactions upon auction expiry.

---

## 🏛️ Architecture Overview

The solution adheres strictly to separation of concerns across layered domain, contract, persistence, and presentation boundaries:

```
TheAuctionHouse/
├── TheAuctionHouse.API                     # ASP.NET Core Web API host, Controllers, JWT Auth & Swagger
├── TheAuctionHouse.Domain.Entities         # Domain models (Asset, Auction, BidHistory, PortalUser, Wallet)
├── TheAuctionHouse.Domain.DataContracts    # Repository & Unit-of-Work interfaces (IAppUnitOfWork, etc.)
├── TheAuctionHouse.Domain.ServiceContracts # DTOs, request/response models, and service interfaces
├── TheAuctionHouse.Domain.Services         # Business logic implementation enforcing SRS rules
├── TheAuctionHouse.Data.EFCore.InMemory    # Persistence layer (EF Core with SQLite & In-Memory providers)
├── TheAuctionHouse.Common                  # Result<T>, Error models, and cross-cutting validation utilities
└── TheAuctionHouse.Domain.Services.Tests   # Automated test suite (xUnit, Moq, Unit tests)
```

---

## ✨ Key Features & Business Rules

### 1. Asset Management (`/api/Asset`)
- **Creation & Validation**: Create assets with `Draft` status. Title must be 10–150 alphanumeric characters with spaces automatically trimmed and collapsed; description 10–1000 characters; retail value must be a positive integer.
- **Draft Updates**: Only assets in `Draft` state can be edited.
- **Status Lifecycle**: Transitions across `Draft` → `Open` (eligible for auction) → `Active` (currently in an open auction) → `Closed` / Ownership Transferred.
- **Safe Deletion**: Deletion is permitted strictly for assets in `Draft` or `Open` state.

### 2. Auction Engine & Bidding (`/api/Auction`)
- **Listing Parameters**:
  - **Reserve Price**: Non-zero positive integer (max \$9,999).
  - **Incremental Value**: Minimum bid increment (max \$999).
  - **Expiration Window**: 1 to 10,080 minutes (up to 7 days).
- **Public & Personalized Dashboard**: Retrieve all active auctions sorted by upcoming expiration, prioritizing listings where the calling user is the highest bidder.
- **Wallet-Backed Bidding (BR-1)**:
  - Bidders must have sufficient unblocked funds.
  - Submitting the top bid immediately blocks the bid amount in the bidder's wallet.
  - When outbid by another participant, previous funds are unblocked in real-time.
- **Settlement & Expiry (BR-2)**:
  - When an auction concludes with a winner, blocked funds are deducted from the buyer, credited to the seller, and asset ownership transfers to the buyer.
  - If expired with no bids, the asset status resets back to `Open` under the original owner.

### 3. Wallet Operations (`/api/Wallet`)
- **Balance Tracking**: Explicitly separates total available balance and blocked balance held for active bids.
- **Deposits & Withdrawals**: Support transactions up to \$999,999 with validation ensuring withdrawals never exceed unblocked balance.

### 4. Authentication & Security (`/api/PortalUser`)
- **JWT Bearer Authentication**: Secure issuance and validation of signed JSON Web Tokens for authorized operations.
- **Password Security**: Passwords hashed using standard cryptographic algorithms (SHA-256).
- **Self-Service Account Controls**: User registration, login, profile inspection, password reset, and forgot-password recovery.

### 5. Role-Based Administration (`/api/Admin`)
- **RBAC Protected**: Restricted to users bearing the `Admin` role claim.
- **User Governance**: Block or restore user account access, regenerate passwords, inspect user audit activity logs, or permanently delete accounts.

---

## 🔌 API Endpoints Reference

### Portal User & Authentication
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/PortalUser/signup` | Anonymous | Register a new user account |
| `POST` | `/api/PortalUser/login` | Anonymous | Authenticate and obtain JWT Bearer token |
| `POST` | `/api/PortalUser/forgot-password` | Anonymous | Request password reset token/link |
| `POST` | `/api/PortalUser/reset-password` | Authorized | Reset user password |
| `POST` | `/api/PortalUser/logout` | Authorized | Revoke active user session |
| `GET` | `/api/PortalUser/profile` | Authorized | Retrieve authenticated user profile |

### Assets
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/Asset` | Authorized | Create a new asset in Draft state |
| `PUT` | `/api/Asset` | Authorized | Update an existing Draft asset |
| `GET` | `/api/Asset/{assetId}` | Authorized | Get asset details by ID |
| `GET` | `/api/Asset/mine` | Authorized | List all assets owned by current user |
| `DELETE` | `/api/Asset/{assetId}` | Authorized | Delete a Draft or Open asset |

### Auctions
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/Auction` | Authorized | Create and publish a new auction |
| `POST` | `/api/Auction/bid` | Authorized | Place a bid on an active auction |
| `POST` | `/api/Auction/check-expiries` | Authorized | Trigger expiry evaluation and settlement |
| `GET` | `/api/Auction/{auctionId}` | Authorized | Get full auction details and bid history |
| `GET` | `/api/Auction/mine` | Authorized | Get auctions created by authenticated user |
| `GET` | `/api/Auction/open` | Anonymous | List all open auctions sorted by expiry |

### Wallet
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/Wallet/deposit` | Authorized | Deposit funds into user wallet |
| `POST` | `/api/Wallet/withdraw` | Authorized | Withdraw available (unblocked) funds |
| `GET` | `/api/Wallet/balance` | Authorized | Get wallet balance and blocked amounts |

### Administration
| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/Admin/access` | Admin | Block or unblock a target user |
| `POST` | `/api/Admin/regenerate-password`| Admin | Administratively reset user password |
| `GET` | `/api/Admin/activity` | Admin | Query activity audit log for users |
| `DELETE` | `/api/Admin/users/{targetUserId}` | Admin | Delete a user from the system |

---

## 🚀 Getting Started

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022, JetBrains Rider, or Visual Studio Code with the C# Dev Kit extension

### Setup & Run
1. **Clone the repository:**
   ```bash
   git clone https://github.com/Shubhankart101/WEB_API_L1.git
   cd WEB_API_L1
   ```

2. **Restore dependencies & build:**
   ```bash
   dotnet restore
   dotnet build .\TheAuctionHouse.sln
   ```

3. **Launch the Web API:**
   ```bash
   dotnet run --project .\TheAuctionHouse.API\TheAuctionHouse.API.csproj
   ```

4. **Access Swagger UI:**
   Once running, open your browser and navigate to:
   - `http://localhost:5000/` or `https://localhost:5001/`

---

## ⚙️ Configuration & Database Providers

The application supports both persistent **SQLite** and transient **In-Memory** database engines via `appsettings.json`:

```json
{
  "Database": {
    "Provider": "Sqlite"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=auctionhouse.db"
  },
  "Jwt": {
    "Key": "your-256-bit-secret-key-here",
    "Issuer": "TheAuctionHouse",
    "Audience": "TheAuctionHouseClient"
  },
  "SeedAdmin": {
    "Name": "System Admin",
    "Email": "admin@auctionhouse.local",
    "Password": "Admin@12345"
  }
}
```

> [!NOTE]
> On the first startup, an administrator account is automatically seeded using the `SeedAdmin` configuration (`admin@auctionhouse.local` / `Admin@12345`).

---

## 🧪 Automated Testing & Traceability

The test suite validates compliance with the software requirements specification (`SRS.md`):

```bash
dotnet test .\TheAuctionHouse.sln
```

### Traceability Mapping
Full requirements-to-test mapping is documented in [Requirements-Test-Mapping.md](Requirements-Test-Mapping.md), covering:
- **BR-1**: Balance verification and wallet fund holds/release during bidding.
- **BR-2**: Asset ownership transfer and final funds deduction upon auction settlement.
- **Asset Validation**: Title edge cases, character length, and state machine guards.
- **Wallet Boundaries**: Deposit/withdrawal limits and insufficient funds handling.
- **Admin & JWT**: Token issuance, admin claims enforcement, and account lifecycle.

---

## 📮 Postman Collection

A preconfigured Postman collection is included directly in the repository at:
`TheAuctionHouse.API/TheAuctionHouse.postman_collection.json`

Import this file into Postman to quickly test signup, JWT authentication, asset creation, auction bidding, wallet management, and administrative endpoints.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

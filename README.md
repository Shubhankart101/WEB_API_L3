# The Auction House

An online auction platform built with ASP.NET Web API (.NET 9) following a clean layered architecture. Users can register, list assets for auction, place bids, and settle transactions automatically when auctions expire.

---

## Architecture

```
TheAuctionHouse.Domain.Entities          → Core domain models
TheAuctionHouse.Domain.DataContracts     → Repository & unit-of-work interfaces
TheAuctionHouse.Domain.ServiceContracts  → Service interfaces + DTOs
TheAuctionHouse.Domain.Services          → Business logic implementation
TheAuctionHouse.Common                   → Shared Result<T>, Error, validation helpers
TheAuctionHouse.Data.EFCore.InMemory     → EF Core in-memory persistence
TheAuctionHouse.Domain.Services.Tests    → xUnit unit tests (23 tests, all passing)
```

---

## Features

### Assets
| Action | Rule |
|---|---|
| Create | Title 10–150 chars (alphanumeric + spaces, consecutive spaces collapsed), Description 10–1000 chars, RetailValue > 0 |
| Update | Only assets in **Draft** status |
| Delete | Only assets in **Draft** or **Open** status |
| Open to Auction | Moves asset from Draft → Open |
| Active | Set automatically when posted to a live auction |
| Change Ownership | On auction close, asset transferred to the buyer |

### Auctions
| Field | Rule |
|---|---|
| Reserve Price | Non-zero positive integer, max $9,999 |
| Incremental Value | Non-zero positive integer, max $999 |
| Expiration Time | 1–10,080 minutes (max 7 days) |

- Dashboard shows all live auctions sorted by nearest expiry, with the user's highest bids surfaced first.
- `CheckAuctionExpiriesAsync` closes expired auctions, transfers assets, and settles wallets.

### Bidding (Business Rule)
- Bidder must have sufficient **unblocked** wallet balance.
- On a successful bid, the bid amount is **blocked** in the bidder's wallet.
- When outbid, the previously blocked amount is **unblocked** immediately.
- On auction close, the winner's blocked amount is deducted and transferred to the seller.

### Wallet
| Action | Rule |
|---|---|
| Deposit | Positive integer, max $999,999 |
| Withdraw | Positive integer, max $999,999; available balance = Amount − BlockedAmount |
| Dashboard | Shows balance, blocked amount, and open bid history |

### User / Auth
- Sign up, login (JWT), logout, forgot password (email), reset password.
- Passwords stored as SHA-256 hashes.

---

## Getting Started

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download)

### Run Tests
```bash
dotnet restore
dotnet build
dotnet test
```

All 23 tests should pass.

---

## Project Status

| Layer | Status |
|---|---|
| Domain Entities | ✅ Complete |
| Repository Contracts | ✅ Complete |
| Service Contracts & DTOs | ✅ Complete |
| Business Logic (Services) | ✅ Complete |
| In-Memory EF Core persistence | ✅ Complete |
| Unit Tests | ✅ 23 / 23 passing |
| REST API controllers | 🔲 Planned |
| Auth middleware (JWT) | 🔲 Planned |

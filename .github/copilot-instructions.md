# Copilot Instructions for InvoiceProcessingSystem

## 🚀 Goal
Help contributors quickly understand and navigate the Invoice Processing System codebase, run the solution locally, and make safe changes with confidence.

> This repository uses a .NET 10 clean architecture style (Domain / Application / Infrastructure / WebAPI / Worker).

---

## ✅ Getting Started (Quick Start)

1. **Restore packages + build**
   ```bash
   dotnet restore
   dotnet build InvoiceProcessingSystem.slnx
   ```

2. **Update database (PostgreSQL required)**
   ```bash
   dotnet ef database update --project src/InvoiceSystem.Infrastructure/InvoiceSystem.Infrastructure.csproj
   ```

3. **Run the API**
   ```bash
   dotnet run --project src/InvoiceSystem.WebAPI/InvoiceSystem.WebAPI.csproj
   ```

4. **Run tests**
   - Unit tests: `dotnet test tests/InvoiceSystem.UnitTests/` 
   - Integration tests: `dotnet test tests/InvoiceSystem.IntegrationTests/`

---

## 🧱 Architecture Overview

This repo follows a layered clean architecture pattern:

- **Domain** (`src/InvoiceSystem.Domain`) – core business entities and domain logic.
- **Application** (`src/InvoiceSystem.Application`) – application services / use cases (depends on Domain).
- **Infrastructure** (`src/InvoiceSystem.Infrastructure`) – persistence (EF Core), migrations, and concrete implementations.
- **WebAPI** (`src/InvoiceSystem.WebAPI`) – ASP.NET Core API entry point with controllers, middleware, and DI wiring.
- **Worker** (`src/InvoiceSystem.Worker`) – background processing jobs.

---

## 🔧 Conventions & Notes

- **Target framework**: `net10.0`
- **Dependency versions** are managed centrally via `Directory.Packages.props`.
- **EF Core migrations** live in `src/InvoiceSystem.Infrastructure/Migrations/`.
- **CORS** is configured in `src/InvoiceSystem.WebAPI/Extensions/CorsServiceExtensions.cs`.
- **Global error handling** is implemented via `src/InvoiceSystem.WebAPI/Middleware/GlobalExceptionHandler.cs`.
- **Connection strings** and runtime config live in `src/InvoiceSystem.WebAPI/appsettings.json`.

---

## 🧠 What to Ask the Assistant

Here are some example prompts that work well in this project:

- “How do I add a new field to the `Invoice` entity and persist it in the database?”
- “Explain the lifecycle of an invoice in this system and where state transitions are enforced.”
- “I need to add a new endpoint to create an invoice. Which layers should be updated and what patterns should I follow?”
- “How do I run only the integration tests that depend on the database?”

---

## 🧩 Helpful File/Folder Map

- `src/InvoiceSystem.Domain/Entities` – core model types (Invoice, Participant, PaymentRecord, etc.)
- `src/InvoiceSystem.Infrastructure/Persistence` – EF DbContext + migrations
- `src/InvoiceSystem.WebAPI/Program.cs` – app startup + DI wiring
- `tests/InvoiceSystem.UnitTests` – unit tests (xUnit)
- `tests/InvoiceSystem.IntegrationTests` – integration tests (xUnit + real database)

---

## 🛠️ When You’re Unsure

- If a change affects database schema, add an EF migration (`dotnet ef migrations add <Name>` in Infrastructure project) and update any tests.
- When adding new behavior, prefer placing business rules in the Domain layer and keep WebAPI thin.
- Look for existing patterns in other features before creating a new approach.

---

## 📌 Notes for Copilot

- Prioritize following the existing clean architecture layering.
- Keep WebAPI controllers simple; most logic belongs in Application services or Domain model.
- If a change touches persistence, remember to update migrations and ensure tests cover database behavior.

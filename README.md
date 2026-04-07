# EgyptOnline — Backend Service

Brief overview and design notes for the EgyptOnline backend (ASP.NET Core).
**Project**: Backend API for EgyptOnline providing user registration, subscriptions, notifications, chat, presence, and background services.

**Tech stack**: ASP.NET Core, Entity Framework Core, SQL-based datastore (EF migrations present), Mongo (notifications), Docker, unit tests (xUnit), CI-friendly layout.
**Controllers (typical responsibilities)**
Note: controllers are located under `Presentation/` and wire into application services. Typical controllers you will find or want to add:
- `AuthController` (login, token issuance)
**Controllers & Endpoints**

Below is a catalog of the API controllers and their endpoints (HTTP method + route) discovered in the codebase. Routes are prefixed with `api/v{version}/`.

- `AuthController` (`api/v{version}/Auth`)
  - POST `register` — multipart form registration for providers (file + data)
  - POST `login` — username/email/phone + password login, returns tokens
  - POST `add-firebase-token` — add FCM token for current user (authorized)
  - POST `change-password` — change password for logged-in user (authorized)
  - POST `refresh` — refresh access token using refresh token
  - POST `logout` — revoke refresh token and remove FCM tokens
  - POST `upload-profile-image` — upload profile image (authorized, subscription required)

- `ProfileController` (`api/v{version}/Profile`) — (authorized)
  - GET `` — get current user profile
  - GET `subscription-status` — get subscription object for current user
  - PUT `` — update profile (RequireSubscription)
  - POST `set-occupied` — mark user occupied until midnight (RequireSubscription)
  - DELETE `remove-occupied` — remove occupation status (RequireSubscription)
  - GET `occupation-status` — get current occupation flag

- `ChatController` (`api/v{version}/Chat`) — (authorized)
  - GET `status/{userId}` — get online status for `userId`
  - GET `online-users` — list currently online users
  - GET `poll` — poll for new messages for authenticated user (`?sinceUtc=&pageSize=`)
  - GET `history/{targetUserId}` — paginated conversation with a target user (`?pageNumber=&pageSize=`)

- `NotificationController` (`api/v{version}/Notification`) — (authorized)
  - GET `my-notifications` — paginated notifications for authenticated user (`?pageNumber=&pageSize=`)
  - PATCH `{id}/read` — mark a notification as read
  - DELETE `{id}` — delete a single notification
  - DELETE `all` — delete all notifications for authenticated user

- `PaymentController` (`api/v{version}/Payment`)
  - POST `subscribe` — initiate subscription payment (query `paymentMethod`)
  - POST/GET `webhook` — payment gateway webhook handler
  - GET `status/{paymentId}` — get payment transaction status (authorized)
  - POST `pay-mobile-wallet` — placeholder mobile wallet payment endpoint

- `AdminController` (`api/v{version}/Admin`)
  - GET `users` — admin list/search users (authorized: Admin)
  - GET `payments/{userId}` — get payments for a user (Admin)
  - PUT `users/{userId}` — update user (Admin)
  - DELETE `users/{userId}` — delete user and related artifacts (Admin)
  - POST `login` — admin login (AllowAnonymous)
  - POST `logout` — revoke token (Admin logout placeholder)

- `SearchController` (`api/v{version}/Search`) — (authorized)
  - POST `workers` — search workers with filters (RequireSubscription)
  - POST `companies` — search companies (RequireSubscription)
  - POST `contractors` — search contractors (RequireSubscription)
  - POST `marketplaces` — search marketplaces (RequireSubscription)
  - POST `engineers` — search engineers (RequireSubscription)
  - POST `assistants` — search assistants (RequireSubscription)
  - POST `sculptors` — search sculptors (RequireSubscription)
  - POST `providers` — return top providers (no subscription required)

- `OTPController` (`api/v{version}/OTP`)
  - POST `request-otp` — send OTP to phone (AllowAnonymous)
  - POST `change-password` — verify OTP and reset password (AllowAnonymous)

- `GooglePlayBillingController` (`api/v{version}/GooglePlayBilling`)
  - POST `verify-subscription` — verify Google Play purchase and renew subscription (authorized)

Notes:
- Many endpoints require authenticated users with the `User` role; admin endpoints require the `Admin` role.
- `RequireSubscription` attribute marks operations that must check an active subscription in the database (fresh check) versus token-only checks that may be stale.
- For precise request/response DTOs inspect the controller method signatures and DTOs under `Application/Dtos` and `Domain/Models`.

# EgyptOnline — Backend Service

Brief overview and design notes for the EgyptOnline backend (ASP.NET Core).

**Project**: Backend API for EgyptOnline providing user registration, subscriptions, notifications, chat, presence, and background services.

**Tech stack**: ASP.NET Core, Entity Framework Core, SQL-based datastore (EF migrations present), Mongo (notifications), Docker, unit tests (xUnit), CI-friendly layout.

**Quick start**
- Build: `dotnet build EgyptOnline.csproj`
- Run locally: `dotnet run --project EgyptOnline.csproj`
- With Docker: `docker build -t egyptonline .` then `docker run -p 5000:80 egyptonline`

**Repository layout (high level)**
- `Application/` : DTOs, application services, interfaces.
- `Domain/` : domain models, attributes, middlewares, core logic.
- `Data/` : `ApplicationDBContext.cs`, EF Core factories, migrations.
- `Infrastructure/` : concrete services (ChatService, EmailService, NotificationMongoService, OccupationService, PresenceService, Repositories).
- `Presentation/` : web layer (API controllers, startup wiring), `Program.cs` and app configuration.
- `Tests/` & `EgyptOnline.Tests/` : unit/integration tests.

**Architecture overview**
- Layered architecture (Presentation → Application → Domain → Infrastructure → Data). This keeps controllers thin and delegates business logic to services in `Application`/`Domain`.
- Dependency inversion: interfaces in `Application` and implementations in `Infrastructure` are wired at startup (`ServiceExtensions.cs`).
- Persistence: EF Core for the primary relational store (migrations available) and a secondary store (Mongo) for notification/presence data to enable flexible schemas and high-throughput writes.

**Main components**
- API / Controllers: entry points for HTTP clients; responsible for validation and delegating to application services.
- Application services: orchestration layer implementing use-cases (registration, subscription, profile updates, notifications delivery).
- Domain models: business rules and invariants.
- Data access: EF Core `ApplicationDBContext` plus repository abstractions that keep queries testable.
- Infrastructure services: third-party integrations and background helpers (email, chat, presence, occupation, notification persistence).
- Background workers / HostedServices: scheduled or long-running processing (notification dispatch, presence reconciliation, billing tasks).

**Controllers (typical responsibilities)**
Note: controllers are located under `Presentation/` and wire into application services. Typical controllers you will find or want to add:
- `AuthController` (login, token issuance)
- `UserController` (registration, profile management)
- `SubscriptionController` (plans, subscription lifecycle)
- `NotificationController` (push or in-app notification endpoints)
- `ChatController` (chat endpoints or websockets integration)
- `AdminController` (operations and diagnostics)

Each controller should be thin: accept DTOs, validate, call application services, and return well-formed responses.

**Advantages**
- Clear separation of concerns: easier testing, maintainability, and onboarding.
- EF Core + migrations: repeatable schema changes and local dev flow.
- Polyglot persistence where appropriate: relational DB for transactional data, Mongo for high-throughput or flexible notification documents.
- Modular infrastructure services: `ChatService`, `EmailService`, `NotificationMongoService` allow swapping implementations (e.g., external providers) without changing business logic.
- Docker-friendly: `Dockerfile` and `docker-compose.yaml` support containerized deployment and consistent environments.

**Design trade-offs & rationale**
- Two datastores (relational + Mongo):
  - Pros: right tool per workload; notifications/presence scale independently and avoid locking relational DB.
  - Cons: added operational complexity (two systems to maintain, backup, monitor, and secure).
- Layered architecture vs. single monolith service classes:
  - Pros: testability and clear boundaries; business rules live in domain/services.
  - Cons: slightly more boilerplate and indirection for small features.
- Using repository abstraction and DI:
  - Pros: decouples EF from services, easier to mock for tests.
  - Cons: potential for anemic abstractions if repository interfaces mirror EF too closely—prefer explicit query/service methods for clarity.
- Background processing inside the same process vs separate worker service:
  - Pros: easier deployment and fewer services to manage for small-scale deployments.
  - Cons: heavy background workloads risk affecting API latency; consider offloading to a dedicated worker or serverless/job queue for scale.

**Operational notes**
- Logging: use structured logs and the existing `Logs/` folder for local investigation; integrate with centralized logging (ELK/Azure Monitor) in production.
- Observability: add request/response tracing and metrics (e.g., Prometheus, Application Insights).
- Resilience: implement retries for external calls (email, chat gateways) and handle `429`/transient failures gracefully.
- Security: protect secrets (do not commit `serviceAccountKey.json` for production); use managed identities or secure stores.

**Testing & CI**
- Unit tests live under `EgyptOnline.Tests/` and `Tests/`. Run with `dotnet test` or the provided `run-tests.bat` scripts.
- Add integration tests that run against in-memory or testcontainers for EF and a Mongo test instance.

**Deployment**
- Dockerize and push images to registry; `docker-compose.yaml` and `Dockerfile` included for local and simple production setups.
- For high scale, consider splitting API and background workers into separate containers and using Kubernetes or managed App Service / Container Apps.


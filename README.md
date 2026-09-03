# EgyptOnline — Backend Service

Backend API for EgyptOnline, a marketplace platform connecting service providers with clients in Egypt. Provides user registration, subscriptions, contracts, wallet management, KYC verification, job requests, real-time chat, notifications, and AI-powered assistance.

## Quick Start

```bash
# Build the project
dotnet build EgyptOnline.csproj

# Run locally
dotnet run --project EgyptOnline.csproj

# Run with Docker
docker build -t egyptonline .
docker run -p 8080:8080 egyptonline

# Run tests
dotnet test
```

## Technology Stack

- **Framework**: ASP.NET Core 9.0
- **Database**: PostgreSQL (primary) via Entity Framework Core 9.0
- **Secondary Storage**: MongoDB for chat messages and notifications
- **Real-time**: SignalR for chat and notification hubs
- **Authentication**: JWT Bearer tokens with ASP.NET Core Identity
- **Testing**: xUnit, FakeItEasy, EF Core InMemory
- **Logging**: Serilog (console + file)
- **Containerization**: Docker
- **Payment**: Paymob, Google Play Billing
- **External Services**: Firebase (FCM), SendGrid (email), Twilio (SMS), MinIO (CDN), Google Gemini (AI)

## Repository Structure

```
EgyptOnline/
├── Application/          # DTOs, application services, interfaces
├── Domain/              # Domain models, attributes, middlewares
├── Data/                # EF Core context and migrations
├── Infrastructure/      # Concrete implementations of external services
├── Presentation/        # Web layer (controllers, hubs)
├── Extensions/          # Service registration and configuration
├── Strategies/          # Strategy pattern implementations
├── Utilities/          # Helper classes
├── Migrations/         # EF Core database migrations
├── EgyptOnline.Tests.Unit/      # Unit tests
├── EgyptOnline.Tests.Integration/ # Integration tests
└── docs/               # Additional documentation
```

## Configuration

Required configuration in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "PostgreSQL connection string"
  },
  "Jwt": {
    "Key": "JWT signing key",
    "Issuer": "Issuer",
    "Audience": "Audience"
  },
  "MongoDB": {
    "ConnectionString": "MongoDB connection string"
  }
}
```

For development, copy `appsettings.Development.json` and add your local configuration values.

## API Endpoints

All routes are prefixed with `api/v{version}/`.

### Authentication (`/Auth`)
- POST `register` — Provider registration with file upload
- POST `login` — Login with username/email/phone + password
- POST `refresh` — Refresh access token
- POST `logout` — Revoke refresh token
- POST `change-password` — Change password (authorized)
- POST `add-firebase-token` — Add FCM token (authorized)
- POST `upload-profile-image` — Upload profile image (authorized, subscription required)

### Profile (`/Profile`)
- GET `` — Get current user profile (authorized)
- GET `subscription-status` — Get subscription status (authorized)
- PUT `` — Update profile (authorized, subscription required)
- POST `set-occupied` — Mark user occupied (authorized, subscription required)
- DELETE `remove-occupied` — Remove occupation status (authorized, subscription required)

### Chat (`/Chat`)
- GET `status/{userId}` — Get online status (authorized)
- GET `online-users` — List online users (authorized)
- GET `poll` — Poll for new messages (authorized)
- GET `history/{targetUserId}` — Get conversation history (authorized)

### Notifications (`/Notification`)
- GET `my-notifications` — Get paginated notifications (authorized)
- PATCH `{id}/read` — Mark notification as read (authorized)
- DELETE `{id}` — Delete notification (authorized)
- DELETE `all` — Delete all notifications (authorized)

### Payment (`/Payment`)
- POST `subscribe` — Initiate subscription payment
- POST `webhook` — Payment gateway webhook
- GET `status/{paymentId}` — Get payment status (authorized)

### Search (`/Search`)
- POST `workers` — Search workers (authorized, subscription required)
- POST `companies` — Search companies (authorized, subscription required)
- POST `contractors` — Search contractors (authorized, subscription required)
- POST `marketplaces` — Search marketplaces (authorized, subscription required)
- POST `engineers` — Search engineers (authorized, subscription required)
- POST `assistants` — Search assistants (authorized, subscription required)
- POST `sculptors` — Search sculptors (authorized, subscription required)
- POST `providers` — Get top providers (no subscription required)

### Admin (`/Admin`)
- GET `users` — List/search users (Admin)
- GET `payments/{userId}` — Get user payments (Admin)
- PUT `users/{userId}` — Update user (Admin)
- DELETE `users/{userId}` — Delete user (Admin)
- POST `login` — Admin login

### OTP (`/OTP`)
- POST `request-otp` — Send OTP to phone
- POST `change-password` — Verify OTP and reset password

### Google Play Billing (`/GooglePlayBilling`)
- POST `verify-subscription` — Verify Google Play purchase (authorized)

## Architecture

**Layered Architecture**: Presentation → Application → Domain → Infrastructure → Data

- **Presentation**: Controllers and SignalR hubs handle HTTP/WebSocket requests
- **Application**: Business logic orchestration and use-cases
- **Domain**: Core business entities, rules, and interfaces
- **Infrastructure**: External service implementations (email, chat, payment, etc.)
- **Data**: EF Core context for PostgreSQL, MongoDB client for document storage

**Key Patterns**:
- Dependency injection with interfaces in Application/Domain, implementations in Infrastructure
- Repository pattern for data access abstraction
- Strategy pattern for payment methods
- Background services for automated tasks (payouts, contract completion)
- SignalR for real-time features

## Database

### PostgreSQL (Primary)
- User accounts, subscriptions, contracts, wallets, KYC, job requests
- Managed via Entity Framework Core migrations
- Migration files in `Migrations/` directory

### MongoDB (Secondary)
- Chat messages (EgyptOnlineChat.Messages collection)
- Notifications for flexible schema and high-throughput writes

## Background Services

- **AutoPayoutBackgroundService**: Processes daily wage payouts at 5 PM Egypt time, expires stale contracts, completes incomplete contracts
- **SubscriptionCheckerService**: Currently disabled (commented out in Program.cs)

## Testing

### Unit Tests
- Located in `EgyptOnline.Tests.Unit/`
- Uses xUnit and FakeItEasy for mocking
- Run with: `dotnet test EgyptOnline.Tests.Unit/EgyptOnline.Tests.Unit.csproj`

### Integration Tests
- Located in `EgyptOnline.Tests.Integration/`
- Uses real PostgreSQL via test fixtures
- Run with: `dotnet test EgyptOnline.Tests.Integration/EgyptOnline.Tests.Integration.csproj`

## Deployment

### Docker
- Multi-stage Dockerfile for optimized image size
- Exposes port 8080
- Run with: `docker-compose up -d`

### Production Considerations
- Use environment variables for sensitive configuration
- Enable proper CORS policies for production domains
- Configure centralized logging and monitoring
- Review rate limiting for production traffic
- Consider separating API and background workers for scale

## Additional Documentation

- **AGENTS.md**: Instructions for AI coding agents
- **ADMIN_API.md**: Admin API endpoint documentation
- **GooglePlayBillingFlow.md**: Google Play Billing integration guide
- **docs/architecture.md**: Detailed architecture documentation
- **docs/development.md**: Development workflow and conventions
- **docs/testing.md**: Testing guidelines and practices
- **docs/database.md**: Database schema and migration guide
- **docs/api.md**: API documentation and conventions
- **docs/deployment.md**: Deployment procedures and infrastructure
- **docs/decisions.md**: Architectural decisions and rationale

## Security Notes

- Never commit secrets, API keys, or credentials to the repository
- Use environment variables for production configuration
- JWT tokens are used for authentication with configurable expiration
- Rate limiting is applied to all API endpoints (30 req/min average)
- Subscription validation middleware can be enabled via `RequireSubscription` attribute

## Development Notes

- Arabic character support in usernames (configured in Identity options)
- Phone-based authentication (email is optional)
- Idempotency keys for payment operations to prevent duplicate processing
- All datetime fields in Egypt operations use Egypt Standard Time (UTC+2)
- Logging to `Logs/` directory with daily rotation via Serilog

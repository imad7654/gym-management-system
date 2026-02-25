# Claude Configuration - The Fit Bear Gym Management System

This file provides context and conventions for AI assistance on this project.

## 🎯 Project Overview

A full-stack gym management system with:
- **Backend**: .NET 8 Web API with Clean Architecture
- **Frontend**: React 18 + TypeScript + Material-UI
- **Database**: MySQL 8.0
- **Authentication**: JWT with refresh tokens
- **Theme**: Green-themed UI with bear mascot

## ✨ Recent Improvements (2026-01-29)

The codebase has been enhanced with professional-grade improvements:

### Backend Enhancements
1. **Global Error Handling Middleware** - Centralized exception handling with custom exception types
2. **FluentValidation** - Comprehensive input validation with detailed error messages
3. **Unit Tests** - xUnit test project with sample validator tests (10 passing tests)
4. **Custom Exceptions** - ValidationException, NotFoundException, BusinessException, UnauthorizedException
5. **Configuration-Based Settings** - Pagination, validation rules, CORS origins in appsettings.json

### Frontend Enhancements
1. **Type-Safe Enums** - TypeScript enums matching backend with bidirectional mappings
2. **Error Boundary Component** - React error boundary for graceful error handling
3. **Centralized Constants** - Configuration file for API, pagination, validation rules
4. **Environment Variables** - .env.example template for configuration

### Code Quality Improvements
- Replaced magic numbers with configuration constants
- Type-safe enum handling (no more unsafe `as any` casts)
- Proper error propagation from backend to frontend
- Automatic request validation using FluentValidation filters

## 🚀 Quick Start Commands

### Start Development Servers
```bash
# Backend (start first)
cd backend/GymManagement
dotnet run --project src/GymManagement.Api/GymManagement.Api.csproj

# Frontend (start second)
cd frontend/gym-management-ui
npm run dev
```

### Database Commands
```bash
cd backend/GymManagement/src/GymManagement.Api

# Apply migrations
dotnet ef database update --project ../GymManagement.Infrastructure

# Create new migration
dotnet ef migrations add MigrationName --project ../GymManagement.Infrastructure

# Drop database
dotnet ef database drop --project ../GymManagement.Infrastructure --force
```

### Access Points
- **Frontend**: http://localhost:5173
- **Backend API**: http://localhost:5001
- **Swagger**: http://localhost:5001/swagger
- **Admin Login**: admin@gym.com / Admin@123

## 📁 Project Structure

```
gym-app/
├── backend/GymManagement/src/
│   ├── GymManagement.Domain/        # Entities, Enums, Repository Interfaces
│   ├── GymManagement.Application/   # DTOs, Services, Business Logic
│   ├── GymManagement.Infrastructure/# DbContext, Repositories, Migrations
│   └── GymManagement.Api/          # Controllers, Middleware, DI Config
│
├── frontend/gym-management-ui/src/
│   ├── assets/                      # SVG illustrations
│   ├── components/                  # React components (by feature)
│   ├── lib/                        # Theme, QueryClient setup
│   ├── pages/                      # Page components
│   ├── routes/                     # Route guards
│   ├── services/                   # API services (Axios)
│   ├── store/                      # Zustand stores
│   └── types/                      # TypeScript types
│
└── docker/                         # Docker Compose for MySQL
```

## 🏗️ Architecture Patterns

### Backend: Clean Architecture

**Dependency Flow**: API → Infrastructure → Application → Domain

- **Domain**: Pure C# entities, no dependencies
  - Entities: `Client`, `Package`, `Payment`, `User`, `Role`, `RefreshToken`, etc.
  - Enums: `Gender`, `MembershipStatus`, `PaymentStatus`, `PaymentMethod`
  - Interfaces: Repository contracts

- **Application**: Business logic
  - DTOs: Request/Response models
  - Services: Business logic implementations
  - Interfaces: Service contracts

- **Infrastructure**: Data access
  - `ApplicationDbContext`: EF Core DbContext
  - Repositories: Implement `IRepository<T>` and `IUnitOfWork`
  - Migrations: EF Core migrations
  - Seeding: Initial data (admin user, roles, packages)

- **API**: HTTP layer
  - Controllers: REST endpoints (versioned: `/api/v1/`)
  - Middleware: JWT authentication, error handling
  - DI: Service registration in `Program.cs`

### Frontend: Feature-Based

- **State Management**:
  - **Zustand**: Auth state (`authStore.ts`) - persisted to localStorage
  - **TanStack Query**: Server state with automatic caching/refetching

- **API Communication**:
  - Centralized Axios instance in `services/api.ts`
  - Interceptors handle token refresh automatically
  - Service files per entity: `authService.ts`, `clientService.ts`, `packageService.ts`

- **Routing**:
  - `ProtectedRoute`: Requires authentication
  - `RoleBasedRoute`: Requires specific role
  - Admin routes prefixed with `/admin/`

## 🔑 Key Conventions

### Backend Conventions

1. **Naming**:
   - Controllers: `[Entity]Controller` (e.g., `ClientsController`)
   - Services: `[Entity]Service` implementing `I[Entity]Service`
   - Repositories: `Repository<T>` implementing `IRepository<T>`
   - DTOs: `[Action][Entity]Dto` (e.g., `CreateClientDto`, `ClientDto`)

2. **Response Pattern**:
   ```csharp
   // Success
   return Ok(new { success = true, data = result });

   // Error
   return BadRequest(new { success = false, message = "Error message" });
   ```

3. **Soft Delete**:
   - All entities have `IsActive` property
   - Delete operations set `IsActive = false`
   - Queries filter by `IsActive == true`

4. **Authentication**:
   - Controllers use `[Authorize]` attribute
   - Role requirements: `[Authorize(Roles = "Admin")]`
   - Current user: `User.FindFirst(ClaimTypes.NameIdentifier)?.Value`

5. **Async/Await**:
   - All service methods are async
   - Controller actions return `Task<IActionResult>`

### Frontend Conventions

1. **Components**:
   - Functional components with TypeScript
   - Props interfaces named `[Component]Props`
   - Organized by feature in `components/` folder

2. **Styling**:
   - Material-UI `sx` prop for styling
   - Theme colors: `primary.main`, `success.main`, etc.
   - Green theme: Primary = `#2e7d32`, Dark = `#1b5e20`

3. **API Calls**:
   - Use TanStack Query hooks: `useQuery`, `useMutation`
   - Query keys: `['entity', 'action', ...params]`
   - Automatic cache invalidation on mutations

4. **Path Aliases** (configured in `vite.config.ts`):
   ```typescript
   @/          → src/
   @components → src/components/
   @services   → src/services/
   @store      → src/store/
   @pages      → src/pages/
   @types      → src/types/
   @lib        → src/lib/
   ```

5. **Type Safety**:
   - All API responses typed
   - Shared types in `types/index.ts`
   - No `any` types unless absolutely necessary

## 🗄️ Database Schema

### Key Entities

1. **Users** (Authentication)
   - Id, Email, PasswordHash, FirstName, LastName, IsActive
   - Relationships: Roles (many-to-many), RefreshTokens (one-to-many)

2. **Clients** (Members)
   - Id, FirstName, LastName, Email, Phone, Address, Gender, DateOfBirth
   - EmergencyContactName, EmergencyContactPhone
   - PackageId, MembershipStartDate, MembershipEndDate, MembershipStatus
   - PaymentStatus, IsActive, CreatedAt, UpdatedAt
   - Relationships: Package (many-to-one), Payments (one-to-many)

3. **Packages** (Membership Plans)
   - Id, Name, Description, Price, DurationInDays
   - IsActive, DisplayOrder, CreatedAt, UpdatedAt

4. **Payments**
   - Id, ClientId, Amount, PaymentDate, PaymentMethod, Notes
   - Relationships: Client (many-to-one), PaymentHistories (one-to-many)

### Enums

```csharp
Gender: Male, Female, Other, PreferNotToSay
MembershipStatus: Active, Expired, Pending, Cancelled, Suspended
PaymentStatus: Paid, Pending, Overdue, PartiallyPaid
PaymentMethod: Cash, CreditCard, DebitCard, BankTransfer, Other
```

## 🔐 Authentication Flow

1. **Login**: `POST /api/v1/auth/login`
   - Returns: AccessToken (15 min), RefreshToken (7 days), User info
   - Frontend stores in Zustand → localStorage

2. **API Requests**:
   - Include: `Authorization: Bearer {accessToken}`
   - Axios interceptor adds automatically

3. **Token Refresh** (automatic):
   - On 401 response, interceptor calls `POST /api/v1/auth/refresh-token`
   - Gets new access token
   - Retries original request

4. **Logout**: Clears tokens from store and localStorage

## 📝 Common Tasks

### Add New Entity (Full Stack)

1. **Domain Layer**:
   - Create entity in `Domain/Entities/`
   - Add to `ApplicationDbContext.cs`
   - Create migration: `dotnet ef migrations add Add[Entity]`

2. **Application Layer**:
   - Create DTOs in `Application/DTOs/`
   - Create service interface and implementation in `Application/Services/`

3. **Infrastructure Layer**:
   - Add repository if custom queries needed

4. **API Layer**:
   - Create controller in `Api/Controllers/`
   - Add endpoints with `[Authorize]` attributes

5. **Frontend**:
   - Add types to `types/index.ts`
   - Create service in `services/[entity]Service.ts`
   - Create components in `components/[entity]/`
   - Create page in `pages/[entity]/`
   - Add routes in `App.tsx`

### Debugging Tips

1. **Backend Errors**: Check Swagger for endpoint details
2. **Frontend API Errors**: Use React Query DevTools (bottom right corner)
3. **Auth Issues**: Clear localStorage and re-login
4. **Database Issues**: Check migrations, connection string, MySQL service

## 🎨 Design System

### Colors (Green Theme)
- **Primary**: `#2e7d32` (green.700)
- **Primary Dark**: `#1b5e20` (green.900)
- **Secondary**: `#66bb6a` (green.400)
- **Success**: `#4caf50` (green.500)
- **Error**: `#f44336` (red.500)
- **Warning**: `#ff9800` (orange.500)

### Status Colors
- **Active**: Green (`success.main`)
- **Expired**: Red (`error.main`)
- **Pending**: Orange (`warning.main`)
- **Paid**: Green chip
- **Overdue**: Red chip

### Typography
- Font: Roboto (Material-UI default)
- Headings: Bold (600-700 weight)
- Body: Regular (400 weight)

## 🧪 Testing

### Running Backend Tests
```bash
cd backend/GymManagement
dotnet test

# Run tests with coverage
dotnet test /p:CollectCoverage=true

# Run specific test
dotnet test --filter "FullyQualifiedName~CreateClientRequestValidatorTests"
```

### Test Structure
```
tests/GymManagement.UnitTests/
├── Validators/
│   ├── CreateClientRequestValidatorTests.cs
│   └── UpdateClientRequestValidatorTests.cs
└── Services/
    └── (Add service tests here)
```

### Writing Tests
- Use **xUnit** as the test framework
- Use **Moq** for mocking dependencies
- Use **FluentAssertions** for readable assertions
- Follow AAA pattern: Arrange, Act, Assert
- Test file naming: `{ClassUnderTest}Tests.cs`

Example:
```csharp
[Fact]
public void Validate_ValidRequest_ShouldNotHaveValidationErrors()
{
    // Arrange
    var validator = new CreateClientRequestValidator();
    var request = new CreateClientRequest { /* valid data */ };

    // Act
    var result = validator.Validate(request);

    // Assert
    result.IsValid.Should().BeTrue();
    result.Errors.Should().BeEmpty();
}
```

## 📝 Configuration Files

### Backend Configuration
- **appsettings.json**: Contains all configuration
  - Database connection strings
  - JWT settings (token expiration, secret key)
  - Pagination settings (page size, max size)
  - Validation rules (max lengths, password requirements)
  - Dashboard settings (recent items count, chart months)
  - File upload settings (max size, allowed extensions)
  - CORS allowed origins

### Frontend Configuration
- **src/constants/config.ts**: All frontend constants
  - API_CONFIG: Base URL, timeout
  - PAGINATION_CONFIG: Page sizes, defaults
  - VALIDATION_CONFIG: Input validation rules
  - QUERY_CONFIG: React Query settings (stale time, cache time)
  - STATUS_COLORS: UI color mappings
  - DASHBOARD_CONFIG: Dashboard widget settings

- **.env.example**: Environment variable template
  - Copy to `.env` for local development
  - Update `VITE_API_BASE_URL` for production

## 🚨 Important Notes

1. **Don't Expose Domain Entities**: Always use DTOs in API responses
2. **Always Use Soft Delete**: Never hard delete data (set `IsActive = false`)
3. **Validate Input**: Use FluentValidation or data annotations
4. **Handle Errors**: Use try-catch in services, return meaningful messages
5. **Use Transactions**: Use `UnitOfWork.SaveChangesAsync()` for multi-entity operations
6. **API Versioning**: All endpoints start with `/api/v1/`
7. **JWT Secret**: Change in production (in appsettings.json)
8. **CORS**: Configured for `http://localhost:5173` only

## 🧪 Testing

### Backend Test Project Structure
```
tests/
├── GymManagement.UnitTests/       # Unit tests
├── GymManagement.IntegrationTests/# Integration tests
└── GymManagement.Api.Tests/       # API tests
```

### Frontend Testing
- React Testing Library for components
- MSW for API mocking

## 📦 Key Dependencies

### Backend
- `Microsoft.EntityFrameworkCore` (8.0)
- `Pomelo.EntityFrameworkCore.MySql` (8.0)
- `Microsoft.AspNetCore.Authentication.JwtBearer` (8.0)
- `BCrypt.Net-Next` (4.0)
- `Swashbuckle.AspNetCore` (6.5)

### Frontend
- `react` (18.2)
- `@mui/material` (5.15)
- `@tanstack/react-query` (5.20)
- `zustand` (4.5)
- `axios` (1.6)
- `react-router-dom` (6.22)

## 🔄 Typical Development Workflow

1. **Backend Change**:
   - Modify entity/service/controller
   - Test in Swagger
   - Update frontend types if API changed

2. **Frontend Change**:
   - Update component/page
   - Test in browser
   - Check React Query DevTools for cache status

3. **Database Change**:
   - Modify entity in Domain
   - Create migration
   - Apply migration
   - Update seed data if needed
   - Update DTOs and services

## 💡 Code Examples

### Backend: Creating a Service Method
```csharp
public async Task<ClientDto> GetClientByIdAsync(int id)
{
    var client = await _unitOfWork.Clients
        .GetByIdAsync(id, c => c.Package);

    if (client == null || !client.IsActive)
        return null;

    return new ClientDto
    {
        Id = client.Id,
        FirstName = client.FirstName,
        // ... map properties
    };
}
```

### Frontend: API Call with React Query
```typescript
const { data: clients, isLoading } = useQuery({
  queryKey: ['clients'],
  queryFn: clientService.getAllClients,
});

const createMutation = useMutation({
  mutationFn: clientService.createClient,
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: ['clients'] });
  },
});
```

---

**Last Updated**: 2026-01-29
**Version**: 1.0.0

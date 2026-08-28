# 🐻 The Fit Bear Gym - Management System

A comprehensive gym management system built with modern web technologies, featuring client management, membership packages, payment tracking, and a beautiful green-themed user interface.

![License](https://img.shields.io/badge/license-MIT-green.svg)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![React](https://img.shields.io/badge/React-18-61DAFB)
![TypeScript](https://img.shields.io/badge/TypeScript-5-3178C6)

## 📋 Table of Contents

- [Features](#features)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Running the Application](#running-the-application)
- [Default Credentials](#default-credentials)
- [Architecture](#architecture)
- [API Documentation](#api-documentation)
- [Contributing](#contributing)
- [License](#license)

## ✨ Features

### 🏠 Public Features
- Beautiful homepage with gym branding
- Custom bear mascot illustration
- Motivational quotes wall
- Membership packages display
- Responsive design for all devices

### 👨‍💼 Admin Features

#### Client Management
- ✅ Create, read, update, and delete clients
- ✅ Track personal information, emergency contacts
- ✅ Membership status tracking (Active, Expired, Pending)
- ✅ Payment status monitoring (Paid, Pending, Overdue)
- ✅ Advanced search and filtering
- ✅ Pagination support
- ✅ Soft delete (clients marked inactive, never permanently deleted)

#### Package Management
- ✅ Create and manage membership packages
- ✅ Duration-based memberships (days)
- ✅ Custom pricing
- ✅ Active/inactive package control
- ✅ Display order customization

#### Dashboard & Analytics
- ✅ Real-time statistics
- ✅ Active client tracking
- ✅ Revenue analytics (today, this month, total)
- ✅ Expiring membership alerts
- ✅ Payment status overview

#### Authentication & Security
- ✅ JWT-based authentication
- ✅ Role-based access control (Admin, User roles)
- ✅ Secure password hashing (BCrypt)
- ✅ Refresh token mechanism
- ✅ Protected routes

### 🎨 Design Features
- Green-themed UI matching gym branding
- Material Design components
- Smooth animations and transitions
- Mobile-first responsive design
- Custom SVG illustrations
- Intuitive navigation

## 🛠️ Tech Stack

### Backend
- **Framework:** ASP.NET Core 8.0 Web API
- **Architecture:** Clean Architecture
  - Domain Layer (Entities, Enums)
  - Application Layer (DTOs, Business Logic)
  - Infrastructure Layer (Data Access, Repositories)
  - API Layer (Controllers, Middleware)
- **Database:** MySQL 8.0
- **ORM:** Entity Framework Core 8.0
- **Authentication:** JWT Bearer Tokens with Refresh Tokens
- **Password Hashing:** BCrypt.Net
- **Documentation:** Swagger/OpenAPI
- **CORS:** Configured for frontend integration

### Frontend
- **Framework:** React 18 with TypeScript
- **Build Tool:** Vite 5
- **UI Library:** Material-UI (MUI) v5
- **State Management:**
  - Zustand (Auth state)
  - TanStack Query / React Query (Server state)
- **Routing:** React Router v6 with protected routes
- **HTTP Client:** Axios with interceptors
- **Styling:** CSS-in-JS (MUI's sx prop)
- **Icons:** Material Icons
- **Form Handling:** Controlled components

### Development Tools
- **Version Control:** Git
- **Package Managers:** NuGet (backend), npm (frontend)
- **IDE:** Visual Studio Code / Visual Studio
- **API Testing:** Swagger UI
- **Hot Reload:** dotnet watch (backend), Vite HMR (frontend)

## 📁 Project Structure

```
gym-app/
├── backend/
│   └── GymManagement/
│       ├── GymManagement.sln
│       └── src/
│           ├── GymManagement.Domain/
│           │   ├── Entities/          # Client, Package, Payment, etc.
│           │   ├── Enums/             # Gender, MembershipStatus, etc.
│           │   └── Interfaces/        # Repository interfaces
│           │
│           ├── GymManagement.Application/
│           │   ├── DTOs/              # Request/Response models
│           │   ├── Services/          # Business logic
│           │   └── Interfaces/        # Service interfaces
│           │
│           ├── GymManagement.Infrastructure/
│           │   ├── Data/              # DbContext, Configurations
│           │   ├── Repositories/      # Repository implementations
│           │   └── Migrations/        # EF Core migrations
│           │
│           └── GymManagement.Api/
│               ├── Controllers/       # API endpoints
│               ├── Middleware/        # Authentication, Error handling
│               └── Program.cs         # App configuration
│
├── frontend/
│   └── gym-management-ui/
│       ├── src/
│       │   ├── assets/
│       │   │   └── illustrations/     # BearLifting.tsx (SVG)
│       │   │
│       │   ├── components/
│       │   │   ├── clients/           # ClientFormDialog, DeleteClientDialog
│       │   │   ├── home/              # MotivationalQuoteCard, PackageCard
│       │   │   └── layout/            # AdminLayout
│       │   │
│       │   ├── constants/
│       │   │   └── motivationalQuotes.ts
│       │   │
│       │   ├── lib/
│       │   │   └── theme.ts           # MUI green theme
│       │   │
│       │   ├── pages/
│       │   │   ├── auth/              # LoginPage
│       │   │   ├── clients/           # ClientsPage
│       │   │   ├── dashboard/         # DashboardPage
│       │   │   ├── home/              # HomePage
│       │   │   └── packages/          # PackagesPage
│       │   │
│       │   ├── routes/
│       │   │   ├── ProtectedRoute.tsx
│       │   │   └── RoleBasedRoute.tsx
│       │   │
│       │   ├── services/
│       │   │   ├── api.ts             # Axios instance
│       │   │   ├── authService.ts
│       │   │   ├── clientService.ts
│       │   │   └── packageService.ts
│       │   │
│       │   ├── store/
│       │   │   └── authStore.ts       # Zustand auth store
│       │   │
│       │   ├── types/
│       │   │   └── index.ts           # TypeScript types
│       │   │
│       │   ├── App.tsx                # Root component
│       │   └── main.tsx               # Entry point
│       │
│       ├── package.json
│       ├── tsconfig.json
│       └── vite.config.ts
│
├── .gitignore
├── LICENSE
└── README.md
```

## 📦 Prerequisites

Before you begin, ensure you have the following installed:

- **Node.js** (v18 or higher) - [Download](https://nodejs.org/)
- **.NET 8 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **MySQL 8.0** - [Download](https://dev.mysql.com/downloads/mysql/)
- **Git** - [Download](https://git-scm.com/downloads)

### Verify Installation

```bash
# Check Node.js
node --version  # Should be v18 or higher

# Check .NET
dotnet --version  # Should be 8.0.x

# Check MySQL
mysql --version

# Check Git
git --version
```

## 🚀 Installation

### 1. Clone the Repository

```bash
git clone https://github.com/YOUR_USERNAME/gym-management-system.git
cd gym-management-system
```

### 2. Database Setup

1. **Start MySQL Server**
   - Make sure MySQL is running on your machine
   - Default port: 3306

2. **Create Database** (Optional - migrations will create it)
   ```sql
   CREATE DATABASE GymManagementDb;
   ```

3. **Update Connection String**

   Edit `backend/GymManagement/src/GymManagement.Api/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=GymManagementDb;User=root;Password=root;"
     },
     "Jwt": {
       "Key": "your-super-secret-key-min-32-characters-long-for-production",
       "Issuer": "GymManagementApi",
       "Audience": "GymManagementClient",
       "AccessTokenExpirationMinutes": 15,
       "RefreshTokenExpirationDays": 7
     }
   }
   ```

   ⚠️ **Security Note:** Change the JWT Key in production!

### 3. Backend Setup

```bash
cd backend/GymManagement/src/GymManagement.Api

# Restore NuGet packages
dotnet restore

# Install EF Core CLI tools (if not already installed)
dotnet tool install --global dotnet-ef

# Apply database migrations (creates tables and seeds admin user)
dotnet ef database update --project ../GymManagement.Infrastructure

# You should see: "Done. Applied X migrations."
```

### 4. Frontend Setup

```bash
cd frontend/gym-management-ui

# Install npm packages
npm install

# This will install React, MUI, TanStack Query, and all dependencies
```

## 🏃 Running the Application

### Option 1: VS Code Split Terminal (Recommended)

This method keeps both servers running side-by-side:

1. **Open VS Code** in the project root
2. **Open Terminal** (`` Ctrl + ` ``)
3. **Split Terminal** (`Ctrl + Shift + 5`)

**Left Terminal - Backend:**
```bash
cd backend/GymManagement/src/GymManagement.Api
dotnet run --urls "http://localhost:5001"
```

**Right Terminal - Frontend:**
```bash
cd frontend/gym-management-ui
npm run dev
```

### Option 2: Separate Terminal Windows

**Terminal 1 - Backend:**
```bash
cd backend/GymManagement/src/GymManagement.Api
dotnet run --urls "http://localhost:5001"
```

Wait for: `Now listening on: http://localhost:5001`

**Terminal 2 - Frontend:**
```bash
cd frontend/gym-management-ui
npm run dev
```

Wait for: `Local: http://localhost:5173/`

### Access the Application

- **Frontend (User Interface):** http://localhost:5173
- **Backend API:** http://localhost:5001
- **Swagger Documentation:** http://localhost:5001/swagger

### Stopping the Servers

- Press `Ctrl + C` in each terminal
- Or close the terminal windows

## 🔐 Secrets and the admin account

There are no default credentials, and no secrets in this repository. The API refuses
to start if a required secret is missing, too short, or set to a value that was once
committed here.

### Local development

Secrets live in `dotnet user-secrets`, which stores them outside the repository:

```bash
cd backend/GymManagement/src/GymManagement.Api
dotnet user-secrets set "Jwt:SecretKey" "$(openssl rand -base64 48)"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3306;Database=gymdb;User=gymuser;Password=<your password>;"
```

On first run an admin account is created and its randomly generated password is
printed to the console **once**. Copy it then — it is not stored anywhere readable.
If you miss it, delete the user row and restart to get a new one.

To choose the credentials yourself instead:

```bash
dotnet user-secrets set "Seed:AdminEmail" "you@example.com"
dotnet user-secrets set "Seed:AdminPassword" "<a long password>"
```

### Production

Use environment variables, with `__` for the nesting:

| Variable | Purpose |
|---|---|
| `Jwt__SecretKey` | Signs login tokens. At least 32 bytes of random data. |
| `ConnectionStrings__DefaultConnection` | Database. Use an account that owns only `gymdb`, never `root`. |
| `Seed__AdminEmail` | First administrator. Required outside development. |
| `Seed__AdminPassword` | First administrator's password. Required outside development. |

`Seed:DemoData` controls the sample packages and placeholder gym details. It is on in
development and off everywhere else.

⚠️ Editing a secret that was already committed does not remove it — the old value stays
in git history. Always rotate to a genuinely new value rather than correcting the file.

## 🏗️ Architecture

### Clean Architecture Principles

```
┌─────────────────────────────────────────────────────────┐
│                      API Layer                           │
│  • Controllers (REST endpoints)                          │
│  • Middleware (Auth, Error Handling)                     │
│  • Dependency Injection Configuration                    │
└──────────────────┬──────────────────────────────────────┘
                   │ Depends on ↓
┌──────────────────▼──────────────────────────────────────┐
│                Application Layer                         │
│  • DTOs (Data Transfer Objects)                          │
│  • Services (Business Logic)                             │
│  • Interfaces (Service contracts)                        │
│  • Validation Logic                                      │
└──────────────────┬──────────────────────────────────────┘
                   │ Depends on ↓
┌──────────────────▼──────────────────────────────────────┐
│              Infrastructure Layer                        │
│  • DbContext (EF Core)                                   │
│  • Repositories (Data Access)                            │
│  • Migrations                                            │
│  • External Service Implementations                      │
└──────────────────┬──────────────────────────────────────┘
                   │ Depends on ↓
┌──────────────────▼──────────────────────────────────────┐
│                  Domain Layer                            │
│  • Entities (Client, Package, Payment, User)             │
│  • Enums (Gender, MembershipStatus, PaymentStatus)       │
│  • Domain Logic                                          │
│  • Repository Interfaces                                 │
└─────────────────────────────────────────────────────────┘
        ↑
        │ No dependencies - Pure business logic
```

### Frontend Architecture

- **Feature-Based Structure:** Components organized by feature (clients, home, layout)
- **Separation of Concerns:** Services, stores, and components are separate
- **Type Safety:** TypeScript throughout with centralized type definitions
- **State Management:**
  - **Zustand:** Auth state (user, tokens)
  - **React Query:** Server state (clients, packages) with caching
- **Clean Components:** Logic separated from presentation

## 📚 API Documentation

### Authentication Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/auth/register` | Register new user |
| POST | `/api/v1/auth/login` | User login (returns JWT) |
| POST | `/api/v1/auth/refresh` | Refresh access token |

### Client Endpoints (Protected - Requires Auth)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/clients` | Get paginated clients list |
| GET | `/api/v1/clients/{id}` | Get client details by ID |
| POST | `/api/v1/clients` | Create new client |
| PUT | `/api/v1/clients/{id}` | Update existing client |
| DELETE | `/api/v1/clients/{id}` | Soft delete client (marks inactive) |

### Package Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/packages` | Get all packages |
| GET | `/api/v1/packages/active` | Get only active packages (public) |
| GET | `/api/v1/packages/{id}` | Get package by ID |
| POST | `/api/v1/packages` | Create new package (admin) |
| PUT | `/api/v1/packages/{id}` | Update package (admin) |
| DELETE | `/api/v1/packages/{id}` | Delete package (admin) |

### Dashboard Endpoints (Admin Only)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/dashboard/stats` | Get dashboard statistics |
| GET | `/api/v1/dashboard/expiring-memberships` | Get expiring memberships list |
| GET | `/api/v1/dashboard/recent-clients` | Get recently added clients |
| GET | `/api/v1/dashboard/recent-payments` | Get recent payments |

For complete API documentation with request/response examples, run the backend and visit: **http://localhost:5001/swagger**

## 🎨 Design System

### Color Palette (Green Theme)
- **Primary Green:** `#2e7d32` - Buttons, links, main actions
- **Dark Green:** `#1b5e20` - Headers, navigation, accents
- **Light Green:** `#4caf50` - Hover states, highlights
- **Secondary Green:** `#66bb6a` - Secondary actions
- **White:** `#ffffff` - Backgrounds, cards
- **Gray:** `#f5f5f5` - Secondary backgrounds

### Typography
- **Font Family:** Roboto, Helvetica, Arial, sans-serif
- **Headings:** Bold (600-900 weight)
- **Body Text:** Regular (400 weight)
- **Button Text:** Medium (500 weight)

### Components
- **Cards:** Elevated with subtle shadows
- **Buttons:** Rounded corners (8px), uppercase text
- **Forms:** Clean inputs with validation
- **Tables:** Striped rows, hover effects
- **Dialogs:** Modal with backdrop blur

## 🔒 Security Features

- ✅ **JWT Authentication** with refresh tokens
- ✅ **Password Hashing** using BCrypt
- ✅ **Role-Based Access Control** (Admin, User)
- ✅ **Protected API Routes** - Unauthorized returns 401
- ✅ **CORS Configuration** - Prevents unauthorized origins
- ✅ **SQL Injection Protection** - EF Core parameterized queries
- ✅ **XSS Protection** - React's automatic escaping
- ✅ **Soft Delete** - Data never permanently lost

## 🧪 Testing

### Backend Tests
```bash
cd backend/GymManagement
dotnet test
```

### Frontend Tests
```bash
cd frontend/gym-management-ui
npm run test
```

## 🚢 Building for Production

### Backend Build
```bash
cd backend/GymManagement/src/GymManagement.Api
dotnet publish -c Release -o ./publish
```

Output will be in `./publish` folder ready for deployment.

### Frontend Build
```bash
cd frontend/gym-management-ui
npm run build
```

Output will be in `./dist` folder. Deploy to:
- Vercel
- Netlify
- AWS S3 + CloudFront
- Azure Static Web Apps

## 🐛 Troubleshooting

### MySQL Connection Issues
```bash
# Check if MySQL is running
# Windows: Services → MySQL80
# Mac: System Preferences → MySQL
# Linux: sudo systemctl status mysql

# Test connection
mysql -u root -p
```

### Port Already in Use
```bash
# Backend (5001)
# Windows: netstat -ano | findstr :5001
# Mac/Linux: lsof -i :5001

# Frontend (5173)
# Windows: netstat -ano | findstr :5173
# Mac/Linux: lsof -i :5173
```

### Migration Errors
```bash
# Drop and recreate database
cd backend/GymManagement/src/GymManagement.Api
dotnet ef database drop --project ../GymManagement.Infrastructure --force
dotnet ef database update --project ../GymManagement.Infrastructure
```

### CORS Errors
- Ensure frontend is running on `http://localhost:5173`
- Check `AllowedOrigins` in `Program.cs`
- Clear browser cache

## 🤝 Contributing

Contributions are welcome! Please follow these steps:

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/AmazingFeature`
3. Commit your changes: `git commit -m 'Add some AmazingFeature'`
4. Push to the branch: `git push origin feature/AmazingFeature`
5. Open a Pull Request

### Coding Standards
- **Backend:** Follow C# conventions, use async/await
- **Frontend:** Use TypeScript strict mode, functional components
- **Commits:** Use conventional commits (feat:, fix:, docs:)

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 👨‍💻 Author

**Imad Abi Ramia**
- GitHub: [@YOUR_USERNAME](https://github.com/YOUR_USERNAME)
- LinkedIn: [Your LinkedIn](https://linkedin.com/in/your-profile)

## 🙏 Acknowledgments

- Material-UI for the beautiful component library
- The ASP.NET Core team for an excellent framework
- The React team for an amazing frontend library
- All open-source contributors

## 📞 Support

If you have any questions or issues:
1. Check the [Troubleshooting](#troubleshooting) section
2. Review [Swagger Documentation](http://localhost:5001/swagger)
3. Open an [Issue](https://github.com/YOUR_USERNAME/gym-management-system/issues)

---

Made with 💪 and 🐻 by The Fit Bear Gym Team

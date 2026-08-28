# Setup Guide - Gym Management System

This guide will walk you through setting up and running the complete Gym Management System from scratch.

## Prerequisites Installation

### 1. Install .NET 8 SDK
```bash
# Download from: https://dotnet.microsoft.com/download/dotnet/8.0
# Verify installation
dotnet --version
# Should show: 8.0.x
```

### 2. Install Node.js 18+
```bash
# Download from: https://nodejs.org/
# Verify installation
node --version
npm --version
```

### 3. Install Docker Desktop
```bash
# Download from: https://www.docker.com/products/docker-desktop/
# Start Docker Desktop application
```

## Step-by-Step Setup

### Step 1: Start MySQL Database

```bash
# Navigate to docker directory
cd docker

# Start MySQL container
docker-compose up -d

# Verify MySQL is running
docker ps
# You should see: gym-mysql container running on port 3306
```

**MySQL Connection Details:**
- Host: `localhost`
- Port: `3306`
- Database: `gymdb`
- User: `gymuser`
- Password: `gympass123`

### Step 2: Setup Backend

```bash
# Navigate to API project
cd backend/GymManagement/src/GymManagement.Api

# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build

# Run database migrations (this creates all tables)
dotnet ef database update --project ../GymManagement.Infrastructure

# Run the API
dotnet run
```

The API will start on:
- HTTP: `http://localhost:5001`
- HTTPS: `https://localhost:7001`
- Swagger UI: `https://localhost:7001/swagger`

**What happens on first run:**
- Database tables are created
- Initial data is seeded:
  - 4 roles (Admin, Client, Trainer, Staff)
  - 1 admin user, with a random password printed once in the console
  - 4 sample packages
  - Gym information

### Step 3: Setup Frontend

```bash
# Open a NEW terminal window
# Navigate to frontend directory
cd frontend/gym-management-ui

# Install dependencies
npm install

# Start development server
npm run dev
```

The frontend will start on: `http://localhost:5173`

## Testing the Application

### 1. Access the Homepage
Open your browser and go to: `http://localhost:5173`

You should see:
- Hero section with gym title
- Membership packages (4 packages)
- About section
- Contact information

### 2. Login as Admin
1. Click "Admin Login" button
2. Enter credentials:
   - Email: the admin email (default `admin@gym.local` in development)
   - Password: the one printed in the API console on first run — see
     "Secrets and the admin account" in README.md
3. You'll be redirected to `/admin/dashboard`

### 3. Explore Admin Features
- **Dashboard**: View statistics, revenue, and summaries
- **Clients**: Add, edit, view clients
- **Packages**: Manage membership packages

## Troubleshooting

### MySQL Connection Error
**Problem:** "Unable to connect to MySQL"

**Solution:**
```bash
# 1. Check if MySQL container is running
docker ps

# 2. If not running, start it
cd docker
docker-compose up -d

# 3. Check MySQL logs
docker logs gym-mysql

# 4. Verify connection string in appsettings.json
# Should be: Server=localhost;Port=3306;Database=gymdb;User=gymuser;Password=gympass123;
```

### Migration Errors
**Problem:** "Migration failed" or "Table already exists"

**Solution:**
```bash
cd backend/GymManagement/src/GymManagement.Api

# Drop and recreate database
dotnet ef database drop --project ../GymManagement.Infrastructure --force

# Run migrations again
dotnet ef database update --project ../GymManagement.Infrastructure
```

### Port Already in Use
**Problem:** "Port 5001 is already in use"

**Solution:**
```bash
# Find process using port 5001
# Windows:
netstat -ano | findstr :5001

# Linux/Mac:
lsof -i :5001

# Kill the process or change port in appsettings.json
```

### Frontend Not Loading
**Problem:** "Cannot GET /"

**Solution:**
```bash
# 1. Check if backend is running on http://localhost:5001
# 2. Check console for CORS errors
# 3. Verify vite.config.ts proxy settings

# 4. Clear node_modules and reinstall
rm -rf node_modules package-lock.json
npm install
npm run dev
```

### CORS Errors
**Problem:** "CORS policy blocked"

**Solution:**
```csharp
// In Program.cs, verify CORS configuration includes your frontend URL
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});
```

## Development Workflow

### Running in Watch Mode

**Backend (auto-restart on changes):**
```bash
cd backend/GymManagement/src/GymManagement.Api
dotnet watch run
```

**Frontend (hot module replacement):**
```bash
cd frontend/gym-management-ui
npm run dev
```

### Adding a New Migration

```bash
cd backend/GymManagement/src/GymManagement.Api

# Create migration
dotnet ef migrations add MigrationName --project ../GymManagement.Infrastructure

# Apply migration
dotnet ef database update --project ../GymManagement.Infrastructure
```

### Viewing Database

**Using MySQL Workbench:**
1. Install MySQL Workbench
2. Connect with:
   - Host: localhost
   - Port: 3306
   - User: gymuser
   - Password: gympass123
   - Database: gymdb

**Using Command Line:**
```bash
docker exec -it gym-mysql mysql -u gymuser -p
# Enter password: gympass123

# View tables
USE gymdb;
SHOW TABLES;

# Query data
SELECT * FROM Users;
SELECT * FROM Clients;
SELECT * FROM Packages;
```

## Architecture Overview

### Backend (Clean Architecture)
```
Domain Layer
└── Entities, Interfaces (no dependencies)
    ↓
Application Layer
└── DTOs, Services, Business Logic (depends on Domain)
    ↓
Infrastructure Layer
└── DbContext, Repositories, External Services (depends on Application)
    ↓
API Layer
└── Controllers, Middleware (depends on Infrastructure)
```

### Frontend (Feature-Based)
```
Components
├── UI Components (buttons, inputs, cards)
├── Layout Components (headers, sidebars)
└── Feature Components (client forms, dashboard cards)

Services
└── API calls using Axios

Store (Zustand)
└── Auth state, UI state

Pages
└── Route-level components
```

## Next Steps

Now that everything is running, you can:

1. **Add more clients**: Go to Clients page → Add Client
2. **Create custom packages**: Go to Packages page → Add Package
3. **Record payments**: Select a client → Add Payment
4. **View analytics**: Dashboard shows real-time statistics
5. **Customize gym info**: Settings page (to be implemented)

## Production Deployment

### Backend
```bash
cd backend/GymManagement/src/GymManagement.Api
dotnet publish -c Release -o ./publish

# Update appsettings.json with production values
# Deploy to IIS, Azure, AWS, etc.
```

### Frontend
```bash
cd frontend/gym-management-ui
npm run build

# Output is in dist/ folder
# Deploy to Netlify, Vercel, Azure Static Web Apps, etc.
```

### Database
- Update connection string to production MySQL server
- Run migrations: `dotnet ef database update`
- Change JWT secret key in appsettings.json

## Support

For issues or questions:
- Check README.md for detailed documentation
- Review error logs in console
- Check API response in browser DevTools → Network tab

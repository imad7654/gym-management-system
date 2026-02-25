@echo off
echo Starting The Fit Bear Gym Backend...
echo.
cd /d "%~dp0backend\GymManagement\src\GymManagement.Api"
dotnet run --urls "http://localhost:5001"
pause

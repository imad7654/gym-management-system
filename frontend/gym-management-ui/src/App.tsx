import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { ThemeProvider } from '@mui/material/styles';
import CssBaseline from '@mui/material/CssBaseline';
import { queryClient } from '@lib/queryClient';
import { theme } from '@lib/theme';
import { ProtectedRoute } from '@routes/ProtectedRoute';
import { RoleBasedRoute } from '@routes/RoleBasedRoute';
import { AdminLayout } from '@components/layout';
import ErrorBoundary from '@components/ErrorBoundary';


// Pages
import { HomePage } from '@pages/home';
import { LoginPage } from '@pages/login';
import { RegisterPage } from '@pages/register';
import { MyMembershipPage } from '@pages/member';
import { DashboardPage } from '@pages/dashboard';
import { ClientsPage, MemberPage } from '@pages/clients';
import { PackagesPage } from '@pages/packages';
import { PaymentsPage } from '@pages/payments';
import { SettingsPage } from '@pages/settings';
import { ChangePasswordPage } from '@pages/account';
import { ImportMembersPage } from '@pages/import';
import { AuditTrailPage, DailyTakingsPage, WhoOwesMoneyPage } from '@pages/reports';
import { UsersPage } from '@pages/users';

function App() {
  return (
    <ErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <ThemeProvider theme={theme}>
          <CssBaseline />
          <BrowserRouter>
          <Routes>
            {/* Public routes */}
            <Route path="/" element={<HomePage />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />

            {/* Protected admin routes */}
<Route
  path="/admin"
  element={
    <ProtectedRoute>
      <RoleBasedRoute allowedRoles={['Admin']}>
        <AdminLayout />
      </RoleBasedRoute>
    </ProtectedRoute>
  }
>
  <Route path="dashboard" element={<DashboardPage />} />
  <Route path="clients" element={<ClientsPage />} />
  <Route path="clients/import" element={<ImportMembersPage />} />
  <Route path="clients/:id" element={<MemberPage />} />
  <Route path="payments" element={<PaymentsPage />} />
  <Route path="reports/who-owes" element={<WhoOwesMoneyPage />} />
  <Route path="reports/daily-takings" element={<DailyTakingsPage />} />
  <Route path="reports/history" element={<AuditTrailPage />} />
  <Route path="packages" element={<PackagesPage />} />
  <Route path="users" element={<UsersPage />} />
  <Route path="settings" element={<SettingsPage />} />
  <Route path="change-password" element={<ChangePasswordPage />} />
  <Route path="*" element={<Navigate to="/admin/dashboard" replace />} />
</Route>

            {/*
              The member area. Its own top-level branch rather than a child of /admin,
              because a member must never load an admin screen even briefly - every one of
              those calls an AdminOnly endpoint and would flash a page full of errors.
            */}
            <Route
              path="/member"
              element={
                <ProtectedRoute>
                  <RoleBasedRoute allowedRoles={['Client']}>
                    <MyMembershipPage />
                  </RoleBasedRoute>
                </ProtectedRoute>
              }
            />


            {/* Fallback */}
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
          </BrowserRouter>
        </ThemeProvider>
        <ReactQueryDevtools initialIsOpen={false} />
      </QueryClientProvider>
    </ErrorBoundary>
  );
}

export default App;

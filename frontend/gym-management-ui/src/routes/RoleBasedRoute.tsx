import { Navigate } from 'react-router-dom';
import { useAuthStore } from '@/store/authStore';

interface RoleBasedRouteProps {
  children: React.ReactNode;
  allowedRoles: string[];
}

/**
 * Keeps a screen to the roles that can actually use it.
 *
 * Someone signed in but not allowed here is sent to their own home rather than to the
 * public page: reception opening an owner-only screen has not been signed out, and
 * dumping them on the marketing page reads as though they had been.
 *
 * This hides screens; it does not secure them. Every endpoint behind these pages carries
 * its own policy, because a route guard is a convenience for the person and no obstacle
 * at all to anyone calling the API directly.
 */
export const RoleBasedRoute = ({ children, allowedRoles }: RoleBasedRouteProps) => {
  const { user, isAuthenticated, homePath } = useAuthStore();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  const userRoles = user?.roles || [];
  const hasAccess = allowedRoles.some((role) => userRoles.includes(role));

  if (!hasAccess) {
    return <Navigate to={homePath()} replace />;
  }

  return <>{children}</>;
};

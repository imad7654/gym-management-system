import { Navigate } from 'react-router-dom';
import { useAuthStore } from '@/store/authStore';
import { RoleBasedRoute } from './RoleBasedRoute';

/**
 * A screen only the owner opens. Reception is sent to its own home instead.
 *
 * A thin wrapper over <see cref="RoleBasedRoute"/> so the route table reads as a list of
 * who each screen belongs to, rather than repeating the same array of role names ten times.
 */
export const AdminOnly = ({ children }: { children: React.ReactNode }) => (
  <RoleBasedRoute allowedRoles={['Admin']}>{children}</RoleBasedRoute>
);

/**
 * Where an unknown /admin path lands.
 *
 * Not a fixed redirect to the dashboard any more: the dashboard is the owner's, so
 * reception would be bounced straight off it again. Each role has its own home.
 */
export const HomeRedirect = () => {
  const homePath = useAuthStore((state) => state.homePath);
  return <Navigate to={homePath()} replace />;
};

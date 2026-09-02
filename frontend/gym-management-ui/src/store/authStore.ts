import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { UserInfo } from '@app-types/index';

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  user: UserInfo | null;
  isAuthenticated: boolean;

  setTokens: (accessToken: string, refreshToken: string) => void;
  setUser: (user: UserInfo) => void;
  logout: () => void;
  isAdmin: () => boolean;
  isMember: () => boolean;
  /** Reception. Runs the desk; cannot reverse money, see revenue, or manage accounts. */
  isStaff: () => boolean;
  /** Where this user belongs after signing in. Members and admins do not share a home. */
  homePath: () => string;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      accessToken: null,
      refreshToken: null,
      user: null,
      isAuthenticated: false,

      setTokens: (accessToken, refreshToken) => {
        set({ accessToken, refreshToken, isAuthenticated: true });
      },

      setUser: (user) => {
        set({ user });
      },

      logout: () => {
        set({
          accessToken: null,
          refreshToken: null,
          user: null,
          isAuthenticated: false,
        });
      },

      isAdmin: () => {
        const user = get().user;
        return user?.roles?.includes('Admin') ?? false;
      },

      isMember: () => {
        const user = get().user;
        return user?.roles?.includes('Client') ?? false;
      },

      isStaff: () => {
        const user = get().user;
        return user?.roles?.includes('Staff') ?? false;
      },

      homePath: () => {
        // Admin is checked first: an account holding both roles is staff, and sending
        // them to the member area would hide every screen they actually work in.
        if (get().isAdmin()) return '/admin/dashboard';

        // Reception shares the admin panel, minus the screens it is refused. It lands on
        // the member list rather than the dashboard, whose figures are the owner's.
        if (get().isStaff()) return '/admin/clients';
        if (get().isMember()) return '/member';
        return '/';
      },
    }),
    {
      name: 'auth-storage',
    }
  )
);

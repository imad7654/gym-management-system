import axios from 'axios';
import { useAuthStore } from '@store/authStore';
import { API_CONFIG } from '@constants/config';

const axiosInstance = axios.create({
  baseURL: API_CONFIG.BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor to add auth token
axiosInstance.interceptors.request.use(
  (config) => {
    const token = useAuthStore.getState().accessToken;
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

/**
 * The refresh currently in flight, shared by every request waiting on it.
 *
 * The server rotates the refresh token: using one revokes it and issues a replacement.
 * That is deliberate, and it means a token can only be spent once.
 *
 * Refreshing per failed request therefore breaks as soon as a screen makes more than one
 * call - which most of them do. When the access token expires, every in-flight request
 * fails at the same moment, each reads the same refresh token, and each posts it. The first
 * wins; the rest present a token the server has just revoked, get a 401 from the refresh
 * itself, and log the user out. Reception is thrown back to the sign-in page at random,
 * mid-task, having done nothing wrong.
 *
 * Holding one promise means the token is spent once and everyone else waits for the result.
 */
let refreshInFlight: Promise<string> | null = null;

const refreshAccessToken = (): Promise<string> => {
  if (!refreshInFlight) {
    refreshInFlight = (async () => {
      const refreshToken = useAuthStore.getState().refreshToken;

      if (!refreshToken) {
        throw new Error('No refresh token stored');
      }

      // Deliberately the bare axios, not axiosInstance: going through the instance would
      // re-enter this interceptor if the refresh itself 401s, and recurse.
      const response = await axios.post(
        `${API_CONFIG.BASE_URL}/auth/refresh-token`,
        { refreshToken }
      );

      const { accessToken, refreshToken: newRefreshToken } = response.data.data;
      useAuthStore.getState().setTokens(accessToken, newRefreshToken);

      return accessToken as string;
    })().finally(() => {
      // Cleared so a later expiry can refresh again. Waiters already hold the promise, so
      // clearing it here cannot strand them.
      refreshInFlight = null;
    });
  }

  return refreshInFlight;
};

/** Guards against several failed waiters each redirecting to the login page in turn. */
let signingOut = false;

const signOut = () => {
  if (signingOut) return;
  signingOut = true;
  useAuthStore.getState().logout();
  window.location.href = '/login';
};

// Response interceptor to handle token refresh
axiosInstance.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config;

    if (error.response?.status === 401 && originalRequest && !originalRequest._retry) {
      originalRequest._retry = true;

      try {
        const accessToken = await refreshAccessToken();

        originalRequest.headers.Authorization = `Bearer ${accessToken}`;
        return axiosInstance(originalRequest);
      } catch (refreshError) {
        signOut();
        return Promise.reject(refreshError);
      }
    }

    return Promise.reject(error);
  }
);

export default axiosInstance;

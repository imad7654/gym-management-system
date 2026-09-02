import axiosInstance from '@lib/axios';
import {
  ApiResponse,
  LoginRequest,
  LoginResponse,
  ResetPasswordWithTokenRequest,
  UserInfo,
} from '@app-types/index';

export const authService = {
  login: async (credentials: LoginRequest): Promise<LoginResponse> => {
    const response = await axiosInstance.post<ApiResponse<LoginResponse>>(
      '/auth/login',
      credentials
    );
    return response.data.data!;
  },

  refreshToken: async (refreshToken: string): Promise<LoginResponse> => {
    const response = await axiosInstance.post<ApiResponse<LoginResponse>>(
      '/auth/refresh-token',
      { refreshToken }
    );
    return response.data.data!;
  },

  logout: async (refreshToken: string): Promise<void> => {
    await axiosInstance.post('/auth/logout', { refreshToken });
  },

  getCurrentUser: async (): Promise<UserInfo> => {
    const response = await axiosInstance.get<ApiResponse<UserInfo>>('/auth/me');
    return response.data.data!;
  },

  /**
   * Asks for a reset link. Always reports success, even for an address with no account -
   * the server answers identically either way so this cannot be used to discover who has
   * an account. The UI must not promise the email definitely went.
   */
  forgotPassword: async (email: string): Promise<string> => {
    const response = await axiosInstance.post<ApiResponse<never>>('/auth/forgot-password', {
      email,
    });
    return response.data.message ?? 'If that address has an account, a reset link is on its way.';
  },

  /** Sets a new password using the token from the emailed link. */
  resetPasswordWithToken: async (
    data: ResetPasswordWithTokenRequest
  ): Promise<void> => {
    await axiosInstance.post('/auth/reset-password', data);
  },

  changePassword: async (data: {
    currentPassword: string;
    newPassword: string;
    confirmPassword: string;
  }): Promise<void> => {
    await axiosInstance.put('/auth/change-password', data);
  },
};

import axiosInstance from '@lib/axios';
import { ApiResponse, LoginRequest, LoginResponse, UserInfo } from '@app-types/index';

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

  changePassword: async (data: {
    currentPassword: string;
    newPassword: string;
    confirmPassword: string;
  }): Promise<void> => {
    await axiosInstance.put('/auth/change-password', data);
  },
};

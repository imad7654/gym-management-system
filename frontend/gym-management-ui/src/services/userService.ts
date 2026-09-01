import axiosInstance from '@lib/axios';
import {
  ApiResponse,
  CreateUserRequest,
  ResetUserPasswordRequest,
  UpdateUserRequest,
  UserAccount,
} from '@app-types/index';

/**
 * The accounts that can sign in and run the gym.
 *
 * Every call here is admin-only. The server, not this file, decides what is allowed -
 * it refuses removing the last administrator and refuses removing your own account,
 * so a screen that forgot to check cannot cause a lockout.
 */
export const userService = {
  getUsers: async (): Promise<UserAccount[]> => {
    const response = await axiosInstance.get<ApiResponse<UserAccount[]>>('/users');
    return response.data.data!;
  },

  createUser: async (data: CreateUserRequest): Promise<UserAccount> => {
    const response = await axiosInstance.post<ApiResponse<UserAccount>>('/users', data);
    return response.data.data!;
  },

  updateUser: async (id: number, data: UpdateUserRequest): Promise<UserAccount> => {
    const response = await axiosInstance.put<ApiResponse<UserAccount>>(`/users/${id}`, data);
    return response.data.data!;
  },

  deactivateUser: async (id: number): Promise<void> => {
    await axiosInstance.delete(`/users/${id}`);
  },

  restoreUser: async (id: number): Promise<void> => {
    await axiosInstance.post(`/users/${id}/restore`);
  },

  resetPassword: async (id: number, data: ResetUserPasswordRequest): Promise<void> => {
    await axiosInstance.post(`/users/${id}/reset-password`, data);
  },
};

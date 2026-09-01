import axiosInstance from '@lib/axios';
import {
  ApiResponse,
  DashboardStats,
  ExpiringMembership,
} from '@app-types/index';

export const dashboardService = {
  getStats: async (): Promise<DashboardStats> => {
    const response = await axiosInstance.get<ApiResponse<DashboardStats>>(
      '/dashboard/stats'
    );
    return response.data.data!;
  },

  getExpiringMemberships: async (
    days: number = 7
  ): Promise<ExpiringMembership[]> => {
    const response = await axiosInstance.get<
      ApiResponse<ExpiringMembership[]>
    >('/dashboard/expiring-memberships', { params: { days } });
    return response.data.data!;
  },
};

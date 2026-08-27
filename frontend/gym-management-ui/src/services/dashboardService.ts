import axiosInstance from '@lib/axios';
import {
  ApiResponse,
  DashboardStats,
  ExpiringMembership,
  RecentClient,
  RecentPayment,
  RevenueChartData,
} from '@app-types/index';

export const dashboardService = {
  getStats: async (): Promise<DashboardStats> => {
    const response = await axiosInstance.get<ApiResponse<DashboardStats>>(
      '/dashboard/stats'
    );
    return response.data.data!;
  },

  getRevenueChart: async (months: number = 6): Promise<RevenueChartData> => {
    const response = await axiosInstance.get<ApiResponse<RevenueChartData>>(
      '/dashboard/revenue-chart',
      { params: { months } }
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

  getRecentPayments: async (count: number = 5): Promise<RecentPayment[]> => {
    const response = await axiosInstance.get<ApiResponse<RecentPayment[]>>(
      '/dashboard/recent-payments',
      { params: { count } }
    );
    return response.data.data!;
  },

  getRecentClients: async (count: number = 5): Promise<RecentClient[]> => {
    const response = await axiosInstance.get<ApiResponse<RecentClient[]>>(
      '/dashboard/recent-clients',
      { params: { count } }
    );
    return response.data.data!;
  },
};

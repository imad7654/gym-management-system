import axiosInstance from '@lib/axios';
import {
  ApiResponse,
  Today,
} from '@app-types/index';

export const dashboardService = {
  /** The first screen of the day: the drawer, who to ring, and who owes. */
  getToday: async (): Promise<Today> => {
    const response = await axiosInstance.get<ApiResponse<Today>>('/dashboard/today');
    return response.data.data!;
  },

  /** Records that somebody rang this member, or takes the mark back off. */
  markChased: async (clientId: number, called: boolean): Promise<void> => {
    await axiosInstance.post(`/dashboard/chased/${clientId}`, { called });
  },
};

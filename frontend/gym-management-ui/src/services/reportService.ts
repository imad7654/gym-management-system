import axiosInstance from '@lib/axios';
import { ApiResponse, WhoOwesMoney } from '@app-types/index';

/** The owner's money reports. */
export const reportService = {
  getWhoOwesMoney: async (): Promise<WhoOwesMoney> => {
    const response = await axiosInstance.get<ApiResponse<WhoOwesMoney>>(
      '/reports/who-owes'
    );
    return response.data.data!;
  },
};

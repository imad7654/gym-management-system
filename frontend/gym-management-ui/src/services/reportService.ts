import axiosInstance from '@lib/axios';
import { ApiResponse, DailyTakings, WhoOwesMoney } from '@app-types/index';

/** The owner's money reports. */
export const reportService = {
  getWhoOwesMoney: async (): Promise<WhoOwesMoney> => {
    const response = await axiosInstance.get<ApiResponse<WhoOwesMoney>>(
      '/reports/who-owes'
    );
    return response.data.data!;
  },

  /**
   * One day's money. Pass a gym-calendar date as yyyy-MM-dd; omitted means today in the
   * gym's timezone, which the server decides rather than the browser.
   */
  getDailyTakings: async (date?: string): Promise<DailyTakings> => {
    const response = await axiosInstance.get<ApiResponse<DailyTakings>>(
      '/reports/daily-takings',
      { params: date ? { date } : undefined }
    );
    return response.data.data!;
  },
};

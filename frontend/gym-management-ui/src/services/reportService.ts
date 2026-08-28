import axiosInstance from '@lib/axios';
import {
  ApiResponse,
  AuditEntry,
  AuditQueryParams,
  DailyTakings,
  PaginatedResult,
  WhoOwesMoney,
} from '@app-types/index';

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

  /** Who did what, newest first. */
  getAuditTrail: async (
    params: AuditQueryParams
  ): Promise<PaginatedResult<AuditEntry>> => {
    const response = await axiosInstance.get<
      ApiResponse<PaginatedResult<AuditEntry>>
    >('/reports/audit', { params });
    return response.data.data!;
  },
};

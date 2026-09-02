import axiosInstance from '@lib/axios';
import {
  ApiResponse,
  AuditEntry,
  AuditQueryParams,
  DailyTakings,
  PaginatedResult,
  RevenueMonthDetail,
  RevenueTrend,
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

  /**
   * Revenue and membership month by month. Admin only — this is revenue history, which is
   * the one thing reception is deliberately not shown.
   */
  getRevenueTrend: async (months = 12): Promise<RevenueTrend> => {
    const response = await axiosInstance.get<ApiResponse<RevenueTrend>>(
      '/reports/revenue',
      { params: { months } }
    );
    return response.data.data!;
  },

  /** One month opened up: every payment in it, split the way a day is split. */
  getRevenueMonth: async (
    year: number,
    month: number
  ): Promise<RevenueMonthDetail> => {
    const response = await axiosInstance.get<ApiResponse<RevenueMonthDetail>>(
      `/reports/revenue/${year}/${month}`
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

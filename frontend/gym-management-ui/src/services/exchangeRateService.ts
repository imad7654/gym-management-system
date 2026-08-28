import axiosInstance from '@lib/axios';
import { ApiResponse, ExchangeRate } from '@app-types/index';

/**
 * Today's LBP-per-USD rate: the owner sets it each morning, the payment form offers it
 * all day.
 *
 * Only ever a default. What a payment was actually converted at is stored on the payment,
 * so changing the rate at noon cannot restate the money taken in the morning.
 */
export const exchangeRateService = {
  /** Null when the owner has never set one — a gym that hasn't started, not an error. */
  getCurrent: async (): Promise<ExchangeRate | null> => {
    const response = await axiosInstance.get<ApiResponse<ExchangeRate | null>>(
      '/exchange-rates/current'
    );
    return response.data.data ?? null;
  },

  setToday: async (rate: number): Promise<ExchangeRate> => {
    const response = await axiosInstance.put<ApiResponse<ExchangeRate>>(
      '/exchange-rates/today',
      { rate }
    );
    return response.data.data!;
  },
};

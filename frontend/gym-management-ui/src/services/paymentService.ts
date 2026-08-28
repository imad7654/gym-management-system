import axiosInstance from '@lib/axios';
import {
  ApiResponse,
  CreatePaymentRequest,
  PaginatedResult,
  Payment,
  PaymentListItem,
  PaymentQueryParams,
  ReversePaymentRequest,
} from '@app-types/index';

export const paymentService = {
  getPayments: async (
    params: PaymentQueryParams
  ): Promise<PaginatedResult<PaymentListItem>> => {
    const response = await axiosInstance.get<
      ApiResponse<PaginatedResult<PaymentListItem>>
    >('/payments', { params });
    return response.data.data!;
  },

  getPayment: async (id: number): Promise<Payment> => {
    const response = await axiosInstance.get<ApiResponse<Payment>>(
      `/payments/${id}`
    );
    return response.data.data!;
  },

  createPayment: async (data: CreatePaymentRequest): Promise<Payment> => {
    const response = await axiosInstance.post<ApiResponse<Payment>>(
      '/payments',
      data
    );
    return response.data.data!;
  },

  /**
   * Reverses a payment. The original row is never edited - the server writes a second row
   * cancelling it and takes back the days it bought. Returns that reversal row.
   */
  reversePayment: async (id: number, reason?: string): Promise<Payment> => {
    const response = await axiosInstance.post<ApiResponse<Payment>>(
      `/payments/${id}/reverse`,
      { reason } satisfies ReversePaymentRequest
    );
    return response.data.data!;
  },
};

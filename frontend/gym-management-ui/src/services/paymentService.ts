import axiosInstance from '@lib/axios';
import {
  ApiResponse,
  CreatePaymentRequest,
  PaginatedResult,
  Payment,
  PaymentListItem,
  PaymentQueryParams,
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

  refundPayment: async (id: number): Promise<void> => {
    await axiosInstance.post(`/payments/${id}/refund`);
  },
};

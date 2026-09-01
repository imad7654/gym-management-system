import axiosInstance from '@lib/axios';
import {
  ApiResponse,
  Client,
  ClientListItem,
  ClientQueryParams,
  CreateClientRequest,
  PaginatedResult,
  MemberSummary,
  OutstandingPackage,
  UpdateClientRequest,
} from '@app-types/index';

export const clientService = {
  getClients: async (
    params: ClientQueryParams
  ): Promise<PaginatedResult<ClientListItem>> => {
    const response = await axiosInstance.get<
      ApiResponse<PaginatedResult<ClientListItem>>
    >('/clients', { params });
    return response.data.data!;
  },

  getClient: async (id: number): Promise<Client> => {
    const response = await axiosInstance.get<ApiResponse<Client>>(
      `/clients/${id}`
    );
    return response.data.data!;
  },

  createClient: async (data: CreateClientRequest): Promise<Client> => {
    const response = await axiosInstance.post<ApiResponse<Client>>(
      '/clients',
      data
    );
    return response.data.data!;
  },

  updateClient: async (
    id: number,
    data: UpdateClientRequest
  ): Promise<Client> => {
    const response = await axiosInstance.put<ApiResponse<Client>>(
      `/clients/${id}`,
      data
    );
    return response.data.data!;
  },

  deleteClient: async (id: number): Promise<void> => {
    await axiosInstance.delete(`/clients/${id}`);
  },

  restoreClient: async (id: number): Promise<void> => {
    await axiosInstance.post(`/clients/${id}/restore`);
  },

  getExpiringClients: async (days: number = 7): Promise<ClientListItem[]> => {
    const response = await axiosInstance.get<ApiResponse<ClientListItem[]>>(
      '/clients/expiring',
      { params: { days } }
    );
    return response.data.data!;
  },

  /** Everything the member page shows, in one request. */
  getMemberSummary: async (id: number): Promise<MemberSummary> => {
    const response = await axiosInstance.get<ApiResponse<MemberSummary>>(
      `/clients/${id}/summary`
    );
    return response.data.data!;
  },

  /**
   * Money already put toward packages this member has not finished paying for.
   *
   * The payment desk asks for this before judging whether an amount is short: without it
   * the form compared the amount against the full price and warned that a payment which
   * actually completes the package would not extend the membership.
   */
  getOutstanding: async (id: number): Promise<OutstandingPackage[]> => {
    const response = await axiosInstance.get<ApiResponse<OutstandingPackage[]>>(
      `/clients/${id}/outstanding`
    );
    return response.data.data!;
  },

  suspendClient: async (id: number, reason?: string): Promise<void> => {
    await axiosInstance.post(`/clients/${id}/suspend`, { reason: reason ?? null });
  },

  resumeClient: async (id: number): Promise<void> => {
    await axiosInstance.post(`/clients/${id}/resume`);
  },
};

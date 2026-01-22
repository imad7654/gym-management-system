import axiosInstance from '@lib/axios';
import {
  ApiResponse,
  Client,
  ClientListItem,
  ClientQueryParams,
  CreateClientRequest,
  PaginatedResult,
  UpdateClientRequest,
} from '@types/index';

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
};

import axiosInstance from '@lib/axios';
import {
  ApiResponse,
  CreatePackageRequest,
  Package,
  UpdatePackageRequest,
} from '@app-types/index';

export const packageService = {
  getPackages: async (includeInactive: boolean = false): Promise<Package[]> => {
    const response = await axiosInstance.get<ApiResponse<Package[]>>(
      '/packages',
      { params: { includeInactive } }
    );
    return response.data.data!;
  },

  getActivePackages: async (): Promise<Package[]> => {
    const response = await axiosInstance.get<ApiResponse<Package[]>>(
      '/packages/active'
    );
    return response.data.data!;
  },

  getPackage: async (id: number): Promise<Package> => {
    const response = await axiosInstance.get<ApiResponse<Package>>(
      `/packages/${id}`
    );
    return response.data.data!;
  },

  createPackage: async (data: CreatePackageRequest): Promise<Package> => {
    const response = await axiosInstance.post<ApiResponse<Package>>(
      '/packages',
      data
    );
    return response.data.data!;
  },

  updatePackage: async (
    id: number,
    data: UpdatePackageRequest
  ): Promise<Package> => {
    const response = await axiosInstance.put<ApiResponse<Package>>(
      `/packages/${id}`,
      data
    );
    return response.data.data!;
  },

  deletePackage: async (id: number): Promise<void> => {
    await axiosInstance.delete(`/packages/${id}`);
  },

  toggleStatus: async (id: number): Promise<void> => {
    await axiosInstance.put(`/packages/${id}/toggle-status`);
  },
};

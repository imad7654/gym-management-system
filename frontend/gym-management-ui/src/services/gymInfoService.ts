import axiosInstance from '@lib/axios';
import { ApiResponse, GymInfo, UpdateGymInfoRequest } from '@app-types/index';

export const gymInfoService = {
  getGymInfo: async (): Promise<GymInfo> => {
    const response = await axiosInstance.get<ApiResponse<GymInfo>>(
      '/gym-info'
    );
    return response.data.data!;
  },

  updateGymInfo: async (data: UpdateGymInfoRequest): Promise<GymInfo> => {
    const response = await axiosInstance.put<ApiResponse<GymInfo>>(
      '/gym-info',
      data
    );
    return response.data.data!;
  },
};

import axiosInstance from '@lib/axios';
import {
  ApiResponse,
  LoginResponse,
  MemberAccount,
  MyMembership,
  Payment,
  RegisterMemberRequest,
  ResetMemberPasswordRequest,
} from '@app-types/index';

/**
 * The member's side of the system, plus the two things the owner does to a member's login.
 *
 * Nothing here takes a member id. `/me` resolves the membership from the signed-in user on
 * the server, so there is no id in a URL for one member to change into another's.
 */
export const memberService = {
  /**
   * Sign up by matching a membership the gym already created. Anonymous - the person has
   * no account yet - and it signs them straight in on success.
   */
  register: async (data: RegisterMemberRequest): Promise<LoginResponse> => {
    const response = await axiosInstance.post<ApiResponse<LoginResponse>>(
      '/auth/register',
      data
    );
    return response.data.data!;
  },

  getMyMembership: async (): Promise<MyMembership> => {
    const response = await axiosInstance.get<ApiResponse<MyMembership>>('/me/membership');
    return response.data.data!;
  },

  getMyPayments: async (): Promise<Payment[]> => {
    const response = await axiosInstance.get<ApiResponse<Payment[]>>('/me/payments');
    return response.data.data ?? [];
  },

  /** Whether this member has a login. Admin only. */
  getAccount: async (clientId: number): Promise<MemberAccount> => {
    const response = await axiosInstance.get<ApiResponse<MemberAccount>>(
      `/clients/${clientId}/account`
    );
    return response.data.data!;
  },

  /**
   * The owner setting a member's password for them. The only recovery a member has, since
   * there is no email anywhere in this system to send a reset link with.
   */
  resetPassword: async (
    clientId: number,
    data: ResetMemberPasswordRequest
  ): Promise<void> => {
    await axiosInstance.post(`/clients/${clientId}/account/reset-password`, data);
  },
};

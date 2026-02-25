// Enums matching backend
export enum Gender {
  Male = 0,
  Female = 1,
  Other = 2,
  PreferNotToSay = 3
}

export enum MembershipStatus {
  Active = 0,
  Expired = 1,
  Pending = 2,
  Cancelled = 3,
  Suspended = 4
}

export enum PaymentStatus {
  Paid = 0,
  Pending = 1,
  Overdue = 2,
  PartiallyPaid = 3
}

export enum PaymentMethod {
  Cash = 0,
  CreditCard = 1,
  DebitCard = 2,
  BankTransfer = 3,
  Other = 4
}

// Type-safe string literals
export type GenderString = 'Male' | 'Female' | 'Other' | 'PreferNotToSay';
export type MembershipStatusString = 'Active' | 'Expired' | 'Pending' | 'Cancelled' | 'Suspended';
export type PaymentStatusString = 'Paid' | 'Pending' | 'Overdue' | 'PartiallyPaid';
export type PaymentMethodString = 'Cash' | 'CreditCard' | 'DebitCard' | 'BankTransfer' | 'Other';

// Mapping helpers
export const GenderMap: Record<GenderString, Gender> = {
  Male: Gender.Male,
  Female: Gender.Female,
  Other: Gender.Other,
  PreferNotToSay: Gender.PreferNotToSay
};

export const GenderReverseMap: Record<Gender, GenderString> = {
  [Gender.Male]: 'Male',
  [Gender.Female]: 'Female',
  [Gender.Other]: 'Other',
  [Gender.PreferNotToSay]: 'PreferNotToSay'
};

export const MembershipStatusMap: Record<MembershipStatusString, MembershipStatus> = {
  Active: MembershipStatus.Active,
  Expired: MembershipStatus.Expired,
  Pending: MembershipStatus.Pending,
  Cancelled: MembershipStatus.Cancelled,
  Suspended: MembershipStatus.Suspended
};

export const MembershipStatusReverseMap: Record<MembershipStatus, MembershipStatusString> = {
  [MembershipStatus.Active]: 'Active',
  [MembershipStatus.Expired]: 'Expired',
  [MembershipStatus.Pending]: 'Pending',
  [MembershipStatus.Cancelled]: 'Cancelled',
  [MembershipStatus.Suspended]: 'Suspended'
};

export const PaymentStatusMap: Record<PaymentStatusString, PaymentStatus> = {
  Paid: PaymentStatus.Paid,
  Pending: PaymentStatus.Pending,
  Overdue: PaymentStatus.Overdue,
  PartiallyPaid: PaymentStatus.PartiallyPaid
};

export const PaymentMethodMap: Record<PaymentMethodString, PaymentMethod> = {
  Cash: PaymentMethod.Cash,
  CreditCard: PaymentMethod.CreditCard,
  DebitCard: PaymentMethod.DebitCard,
  BankTransfer: PaymentMethod.BankTransfer,
  Other: PaymentMethod.Other
};

// API Response types
export interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data?: T;
  errors?: string[];
}

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

// Auth types
export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiration: string;
  user: UserInfo;
}

export interface UserInfo {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  roles: string[];
}

// Client types
export interface Client {
  id: number;
  firstName: string;
  lastName: string;
  fullName: string;
  email?: string;
  phoneNumber: string;
  dateOfBirth?: string;
  gender?: GenderString;
  address?: string;
  emergencyContact?: string;
  emergencyPhone?: string;
  profileImageUrl?: string;
  notes?: string;
  currentPackageId?: number;
  currentPackageName?: string;
  membershipStartDate?: string;
  membershipEndDate?: string;
  membershipStatus: MembershipStatusString;
  paymentStatus: PaymentStatusString;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface ClientListItem {
  id: number;
  fullName: string;
  phoneNumber: string;
  email?: string;
  currentPackageName?: string;
  membershipEndDate?: string;
  membershipStatus: MembershipStatusString;
  paymentStatus: PaymentStatusString;
  isActive: boolean;
}

export interface CreateClientRequest {
  firstName: string;
  lastName: string;
  email?: string;
  phoneNumber: string;
  dateOfBirth?: string;
  gender?: GenderString;
  address?: string;
  emergencyContact?: string;
  emergencyPhone?: string;
  notes?: string;
  packageId?: number;
  membershipStartDate?: string;
}

export interface UpdateClientRequest extends CreateClientRequest {
  membershipEndDate?: string;
  paymentStatus?: PaymentStatusString;
}

export interface ClientQueryParams {
  page?: number;
  pageSize?: number;
  search?: string;
  membershipStatus?: string;
  paymentStatus?: string;
  includeInactive?: boolean;
  sortBy?: string;
  sortDescending?: boolean;
}

// Package types
export interface Package {
  id: number;
  name: string;
  description?: string;
  durationDays: number;
  price: number;
  isActive: boolean;
  displayOrder: number;
  createdAt?: string;
  updatedAt?: string;
}

export interface CreatePackageRequest {
  name: string;
  description?: string;
  durationDays: number;
  price: number;
  isActive?: boolean;
  displayOrder?: number;
}

export type UpdatePackageRequest = CreatePackageRequest;

// Payment types
export interface Payment {
  id: number;
  clientId: number;
  clientName: string;
  packageId: number;
  packageName: string;
  amount: number;
  paymentDate: string;
  paymentMethod: string;
  status: string;
  periodStartDate: string;
  periodEndDate: string;
  transactionReference?: string;
  notes?: string;
  createdAt: string;
}

export interface CreatePaymentRequest {
  clientId: number;
  packageId: number;
  amount: number;
  paymentDate: string;
  paymentMethod: PaymentMethodString;
  periodStartDate: string;
  periodEndDate: string;
  transactionReference?: string;
  notes?: string;
}

export interface PaymentQueryParams {
  page?: number;
  pageSize?: number;
  clientId?: number;
  startDate?: string;
  endDate?: string;
  status?: string;
  paymentMethod?: string;
  sortBy?: string;
  sortDescending?: boolean;
}

// Dashboard types
export interface DashboardStats {
  totalActiveClients: number;
  totalClients: number;
  newClientsThisMonth: number;
  expiringMembershipsCount: number;
  paymentSummary: {
    paidCount: number;
    pendingCount: number;
    overdueCount: number;
  };
  revenueSummary: {
    todayRevenue: number;
    thisMonthRevenue: number;
    lastMonthRevenue: number;
    totalRevenue: number;
  };
}

export interface RevenueChartData {
  data: {
    label: string;
    revenue: number;
    transactionCount: number;
  }[];
}

export interface ExpiringMembership {
  clientId: number;
  clientName: string;
  phoneNumber: string;
  packageName: string;
  expirationDate: string;
  daysUntilExpiration: number;
}

export interface RecentPayment {
  id: number;
  clientName: string;
  amount: number;
  paymentDate: string;
  paymentMethod: string;
}

export interface RecentClient {
  id: number;
  fullName: string;
  phoneNumber: string;
  packageName?: string;
  createdAt: string;
}

// Gym Info types
export interface GymInfo {
  id: number;
  gymName: string;
  logoUrl?: string;
  description?: string;
  address?: string;
  phoneNumber?: string;
  email?: string;
  facebookUrl?: string;
  instagramUrl?: string;
  twitterUrl?: string;
  operatingHours?: string;
  heroTitle?: string;
  heroSubtitle?: string;
  heroImageUrl?: string;
  aboutTitle?: string;
  aboutContent?: string;
  metaTitle?: string;
  metaDescription?: string;
}

export type UpdateGymInfoRequest = Omit<GymInfo, 'id'>;

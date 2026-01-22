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
  gender?: string;
  address?: string;
  emergencyContact?: string;
  emergencyPhone?: string;
  profileImageUrl?: string;
  notes?: string;
  currentPackageId?: number;
  currentPackageName?: string;
  membershipStartDate?: string;
  membershipEndDate?: string;
  membershipStatus: string;
  paymentStatus: string;
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
  membershipStatus: string;
  paymentStatus: string;
  isActive: boolean;
}

export interface CreateClientRequest {
  firstName: string;
  lastName: string;
  email?: string;
  phoneNumber: string;
  dateOfBirth?: string;
  gender?: 'Male' | 'Female' | 'Other';
  address?: string;
  emergencyContact?: string;
  emergencyPhone?: string;
  notes?: string;
  packageId?: number;
  membershipStartDate?: string;
}

export interface UpdateClientRequest extends CreateClientRequest {
  membershipEndDate?: string;
  paymentStatus?: 'Paid' | 'Pending' | 'Overdue';
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
  paymentMethod: 'Cash' | 'Card' | 'BankTransfer' | 'Other';
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

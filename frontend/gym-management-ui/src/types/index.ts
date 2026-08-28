// Enums matching backend
export enum Gender {
  Male = 0,
  Female = 1,
  Other = 2,
  PreferNotToSay = 3
}

// The API sends and accepts enum *names*, so these string unions - not the numeric enums
// below - are what actually travels on the wire. Keep them identical to the backend enums
// in GymManagement.Domain/Enums.
export type GenderString = 'Male' | 'Female' | 'Other' | 'PreferNotToSay';
export type MembershipStatusString =
  | 'Pending'
  | 'Active'
  | 'Expiring'
  | 'Expired'
  | 'Suspended';
export type PaymentStatusString = 'Paid' | 'Pending' | 'Overdue' | 'Partial';
export type PaymentMethodString = 'Cash' | 'Card' | 'BankTransfer' | 'Other' | 'Online';
export type TransactionStatusString = 'Completed' | 'Pending' | 'Failed' | 'Refunded';
export type CurrencyString = 'Usd' | 'Lbp';

/** Statuses that let a member through the door. Mirrors MembershipStatuses.AllowedIn. */
export const MEMBERSHIP_STATUSES_ALLOWED_IN: MembershipStatusString[] = ['Active', 'Expiring'];

export enum MembershipStatus {
  Pending = 0,
  Active = 1,
  Expiring = 2,
  Expired = 3,
  Suspended = 4
}

export enum PaymentStatus {
  Paid = 0,
  Pending = 1,
  Overdue = 2,
  Partial = 3
}

export enum PaymentMethod {
  Cash = 0,
  Card = 1,
  BankTransfer = 2,
  Other = 3,
  Online = 4
}

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
  Pending: MembershipStatus.Pending,
  Active: MembershipStatus.Active,
  Expiring: MembershipStatus.Expiring,
  Expired: MembershipStatus.Expired,
  Suspended: MembershipStatus.Suspended
};

export const MembershipStatusReverseMap: Record<MembershipStatus, MembershipStatusString> = {
  [MembershipStatus.Pending]: 'Pending',
  [MembershipStatus.Active]: 'Active',
  [MembershipStatus.Expiring]: 'Expiring',
  [MembershipStatus.Expired]: 'Expired',
  [MembershipStatus.Suspended]: 'Suspended'
};

export const PaymentStatusMap: Record<PaymentStatusString, PaymentStatus> = {
  Paid: PaymentStatus.Paid,
  Pending: PaymentStatus.Pending,
  Overdue: PaymentStatus.Overdue,
  Partial: PaymentStatus.Partial
};

export const PaymentMethodMap: Record<PaymentMethodString, PaymentMethod> = {
  Cash: PaymentMethod.Cash,
  Card: PaymentMethod.Card,
  BankTransfer: PaymentMethod.BankTransfer,
  Other: PaymentMethod.Other,
  Online: PaymentMethod.Online
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
  /** The USD figure. The only one any report adds up. Negative on a reversal. */
  amount: number;
  /** What the member physically handed over, in `currency`. */
  amountReceived: number;
  currency: CurrencyString;
  /** LBP per USD at the time of payment. Null when paid in USD. */
  exchangeRate?: number;
  paymentDate: string;
  paymentMethod: PaymentMethodString;
  status: TransactionStatusString;
  /** Null when the payment did not cover the price, and on reversal rows. */
  periodStartDate?: string;
  periodEndDate?: string;
  /** Set on a reversal row to the payment it cancels. */
  reversesPaymentId?: number;
  /** True when this payment was short of the package price. */
  isPartial: boolean;
  /** How much of the package price is still owed. Zero on a full payment. */
  amountOutstanding: number;
  transactionReference?: string;
  notes?: string;
  createdAt: string;
}

export interface PaymentListItem {
  id: number;
  clientName: string;
  packageName: string;
  amount: number;
  amountReceived: number;
  currency: CurrencyString;
  paymentDate: string;
  paymentMethod: PaymentMethodString;
  status: TransactionStatusString;
  isReversal: boolean;
}

/**
 * Note what is absent: neither the price nor the membership period is sent. The server
 * works both out from the package, so the browser cannot buy a year for a month's money.
 */
export interface CreatePaymentRequest {
  clientId: number;
  packageId: number;
  /** What the member handed over, in `currency`. */
  amountReceived: number;
  currency: CurrencyString;
  /** Required when currency is 'Lbp', ignored otherwise. */
  exchangeRate?: number;
  paymentMethod: PaymentMethodString;
  transactionReference?: string;
  notes?: string;
}

export interface ReversePaymentRequest {
  reason?: string;
}

export interface PaymentQueryParams {
  page?: number;
  pageSize?: number;
  clientId?: number;
  startDate?: string;
  endDate?: string;
  status?: TransactionStatusString;
  paymentMethod?: PaymentMethodString;
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

// Member import types (blueprint 6.3)
export type MemberImportRowStatus = 'Ready' | 'Duplicate' | 'Error';

export interface MemberImportRow {
  rowNumber: number;
  rawName?: string;
  rawPhone?: string;
  rawPackage?: string;
  rawEndDate?: string;
  firstName?: string;
  lastName?: string;
  phoneNumber?: string;
  email?: string;
  packageId?: number;
  packageName?: string;
  membershipStartDate?: string;
  membershipEndDate?: string;
  startDateWasDerived: boolean;
  membershipStatus?: string;
  status: MemberImportRowStatus;
  problems: string[];
}

export interface MemberImportPreview {
  fileName: string;
  fileHash: string;
  totalRows: number;
  readyCount: number;
  duplicateCount: number;
  errorCount: number;
  availablePackages: string[];
  rows: MemberImportRow[];
}

export interface MemberImportResult {
  importedCount: number;
  skippedCount: number;
  skippedRows: MemberImportRow[];
}

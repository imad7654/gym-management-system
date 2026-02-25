// API Configuration
export const API_CONFIG = {
  BASE_URL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5001/api/v1',
  TIMEOUT: 30000, // 30 seconds
} as const;

// Pagination Configuration
export const PAGINATION_CONFIG = {
  DEFAULT_PAGE_SIZE: 10,
  PAGE_SIZE_OPTIONS: [5, 10, 25, 50, 100],
  INITIAL_PAGE: 1,
} as const;

// Table Configuration
export const TABLE_CONFIG = {
  ROW_HEIGHT: 52,
  HEADER_HEIGHT: 56,
  MAX_ROWS_PER_PAGE: 100,
} as const;

// Form Validation
export const VALIDATION_CONFIG = {
  MIN_PASSWORD_LENGTH: 6,
  MAX_NAME_LENGTH: 100,
  MAX_EMAIL_LENGTH: 255,
  MAX_PHONE_LENGTH: 20,
  MAX_ADDRESS_LENGTH: 500,
  MAX_NOTES_LENGTH: 1000,
} as const;

// Date Configuration
export const DATE_CONFIG = {
  DEFAULT_FORMAT: 'MMM dd, yyyy',
  DATETIME_FORMAT: 'MMM dd, yyyy HH:mm',
  ISO_FORMAT: 'yyyy-MM-dd',
} as const;

// Query Configuration
export const QUERY_CONFIG = {
  STALE_TIME: 5 * 60 * 1000, // 5 minutes
  CACHE_TIME: 10 * 60 * 1000, // 10 minutes
  RETRY_COUNT: 3,
  RETRY_DELAY: 1000, // 1 second
} as const;

// Token Configuration
export const TOKEN_CONFIG = {
  ACCESS_TOKEN_KEY: 'accessToken',
  REFRESH_TOKEN_KEY: 'refreshToken',
  USER_KEY: 'user',
} as const;

// Status Colors
export const STATUS_COLORS = {
  ACTIVE: 'success',
  EXPIRED: 'error',
  PENDING: 'warning',
  CANCELLED: 'default',
  SUSPENDED: 'default',
  PAID: 'success',
  OVERDUE: 'error',
  PARTIALLY_PAID: 'warning',
} as const;

// Membership Status Display Names
export const MEMBERSHIP_STATUS_LABELS = {
  Active: 'Active',
  Expired: 'Expired',
  Pending: 'Pending',
  Cancelled: 'Cancelled',
  Suspended: 'Suspended',
} as const;

// Payment Status Display Names
export const PAYMENT_STATUS_LABELS = {
  Paid: 'Paid',
  Pending: 'Pending',
  Overdue: 'Overdue',
  PartiallyPaid: 'Partially Paid',
} as const;

// Gender Display Names
export const GENDER_LABELS = {
  Male: 'Male',
  Female: 'Female',
  Other: 'Other',
  PreferNotToSay: 'Prefer not to say',
} as const;

// Dashboard Configuration
export const DASHBOARD_CONFIG = {
  RECENT_CLIENTS_COUNT: 5,
  RECENT_PAYMENTS_COUNT: 5,
  EXPIRING_MEMBERSHIPS_DAYS: 30,
  CHART_MONTHS_COUNT: 6,
} as const;

// File Upload Configuration
export const FILE_UPLOAD_CONFIG = {
  MAX_FILE_SIZE: 5 * 1024 * 1024, // 5MB
  ALLOWED_IMAGE_TYPES: ['image/jpeg', 'image/png', 'image/webp'],
  ALLOWED_DOCUMENT_TYPES: ['application/pdf', 'application/msword'],
} as const;

// UI Configuration
export const UI_CONFIG = {
  DEBOUNCE_DELAY: 300, // milliseconds for search debounce
  TOAST_DURATION: 3000, // milliseconds for notification display
  MODAL_ANIMATION_DURATION: 200, // milliseconds
} as const;

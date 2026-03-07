// ─── API Response wrapper (matches backend ApiResponse<T>) ───
export interface ApiResponse<T> {
  success: boolean;
  message: string | null;
  data: T;
  error: ApiError | null;
  correlationId: string | null;
}

export interface ApiError {
  statusCode: number;
  details: string | null;
}

// ─── Auth ───
export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAtUtc: string;
}

export interface AuthResult {
  userId: number;
  companyId: number;
  role: string;
  tokens: AuthTokens;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  companyName: string;
  companyCode: string;
  companyEmail: string;
  adminFullName: string;
  adminEmail: string;
  password: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

// ─── Company ───
export interface Company {
  companyId: number;
  companyName: string;
  email: string;
  phone: string | null;
  status: string | null;
  createdAt: string | null;
  trialStartDate: string | null;
  trialEndDate: string | null;
  subscriptionEndDate: string | null;
}

// ─── User ───
export interface CompanyUser {
  companyUserId: number;
  companyId: number;
  fullName: string;
  email: string;
  role: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface UserUpsertRequest {
  fullName: string;
  email: string;
  password?: string;
  role: string;
  isActive: boolean;
}

// ─── Subscription ───
export interface SubscriptionPlan {
  subscriptionPlanId: number;
  name: string;
  code: string;
  trialDays: number;
  maxMessagesPerMonth: number;
  maxWhatsAppAccounts: number;
  monthlyPrice: number;
  isActive: boolean;
  createdAtUtc: string;
}

export interface CompanySubscription {
  companySubscriptionId: number;
  companyId: number;
  subscriptionPlanId: number;
  status: string;
  trialStartDate: string | null;
  trialEndDate: string | null;
  startDate: string | null;
  endDate: string | null;
  isActive: boolean;
  createdAtUtc: string;
  subscriptionPlan?: SubscriptionPlan | null;
}

// ─── WhatsApp Account ───
export interface MetaBusinessAccount {
  metaBusinessAccountId: number;
  companyId: number;
  businessId: string;
  name: string;
  accessToken: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

export interface WhatsAppAccount {
  whatsAppAccountId: string;
  companyId: number;
  metaBusinessAccountId: number | null;
  businessAccountId: string;
  name: string;
  accessToken: string;
  verifyToken: string;
  appSecret: string | null;
  isDefault: boolean;
  isActive: boolean;
  createdAtUtc: string;
}

export interface WhatsAppAccountUpsertRequest {
  metaBusinessAccountId?: number | null;
  businessAccountId: string;
  name: string;
  accessToken: string;
  verifyToken: string;
  appSecret?: string | null;
  isDefault: boolean;
  isActive: boolean;
}

// ─── Phone Number ───
export interface WhatsAppPhoneNumber {
  whatsAppPhoneNumberId: number;
  companyId: number;
  whatsAppAccountId: string;
  phoneNumberId: string;
  displayPhoneNumber: string;
  verifiedName: string | null;
  isDefault: boolean;
  isActive: boolean;
  createdAtUtc: string;
}

export interface PhoneNumberUpsertRequest {
  whatsAppAccountId?: string | null;
  businessAccountId?: string | null;
  phoneNumberId: string;
  displayPhoneNumber: string;
  verifiedName?: string | null;
  isDefault: boolean;
  isActive: boolean;
}

// ─── Message ───
export interface Message {
  messageId: number;
  companyId: number;
  whatsAppPhoneNumberId: number | null;
  toNumber: string;
  messageType: string;
  messageBody: string;
  status: string;
  externalMessageId: string | null;
  failureReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

// ─── API Log ───
export interface ApiLog {
  apiLogId: number;
  companyId: number | null;
  companyUserId: number | null;
  endpoint: string;
  httpMethod: string;
  requestBody: string | null;
  responseBody: string | null;
  statusCode: number;
  ipAddress: string | null;
  createdAtUtc: string;
}

// ─── Dashboard Stats ───
export interface DashboardStats {
  totalMessagesSent: number;
  totalMessagesThisMonth: number;
  messageSuccessRate: number;
  activePhoneNumbers: number;
  activeWhatsAppAccounts: number;
  subscriptionStatus: string;
  subscriptionPlan: string;
  trialDaysRemaining: number | null;
  maxMessagesPerMonth: number;
}

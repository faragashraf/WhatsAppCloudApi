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

// ─── User Permissions (matches backend UserPermissions model) ───
export interface UserPermissions {
  contactsView: boolean;
  contactsCreate: boolean;
  contactsEdit: boolean;
  contactsDelete: boolean;
  contactsImport: boolean;
  campaignsView: boolean;
  campaignsCreate: boolean;
  campaignsEdit: boolean;
  campaignsLaunch: boolean;
  automationView: boolean;
  automationCreate: boolean;
  automationEdit: boolean;
  automationDelete: boolean;
  conversationsView: boolean;
  conversationsSend: boolean;
  conversationsAttach: boolean;
  conversationsAssign: boolean;
  templatesView: boolean;
  templatesCreate: boolean;
  templatesEdit: boolean;
  templatesDelete: boolean;
  messagesView: boolean;
}

export const DEFAULT_PERMISSIONS: UserPermissions = {
  contactsView: true, contactsCreate: true, contactsEdit: true, contactsDelete: false, contactsImport: false,
  campaignsView: true, campaignsCreate: false, campaignsEdit: false, campaignsLaunch: false,
  automationView: true, automationCreate: false, automationEdit: false, automationDelete: false,
  conversationsView: true, conversationsSend: true, conversationsAttach: true, conversationsAssign: false,
  templatesView: true, templatesCreate: false, templatesEdit: false, templatesDelete: false,
  messagesView: true,
};

export const FULL_PERMISSIONS: UserPermissions = {
  contactsView: true, contactsCreate: true, contactsEdit: true, contactsDelete: true, contactsImport: true,
  campaignsView: true, campaignsCreate: true, campaignsEdit: true, campaignsLaunch: true,
  automationView: true, automationCreate: true, automationEdit: true, automationDelete: true,
  conversationsView: true, conversationsSend: true, conversationsAttach: true, conversationsAssign: true,
  templatesView: true, templatesCreate: true, templatesEdit: true, templatesDelete: true,
  messagesView: true,
};

export interface AuthResult {
  userId: number;
  companyId: number;
  fullName: string;
  companyName: string;
  role: string;
  isSuperAdmin: boolean;
  permissions: UserPermissions;
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
  permissionsJson: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface UserUpsertRequest {
  fullName: string;
  email: string;
  password?: string;
  role: string;
  isActive: boolean;
  permissions?: UserPermissions;
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

// ─── Paged Result ───
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNext: boolean;
  hasPrevious: boolean;
}

// ─── Contact ───
export interface Contact {
  contactId: number;
  companyId: number;
  name: string;
  phoneNumber: string;
  email: string | null;
  tags: string | null;
  customFields: string | null;
  source: string | null;
  notes: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface ContactUpsertRequest {
  name: string;
  phoneNumber: string;
  email?: string;
  tags?: string;
  customFields?: string;
  source?: string;
  notes?: string;
}

// ─── Conversation ───
export interface Conversation {
  conversationId: number;
  companyId: number;
  whatsAppPhoneNumberId: number | null;
  contactId: number | null;
  contactNumber: string;
  contactName: string | null;
  lastMessageContent: string | null;
  lastMessageType: string | null;
  lastMessageAtUtc: string | null;
  lastInboundMessageAtUtc: string | null;
  status: string;
  unreadCount: number;
  assignedUserId: number | null;
  createdAtUtc: string;
  whatsAppPhoneNumber?: {
    whatsAppPhoneNumberId: number;
    displayPhoneNumber: string;
    verifiedName: string | null;
  } | null;
}

export interface ConversationMessage {
  conversationMessageId: number;
  conversationId: number;
  companyId: number;
  direction: 'inbound' | 'outbound';
  metaMessageId: string | null;
  messageType: string;
  content: string;
  mediaUrl: string | null;
  mediaMimeType: string | null;
  status: string;
  failureReason: string | null;
  timestampUtc: string;
}

export interface SendMessageRequest {
  messageType: string;
  content: string;
  mediaUrl?: string;
  mediaMimeType?: string;
  fileName?: string;
  templateName?: string;
  languageCode?: string;
}

// ─── Campaign ───
export interface Campaign {
  campaignId: number;
  companyId: number;
  name: string;
  description: string | null;
  templateName: string;
  languageCode: string;
  templateParametersJson: string | null;
  whatsAppPhoneNumberId: number | null;
  status: string;
  scheduledAtUtc: string | null;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  totalContacts: number;
  sentCount: number;
  deliveredCount: number;
  readCount: number;
  failedCount: number;
  createdAtUtc: string;
}

export interface CampaignCreateRequest {
  name: string;
  description?: string;
  templateName: string;
  languageCode: string;
  templateParametersJson?: string;
  whatsAppPhoneNumberId?: number;
  scheduledAtUtc?: string;
  phoneNumbers?: string[];
  contactIds?: number[];
}

export interface CampaignContact {
  campaignContactId: number;
  campaignId: number;
  contactId: number | null;
  phoneNumber: string;
  status: string;
  externalMessageId: string | null;
  failureReason: string | null;
  sentAtUtc: string | null;
  deliveredAtUtc: string | null;
  readAtUtc: string | null;
}

// ─── Automation Rule ───
export interface AutomationRule {
  automationRuleId: number;
  companyId: number;
  name: string;
  description: string | null;
  triggerType: string;
  triggerValue: string;
  responseType: string;
  responseValue: string;
  templateName: string | null;
  languageCode: string | null;
  priority: number;
  isActive: boolean;
  triggerCount: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface AutomationRuleUpsertRequest {
  name: string;
  description?: string;
  triggerType: string;
  triggerValue: string;
  responseType: string;
  responseValue: string;
  templateName?: string;
  languageCode?: string;
  priority: number;
  isActive?: boolean;
}

// ─── Notification ───
export interface Notification {
  notificationId: number;
  companyId: number;
  companyUserId: number | null;
  type: string;
  title: string;
  body: string;
  category: string | null;
  metadataJson: string | null;
  isRead: boolean;
  createdAtUtc: string;
  readAtUtc: string | null;
}

// ─── Developer Info ───
export interface DeveloperInfo {
  baseUrl: string;
  version: string;
  authType: string;
  companyId: number;
  endpoints: { method: string; path: string; description: string; category?: string; requestBody?: string | null }[];
  codeSamples: { language: string; code: string }[];
}

// ─── Connect Meta (WhatsApp Business Account connection) ───
export interface ConnectMetaRequest {
  businessAccountId: string;
  accessToken: string;
}

export interface MetaPhoneNumberInfo {
  phoneNumberId: string;
  displayPhoneNumber: string;
  verifiedName: string | null;
  codeVerificationStatus: string | null;
  qualityRating: string | null;
  platformType: string | null;
  throughputLevel: string | null;
  lastOnboardedTime: string | null;
}

export interface ConnectMetaResponse {
  status: string;
  businessAccountName: string | null;
  businessAccountId: string | null;
  phoneNumbersImported: number;
  webhookConfigured: boolean;
  webhookUrl: string | null;
  lastSyncUtc: string | null;
  errorMessage: string | null;
  phoneNumbers: MetaPhoneNumberInfo[];
}

export interface WhatsAppConnectionStatus {
  isConnected: boolean;
  businessAccountId: string | null;
  businessAccountName: string | null;
  connectionStatus: string;
  phoneNumberCount: number;
  lastSyncUtc: string | null;
  tokenValid: boolean;
  webhookUrl: string | null;
  phoneNumbers: MetaPhoneNumberInfo[];
}

export interface PhoneNumberSyncRequest {
  businessAccountId: string;
  phoneNumbers: {
    phoneNumberId: string;
    displayPhoneNumber: string;
    verifiedName?: string;
  }[];
}

export interface PhoneNumberSyncResponse {
  created: number;
  updated: number;
  total: number;
}

// ─── Activity Event (Phase 12 + 13) ───
export type ActivitySeverity = 'info' | 'success' | 'warning' | 'error';

export interface ActivityEvent {
  id: string;
  type: string;
  message: string;
  timestamp: Date;
  severity: ActivitySeverity;
  metadata?: Record<string, unknown>;
}

export const ACTIVITY_TYPES = {
  // Messaging
  MESSAGE_SENT: 'message_sent',
  MESSAGE_DELIVERED: 'message_delivered',
  MESSAGE_READ: 'message_read',
  MESSAGE_FAILED: 'message_failed',
  // Webhook / Incoming
  WEBHOOK_RECEIVED: 'webhook_received',
  WEBHOOK_VALIDATION: 'webhook_validation',
  INCOMING_MESSAGE: 'incoming_message',
  // System
  NUMBER_CONNECTED: 'number_connected',
  NUMBER_DISCONNECTED: 'number_disconnected',
  API_KEY_CREATED: 'api_key_created',
  SUBSCRIPTION_UPDATED: 'subscription_updated',
} as const;

export type ActivityType = (typeof ACTIVITY_TYPES)[keyof typeof ACTIVITY_TYPES];

// ─── Connection Status for real-time stream ───
export type StreamConnectionStatus = 'connected' | 'reconnecting' | 'disconnected';

// ─── WhatsApp Template ───
export interface WhatsAppTemplate {
  id: string;
  name: string;
  language: string;
  category: string;
  status: string;
  components: WhatsAppTemplateComponent[];
  libraryTemplateName?: string;
}

export interface WhatsAppTemplateComponent {
  type: string; // HEADER | BODY | FOOTER | BUTTONS
  format?: string;
  text?: string;
  buttons?: WhatsAppTemplateButton[];
  example?: { header_text?: string[]; body_text?: string[][]; header_handle?: string[] };
}

export interface WhatsAppTemplateButton {
  type: string;
  text: string;
  url?: string;
  phoneNumber?: string;
}

export interface CreateTemplateRequest {
  name: string;
  language: string;
  category: string;
  components: WhatsAppTemplateComponent[];
}

// ─── Webhook Log Entry (matches backend GET /api/webhook/logs) ───
export interface WebhookLogEntry {
  id: string;
  timestamp: string;
  payload: string | null;
  summary: string | null;
}

// ─── Forgot Password / OTP ───
export interface ForgotPasswordRequest {
  email: string;
}

export interface VerifyOtpRequest {
  email: string;
  otp: string;
}

export interface ResetPasswordRequest {
  email: string;
  otp: string;
  newPassword: string;
}

// ─── Super Admin ───
export interface SuperAdminCompany {
  companyId: number;
  companyName: string;
  email: string;
  phone: string | null;
  status: string | null;
  isDeleted: boolean;
  suspendedAtUtc: string | null;
  deletedAtUtc: string | null;
  createdAt: string | null;
  trialStartDate: string | null;
  trialEndDate: string | null;
  subscriptionEndDate: string | null;
  userCount: number;
  activeSubscription: {
    companySubscriptionId: number;
    status: string;
    name: string;
    trialEndDate: string | null;
    endDate: string | null;
  } | null;
}

export interface SuperAdminCompanyDetail extends SuperAdminCompany {
  users: { companyUserId: number; fullName: string; email: string; role: string; isActive: boolean }[];
  subscriptions: {
    companySubscriptionId: number;
    subscriptionPlanId: number;
    planName: string | null;
    status: string;
    trialStartDate: string | null;
    trialEndDate: string | null;
    startDate: string | null;
    endDate: string | null;
    isActive: boolean;
  }[];
}

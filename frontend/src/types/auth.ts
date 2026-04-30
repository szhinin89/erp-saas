export interface LoginRequest {
  email: string;
  password: string;
  tenantId: string;
}

export interface AuthResponse {
  userId: string;
  fullName: string;
  email: string;
  role: string;
  tenantId: string;
  token: string;
}

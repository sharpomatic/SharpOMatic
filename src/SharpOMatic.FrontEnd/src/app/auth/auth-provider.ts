export type SharpOMaticAuthAccessState =
  | 'authorized'
  | 'unauthorized'
  | 'forbidden';

export interface SharpOMaticAuthMessages {
  unauthorizedTitle?: string;
  unauthorizedBody?: string;
  forbiddenTitle?: string;
  forbiddenBody?: string;
}

export interface SharpOMaticAuthProvider {
  required?: boolean;
  getBearerToken?: () =>
    | Promise<string | null | undefined>
    | string
    | null
    | undefined;
  getAccessState?: () =>
    | Promise<SharpOMaticAuthAccessState>
    | SharpOMaticAuthAccessState;
  onUnauthorized?: () => void | Promise<void>;
  messages?: SharpOMaticAuthMessages;
}

declare global {
  interface Window {
    sharpomaticAuth?: SharpOMaticAuthProvider;
  }
}

export {};

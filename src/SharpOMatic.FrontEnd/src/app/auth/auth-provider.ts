export type SharpOMaticAuthAccessState =
  | 'authorized'
  | 'unauthorized'
  | 'forbidden';

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
}

declare global {
  interface Window {
    sharpomaticAuth?: SharpOMaticAuthProvider;
  }
}

export {};

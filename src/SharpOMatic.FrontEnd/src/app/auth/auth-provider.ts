export interface SharpOMaticAuthProvider {
  required?: boolean;
  getBearerToken?: () =>
    | Promise<string | null | undefined>
    | string
    | null
    | undefined;
  onUnauthorized?: () => void | Promise<void>;
}

declare global {
  interface Window {
    sharpomaticAuth?: SharpOMaticAuthProvider;
  }
}

export {};

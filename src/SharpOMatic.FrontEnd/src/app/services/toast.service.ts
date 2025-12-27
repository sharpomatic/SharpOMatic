import { Injectable, signal } from '@angular/core';

type ToastType = 'success' | 'error';

export interface ToastOptions {
  durationMs?: number;
}

export interface ToastMessage {
  id: number;
  type: ToastType;
  message: string;
  durationMs: number;
}

@Injectable({
  providedIn: 'root',
})
export class ToastService {
  private readonly defaultDuration = 4000;
  private idCounter = 0;
  readonly toasts = signal<ToastMessage[]>([]);

  success(message: string, options?: ToastOptions): void {
    this.addToast('success', message, options);
  }

  error(message: string, options?: ToastOptions): void {
    this.addToast('error', message, options);
  }

  extractErrorDetail(error: unknown): string {
    const maybeError = error as { error?: unknown; message?: unknown; statusText?: unknown };
    const nested = maybeError?.error as { message?: unknown };
    const parts = [
      nested?.message,
      typeof maybeError?.error === 'string' ? maybeError.error : undefined,
      maybeError?.message,
      maybeError?.statusText,
    ];

    return parts
      .map((part) => (typeof part === 'string' ? part.trim() : ''))
      .find((part) => part.length > 0) ?? '';
  }

  dismiss(id: number): void {
    this.toasts.update((current) => current.filter((toast) => toast.id !== id));
  }

  private addToast(type: ToastType, message: string, options?: ToastOptions): void {
    const durationMs = options?.durationMs ?? this.defaultDuration;
    const toast: ToastMessage = {
      id: ++this.idCounter,
      type,
      message,
      durationMs,
    };

    this.toasts.update((current) => [...current, toast]);

    if (durationMs > 0) {
      window.setTimeout(() => this.dismiss(toast.id), durationMs);
    }
  }
}

import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
  importProvidersFrom,
  APP_INITIALIZER,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { routes } from './app.routes';
import { API_URL } from './app.tokens';
import { provideAnimations } from '@angular/platform-browser/animations';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { MonacoService } from '../../services/monaco.service';
import { ModalModule } from 'ngx-bootstrap/modal';

export function initializeMonacoService(monacoGlobalService: MonacoService) {
  return () => {
    monacoGlobalService.init();
  };
}

export function resolveApiUrl(): string {
  const baseUri = new URL(document.baseURI);
  const normalizedPath = baseUri.pathname.replace(/\/$/, '');
  if (normalizedPath.length > 0 && normalizedPath !== '/') {
    return `${baseUri.origin}${normalizedPath}`;
  }

  return 'http://localhost:9001';
}

export const appConfig: ApplicationConfig = {
  providers: [
    { provide: API_URL, useFactory: resolveApiUrl },
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideAnimations(),
    importProvidersFrom(MonacoEditorModule.forRoot()),
    importProvidersFrom(
      MonacoEditorModule.forRoot({
        onMonacoLoad: () => {
          (window as any).monacoServiceInitializer();
        },
      }),
    ),
    importProvidersFrom(ModalModule.forRoot()),
    {
      provide: APP_INITIALIZER,
      useFactory: (monacoService: MonacoService) => {
        return () => {
          (window as any).monacoServiceInitializer =
            initializeMonacoService(monacoService);
        };
      },
      deps: [MonacoService],
      multi: true,
    },
  ],
};

import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { catchError, Observable, of } from 'rxjs';
import { CodeCheckResult  } from '../data-transfer-objects/code-check-result';
import { ToastService } from './toast.service';
import { SettingsService } from './settings.service';

@Injectable({
  providedIn: 'root',
})
export class ServerMonacoService {
  private readonly http = inject(HttpClient);
  private readonly toastService = inject(ToastService);
  private readonly settingsService = inject(SettingsService);

  public codeCheck(code: string): Observable<CodeCheckResult[] | null> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.post<CodeCheckResult[]>(`${apiUrl}/api/monaco/codecheck`, { code: code }).pipe(
      catchError((error) => {
        const message = this.toastService.extractErrorDetail(error);
        this.toastService.error(message ? `Code check failed: ${message}` : 'Code check failed.');
        return of(null);
      })
    );
  }
}

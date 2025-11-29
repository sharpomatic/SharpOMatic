import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs';
import { API_URL } from '../components/app/app.tokens';
import { CodeCheckResult  } from '../interfaces/code-check-result';

@Injectable({
  providedIn: 'root',
})
export class ServerMonacoService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_URL);

  public codeCheck(code: string): Observable<CodeCheckResult[] | null> {
    return this.http.post<CodeCheckResult[]>(`${this.apiUrl}/api/monaco/codecheck`, { code: code }).pipe(
      catchError(() => of(null))
    );
  }
}

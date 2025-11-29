import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable, of } from 'rxjs';
import { catchError } from 'rxjs';
import { API_URL } from '../components/app/app.tokens';
import { WorkflowEntity, WorkflowSnapshot } from '../entities/definitions/workflow.entity';
import { WorkflowSummaryEntity, WorkflowSummarySnapshot } from '../entities/definitions/workflow.summary.entity';
import { RunProgressModel } from '../pages/workflow/interfaces/run-progress-model';
import { TraceProgressModel } from '../pages/workflow/interfaces/trace-progress-model';
import { ContextEntryListEntity } from '../entities/definitions/context-entry-list.entity';

@Injectable({
  providedIn: 'root',
})
export class ServerRepositoryService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_URL);

  public getWorkflows(): Observable<WorkflowSummaryEntity[]> {
    return this.http.get<WorkflowSummarySnapshot[]>(`${this.apiUrl}/api/workflow`).pipe(
      map(snapshots => snapshots.map(WorkflowSummaryEntity.fromSnapshot)),
      catchError(() => of([]))
    );
  }

  public getWorkflow(id: string): Observable<WorkflowEntity | null> {
    return this.http.get<WorkflowSnapshot>(`${this.apiUrl}/api/workflow/${id}`).pipe(
      map(snapshot => WorkflowEntity.fromSnapshot(snapshot)),
      catchError(() => of(null))
    );
  }

  public upsertWorkflow(workflow: WorkflowEntity): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/api/workflow`, workflow.toSnapshot()).pipe(
      catchError(() => of(undefined))
    );
  }

  public runWorkflow(id: string, entryList: ContextEntryListEntity): Observable<string | undefined>  {
    return this.http.post<string>(`${this.apiUrl}/api/workflow/run/${id}`, entryList.toSnapshot()).pipe(
      catchError(() => of(undefined))
    );
  }

  public deleteWorkflow(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/api/workflow/${id}`).pipe(
      catchError(() => of(undefined))
    );
  }

  public getLatestWorkflowRun(id: string): Observable<RunProgressModel | null> {
    return this.http.get<RunProgressModel>(`${this.apiUrl}/api/run/latestforworkflow/${id}`).pipe(
      catchError(() => of(null))
    );
  }

  public getRunTraces(id: string): Observable<TraceProgressModel[] | null> {
    return this.http.get<TraceProgressModel[]>(`${this.apiUrl}/api/trace/forrun/${id}`).pipe(
      catchError(() => of(null))
    );
  }
}

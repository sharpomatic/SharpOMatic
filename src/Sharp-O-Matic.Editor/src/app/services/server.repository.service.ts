import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { catchError, map, Observable, of } from 'rxjs';
import { Connection, ConnectionSnapshot } from '../metadata/definitions/connection';
import { WorkflowEntity, WorkflowSnapshot } from '../entities/definitions/workflow.entity';
import { WorkflowSummaryEntity, WorkflowSummarySnapshot } from '../entities/definitions/workflow.summary.entity';
import { RunProgressModel } from '../pages/workflow/interfaces/run-progress-model';
import { TraceProgressModel } from '../pages/workflow/interfaces/trace-progress-model';
import { ContextEntryListEntity } from '../entities/definitions/context-entry-list.entity';
import { ConnectionConfig, ConnectionConfigSnapshot } from '../metadata/definitions/connection-config';
import { ConnectionSummary, ConnectionSummarySnapshot } from '../metadata/definitions/connection summary';
import { ModelConfig, ModelConfigSnapshot } from '../metadata/definitions/model-config';
import { ModelSummary, ModelSummarySnapshot } from '../metadata/definitions/model-summary';
import { Model, ModelSnapshot } from '../metadata/definitions/model';
import { ToastService } from './toast.service';
import { SettingsService } from './settings.service';

@Injectable({
  providedIn: 'root',
})
export class ServerRepositoryService {
  private readonly http = inject(HttpClient);
  private readonly toastService = inject(ToastService);
  private readonly settingsService = inject(SettingsService);

  public getWorkflows(): Observable<WorkflowSummaryEntity[]> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.get<WorkflowSummarySnapshot[]>(`${apiUrl}/api/workflow`).pipe(
      map(snapshots => snapshots.map(WorkflowSummaryEntity.fromSnapshot)),
      catchError((error) => {
        this.notifyError('Loading workflows', error);
        return of([]);
      })
    );
  }

  public getWorkflow(id: string): Observable<WorkflowEntity | null> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.get<WorkflowSnapshot>(`${apiUrl}/api/workflow/${id}`).pipe(
      map(snapshot => WorkflowEntity.fromSnapshot(snapshot)),
      catchError((error) => {
        this.notifyError('Loading workflow', error);
        return of(null);
      })
    );
  }

  public upsertWorkflow(workflow: WorkflowEntity): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.post<void>(`${apiUrl}/api/workflow`, workflow.toSnapshot()).pipe(
      catchError((error) => {
        this.notifyError('Saving workflow', error);
        return of(undefined);
      })
    );
  }

  public runWorkflow(id: string, entryList: ContextEntryListEntity): Observable<string | undefined>  {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.post<string>(`${apiUrl}/api/workflow/run/${id}`, entryList.toSnapshot()).pipe(
      catchError((error) => {
        this.notifyError('Starting workflow run', error);
        return of(undefined);
      })
    );
  }

  public deleteWorkflow(id: string): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.delete<void>(`${apiUrl}/api/workflow/${id}`).pipe(
      catchError((error) => {
        this.notifyError('Deleting workflow', error);
        return of(undefined);
      })
    );
  }

  public getLatestWorkflowRun(id: string): Observable<RunProgressModel | null> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.get<RunProgressModel>(`${apiUrl}/api/run/latestforworkflow/${id}`).pipe(
      catchError((error) => {
        this.notifyError('Loading latest workflow run', error);
        return of(null);
      })
    );
  }

  public getRunTraces(id: string): Observable<TraceProgressModel[] | null> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.get<TraceProgressModel[]>(`${apiUrl}/api/trace/forrun/${id}`).pipe(
      catchError((error) => {
        this.notifyError('Loading run traces', error);
        return of(null);
      })
    );
  }

  public getConnectionConfigs(): Observable<ConnectionConfig[]> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.get<ConnectionConfigSnapshot[]>(`${apiUrl}/api/metadata/connection-configs`).pipe(
      map(snapshots => snapshots.map(ConnectionConfig.fromSnapshot)),
      catchError((error) => {
        this.notifyError('Loading connection configs', error);
        return of([]);
      })
    );
  }

  public getConnectionSummaries(): Observable<ConnectionSummary[]> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.get<ConnectionSummarySnapshot[]>(`${apiUrl}/api/metadata/connections`).pipe(
      map(snapshots => snapshots.map(ConnectionSummary.fromSnapshot)),
      catchError((error) => {
        this.notifyError('Loading connections', error);
        return of([]);
      })
    );
  }

  public getConnection(id: string): Observable<Connection | null> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.get<ConnectionSnapshot>(`${apiUrl}/api/metadata/connections/${id}`).pipe(
      map(Connection.fromSnapshot),
      catchError((error) => {
        this.notifyError('Loading connection', error);
        return of(null);
      })
    );
  }

  public upsertConnection(connection: Connection): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.post<void>(`${apiUrl}/api/metadata/connections`, connection.toSnapshot()).pipe(
      catchError((error) => {
        this.notifyError('Saving connection', error);
        return of(undefined);
      })
    );
  }

  public deleteConnection(id: string): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.delete<void>(`${apiUrl}/api/metadata/connections/${id}`).pipe(
      catchError((error) => {
        this.notifyError('Deleting connection', error);
        return of(undefined);
      })
    );
  }

  public getModelConfigs(): Observable<ModelConfig[]> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.get<ModelConfigSnapshot[]>(`${apiUrl}/api/metadata/model-configs`).pipe(
      map(snapshots => snapshots.map(ModelConfig.fromSnapshot)),
      catchError((error) => {
        this.notifyError('Loading model configs', error);
        return of([]);
      })
    );
  }

  public getModelSummaries(): Observable<ModelSummary[]> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.get<ModelSummarySnapshot[]>(`${apiUrl}/api/metadata/models`).pipe(
      map(snapshots => snapshots.map(ModelSummary.fromSnapshot)),
      catchError((error) => {
        this.notifyError('Loading models', error);
        return of([]);
      })
    );
  }

  public getModel(id: string): Observable<Model | null> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.get<ModelSnapshot>(`${apiUrl}/api/metadata/models/${id}`).pipe(
      map(Model.fromSnapshot),
      catchError((error) => {
        this.notifyError('Loading model', error);
        return of(null);
      })
    );
  }

  public upsertModel(model: Model): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.post<void>(`${apiUrl}/api/metadata/models`, model.toSnapshot()).pipe(
      catchError((error) => {
        this.notifyError('Saving model', error);
        return of(undefined);
      })
    );
  }

  public deleteModel(id: string): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.delete<void>(`${apiUrl}/api/metadata/models/${id}`).pipe(
      catchError((error) => {
        this.notifyError('Deleting model', error);
        return of(undefined);
      })
    );
  }

  private notifyError(operation: string, error: unknown): void {
    const detail = this.toastService.extractErrorDetail(error);
    const message = detail ? `${operation} failed: ${detail}` : `${operation} failed.`;
    this.toastService.error(message);
  }
}

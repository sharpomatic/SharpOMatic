import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { catchError, map, Observable, of } from 'rxjs';
import {
  Connector,
  ConnectorSnapshot,
} from '../metadata/definitions/connector';
import {
  WorkflowEntity,
  WorkflowSnapshot,
} from '../entities/definitions/workflow.entity';
import {
  WorkflowSummaryEntity,
  WorkflowSummarySnapshot,
} from '../entities/definitions/workflow.summary.entity';
import { RunProgressModel } from '../pages/workflow/interfaces/run-progress-model';
import { WorkflowRunPageResult } from '../pages/workflow/interfaces/workflow-run-page-result';
import { TraceProgressModel } from '../pages/workflow/interfaces/trace-progress-model';
import { Setting } from '../pages/settings/interfaces/setting';
import { ContextEntryListEntity } from '../entities/definitions/context-entry-list.entity';
import {
  ConnectorConfig,
  ConnectorConfigSnapshot,
} from '../metadata/definitions/connector-config';
import {
  ConnectorSummary,
  ConnectorSummarySnapshot,
} from '../metadata/definitions/connector-summary';
import {
  ModelConfig,
  ModelConfigSnapshot,
} from '../metadata/definitions/model-config';
import {
  ModelSummary,
  ModelSummarySnapshot,
} from '../metadata/definitions/model-summary';
import { Model, ModelSnapshot } from '../metadata/definitions/model';
import {
  EvalConfig,
  EvalConfigSnapshot,
} from '../eval/definitions/eval-config';
import { EvalConfigDetailSnapshot } from '../eval/definitions/eval-config-detail';
import {
  EvalConfigSummary,
  EvalConfigSummarySnapshot,
} from '../eval/definitions/eval-config-summary';
import { EvalGraderSnapshot } from '../eval/definitions/eval-grader';
import { EvalColumnSnapshot } from '../eval/definitions/eval-column';
import { EvalRowSnapshot } from '../eval/definitions/eval-row';
import { EvalDataSnapshot } from '../eval/definitions/eval-data';
import { ToastService } from './toast.service';
import { SettingsService } from './settings.service';
import { RunSortField } from '../enumerations/run-sort-field';
import { SortDirection } from '../enumerations/sort-direction';
import { AssetScope } from '../enumerations/asset-scope';
import { AssetSortField } from '../enumerations/asset-sort-field';
import { WorkflowSortField } from '../enumerations/workflow-sort-field';
import { ConnectorSortField } from '../enumerations/connector-sort-field';
import { ModelSortField } from '../enumerations/model-sort-field';
import { EvalConfigSortField } from '../eval/enumerations/eval-config-sort-field';
import { EvalRunSortField } from '../eval/enumerations/eval-run-sort-field';
import { EvalRunRowSortField } from '../eval/enumerations/eval-run-row-sort-field';
import { AssetSummary } from '../pages/assets/interfaces/asset-summary';
import { AssetText } from '../pages/assets/interfaces/asset-text';
import { TransferImportResult } from '../pages/transfer/interfaces/transfer-import-result';
import { TransferExportRequest } from '../pages/transfer/interfaces/transfer-export-request';
import { EvalRunSummarySnapshot } from '../eval/definitions/eval-run-summary';
import { EvalRunDetailSnapshot } from '../eval/definitions/eval-run-detail';
import { EvalRunRowDetailSnapshot } from '../eval/definitions/eval-run-row-detail';

@Injectable({
  providedIn: 'root',
})
export class ServerRepositoryService {
  private readonly http = inject(HttpClient);
  private readonly toastService = inject(ToastService);
  private readonly settingsService = inject(SettingsService);

  public getWorkflowSummaries(
    search = '',
    skip = 0,
    take = 0,
    sortBy: WorkflowSortField = WorkflowSortField.Name,
    sortDirection: SortDirection = SortDirection.Ascending,
  ): Observable<WorkflowSummaryEntity[]> {
    const apiUrl = this.settingsService.apiUrl();
    let params = new HttpParams()
      .set('skip', skip)
      .set('take', take)
      .set('sortBy', sortBy)
      .set('sortDirection', sortDirection);
    if (search.trim().length > 0) {
      params = params.set('search', search.trim());
    }

    return this.http
      .get<WorkflowSummarySnapshot[]>(`${apiUrl}/api/workflow`, { params })
      .pipe(
        map((snapshots) => snapshots.map(WorkflowSummaryEntity.fromSnapshot)),
        catchError((error) => {
          this.notifyError('Loading workflows', error);
          return of([]);
        }),
      );
  }

  public getWorkflowCount(search = ''): Observable<number> {
    const apiUrl = this.settingsService.apiUrl();
    let params = new HttpParams();
    if (search.trim().length > 0) {
      params = params.set('search', search.trim());
    }

    return this.http
      .get<number>(`${apiUrl}/api/workflow/count`, { params })
      .pipe(
        catchError((error) => {
          this.notifyError('Loading workflow count', error);
          return of(0);
        }),
      );
  }

  public getWorkflow(id: string): Observable<WorkflowEntity | null> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.get<WorkflowSnapshot>(`${apiUrl}/api/workflow/${id}`).pipe(
      map((snapshot) => WorkflowEntity.fromSnapshot(snapshot)),
      catchError((error) => {
        this.notifyError('Loading workflow', error);
        return of(null);
      }),
    );
  }

  public upsertWorkflow(workflow: WorkflowEntity): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .post<void>(`${apiUrl}/api/workflow`, workflow.toSnapshot())
      .pipe(
        catchError((error) => {
          this.notifyError('Saving workflow', error);
          return of(undefined);
        }),
      );
  }

  public runWorkflow(
    id: string,
    entryList: ContextEntryListEntity,
  ): Observable<string | undefined> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .post<string>(`${apiUrl}/api/workflow/run/${id}`, entryList.toSnapshot())
      .pipe(
        catchError((error) => {
          this.notifyError('Starting workflow run', error);
          return of(undefined);
        }),
      );
  }

  public deleteWorkflow(id: string): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.delete<void>(`${apiUrl}/api/workflow/${id}`).pipe(
      catchError((error) => {
        this.notifyError('Deleting workflow', error);
        return of(undefined);
      }),
    );
  }

  public copyWorkflow(id: string): Observable<string | undefined> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .post<string>(`${apiUrl}/api/workflow/copy/${id}`, null)
      .pipe(
        catchError((error) => {
          this.notifyError('Copying workflow', error);
          return of(undefined);
        }),
      );
  }

  public getSampleWorkflowNames(): Observable<string[]> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.get<string[]>(`${apiUrl}/api/samples`).pipe(
      catchError((error) => {
        this.notifyError('Loading workflow samples', error);
        return of([]);
      }),
    );
  }

  public createWorkflowFromSample(
    sampleName: string,
  ): Observable<string | undefined> {
    const apiUrl = this.settingsService.apiUrl();
    const encodedSampleName = encodeURIComponent(sampleName);
    return this.http
      .post<string>(`${apiUrl}/api/samples/${encodedSampleName}`, null)
      .pipe(
        catchError((error) => {
          this.notifyError('Creating workflow from sample', error);
          return of(undefined);
        }),
      );
  }

  public getLatestWorkflowRun(id: string): Observable<RunProgressModel | null> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .get<RunProgressModel>(`${apiUrl}/api/run/latestforworkflow/${id}`)
      .pipe(
        catchError((error: any) => {
          this.notifyError('Loading latest workflow run', error);
          return of(null);
        }),
      );
  }

  public getLatestWorkflowRuns(
    id: string,
    page: number,
    count: number,
    sortBy: RunSortField,
    sortDirection: SortDirection,
  ): Observable<WorkflowRunPageResult | null> {
    const apiUrl = this.settingsService.apiUrl();
    const params = new HttpParams()
      .set('sortBy', sortBy)
      .set('sortDirection', sortDirection);
    return this.http
      .get<WorkflowRunPageResult>(
        `${apiUrl}/api/run/latestforworkflow/${id}/${page}/${count}`,
        { params },
      )
      .pipe(
        catchError((error) => {
          this.notifyError('Loading latest workflow runs', error);
          return of(null);
        }),
      );
  }

  public getRunTraces(id: string): Observable<TraceProgressModel[] | null> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .get<TraceProgressModel[]>(`${apiUrl}/api/trace/forrun/${id}`)
      .pipe(
        catchError((error) => {
          this.notifyError('Loading run traces', error);
          return of(null);
        }),
      );
  }

  public getSettings(): Observable<Setting[]> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.get<Setting[]>(`${apiUrl}/api/setting`).pipe(
      catchError((error) => {
        this.notifyError('Loading settings', error);
        return of([]);
      }),
    );
  }

  public upsertSetting(setting: Setting): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.post<void>(`${apiUrl}/api/setting`, setting).pipe(
      catchError((error) => {
        this.notifyError('Saving setting', error);
        return of(undefined);
      }),
    );
  }

  public getConnectorConfigs(): Observable<ConnectorConfig[]> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .get<
        ConnectorConfigSnapshot[]
      >(`${apiUrl}/api/metadata/connector-configs`)
      .pipe(
        map((snapshots) => snapshots.map(ConnectorConfig.fromSnapshot)),
        catchError((error) => {
          this.notifyError('Loading connector configs', error);
          return of([]);
        }),
      );
  }

  public getConnectorSummaries(
    search = '',
    skip = 0,
    take = 0,
    sortBy: ConnectorSortField = ConnectorSortField.Name,
    sortDirection: SortDirection = SortDirection.Ascending,
  ): Observable<ConnectorSummary[]> {
    const apiUrl = this.settingsService.apiUrl();
    let params = new HttpParams()
      .set('skip', skip)
      .set('take', take)
      .set('sortBy', sortBy)
      .set('sortDirection', sortDirection);
    if (search.trim().length > 0) {
      params = params.set('search', search.trim());
    }

    return this.http
      .get<
        ConnectorSummarySnapshot[]
      >(`${apiUrl}/api/metadata/connectors`, { params })
      .pipe(
        map((snapshots) => snapshots.map(ConnectorSummary.fromSnapshot)),
        catchError((error) => {
          this.notifyError('Loading connectors', error);
          return of([]);
        }),
      );
  }

  public getConnectorCount(search = ''): Observable<number> {
    const apiUrl = this.settingsService.apiUrl();
    let params = new HttpParams();
    if (search.trim().length > 0) {
      params = params.set('search', search.trim());
    }

    return this.http
      .get<number>(`${apiUrl}/api/metadata/connectors/count`, { params })
      .pipe(
        catchError((error) => {
          this.notifyError('Loading connector count', error);
          return of(0);
        }),
      );
  }

  public getConnector(id: string): Observable<Connector | null> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .get<ConnectorSnapshot>(`${apiUrl}/api/metadata/connectors/${id}`)
      .pipe(
        map(Connector.fromSnapshot),
        catchError((error) => {
          this.notifyError('Loading connector', error);
          return of(null);
        }),
      );
  }

  public upsertConnector(connector: Connector): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .post<void>(`${apiUrl}/api/metadata/connectors`, connector.toSnapshot())
      .pipe(
        catchError((error) => {
          this.notifyError('Saving connector', error);
          return of(undefined);
        }),
      );
  }

  public deleteConnector(id: string): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .delete<void>(`${apiUrl}/api/metadata/connectors/${id}`)
      .pipe(
        catchError((error) => {
          this.notifyError('Deleting connector', error);
          return of(undefined);
        }),
      );
  }

  public getModelConfigs(): Observable<ModelConfig[]> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .get<ModelConfigSnapshot[]>(`${apiUrl}/api/metadata/model-configs`)
      .pipe(
        map((snapshots) => snapshots.map(ModelConfig.fromSnapshot)),
        catchError((error) => {
          this.notifyError('Loading model configs', error);
          return of([]);
        }),
      );
  }

  public getModelSummaries(
    search = '',
    skip = 0,
    take = 0,
    sortBy: ModelSortField = ModelSortField.Name,
    sortDirection: SortDirection = SortDirection.Ascending,
  ): Observable<ModelSummary[]> {
    const apiUrl = this.settingsService.apiUrl();
    let params = new HttpParams()
      .set('skip', skip)
      .set('take', take)
      .set('sortBy', sortBy)
      .set('sortDirection', sortDirection);
    if (search.trim().length > 0) {
      params = params.set('search', search.trim());
    }

    return this.http
      .get<ModelSummarySnapshot[]>(`${apiUrl}/api/metadata/models`, { params })
      .pipe(
        map((snapshots) => snapshots.map(ModelSummary.fromSnapshot)),
        catchError((error) => {
          this.notifyError('Loading models', error);
          return of([]);
        }),
      );
  }

  public getModelCount(search = ''): Observable<number> {
    const apiUrl = this.settingsService.apiUrl();
    let params = new HttpParams();
    if (search.trim().length > 0) {
      params = params.set('search', search.trim());
    }

    return this.http
      .get<number>(`${apiUrl}/api/metadata/models/count`, { params })
      .pipe(
        catchError((error) => {
          this.notifyError('Loading model count', error);
          return of(0);
        }),
      );
  }

  public getModel(id: string): Observable<Model | null> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .get<ModelSnapshot>(`${apiUrl}/api/metadata/models/${id}`)
      .pipe(
        map(Model.fromSnapshot),
        catchError((error) => {
          this.notifyError('Loading model', error);
          return of(null);
        }),
      );
  }

  public upsertModel(model: Model): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .post<void>(`${apiUrl}/api/metadata/models`, model.toSnapshot())
      .pipe(
        catchError((error) => {
          this.notifyError('Saving model', error);
          return of(undefined);
        }),
      );
  }

  public deleteModel(id: string): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.delete<void>(`${apiUrl}/api/metadata/models/${id}`).pipe(
      catchError((error) => {
        this.notifyError('Deleting model', error);
        return of(undefined);
      }),
    );
  }

  public getEvalConfigSummaries(
    search = '',
    skip = 0,
    take = 0,
    sortBy: EvalConfigSortField = EvalConfigSortField.Name,
    sortDirection: SortDirection = SortDirection.Ascending,
  ): Observable<EvalConfigSummary[]> {
    const apiUrl = this.settingsService.apiUrl();
    let params = new HttpParams()
      .set('skip', skip)
      .set('take', take)
      .set('sortBy', sortBy)
      .set('sortDirection', sortDirection);
    if (search.trim().length > 0) {
      params = params.set('search', search.trim());
    }

    return this.http
      .get<
        EvalConfigSummarySnapshot[]
      >(`${apiUrl}/api/eval/configs`, { params })
      .pipe(
        map((snapshots) => snapshots.map(EvalConfigSummary.fromSnapshot)),
        catchError((error) => {
          this.notifyError('Loading eval configs', error);
          return of([]);
        }),
      );
  }

  public getEvalConfigCount(search = ''): Observable<number> {
    const apiUrl = this.settingsService.apiUrl();
    let params = new HttpParams();
    if (search.trim().length > 0) {
      params = params.set('search', search.trim());
    }

    return this.http
      .get<number>(`${apiUrl}/api/eval/configs/count`, { params })
      .pipe(
        catchError((error) => {
          this.notifyError('Loading eval config count', error);
          return of(0);
        }),
      );
  }

  public getEvalConfig(id: string): Observable<EvalConfig | null> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .get<EvalConfigSnapshot>(`${apiUrl}/api/eval/configs/${id}`)
      .pipe(
        map(EvalConfig.fromSnapshot),
        catchError((error) => {
          this.notifyError('Loading eval config', error);
          return of(null);
        }),
      );
  }

  public getEvalConfigDetail(
    id: string,
  ): Observable<EvalConfigDetailSnapshot | null> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .get<EvalConfigDetailSnapshot>(`${apiUrl}/api/eval/configs/${id}/detail`)
      .pipe(
        catchError((error) => {
          this.notifyError('Loading eval config detail', error);
          return of(null);
        }),
      );
  }

  public upsertEvalConfig(evalConfig: EvalConfig): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .post<void>(`${apiUrl}/api/eval/configs`, evalConfig.toSnapshot())
      .pipe(
        catchError((error) => {
          this.notifyError('Saving eval config', error);
          return of(undefined);
        }),
      );
  }

  public deleteEvalConfig(id: string): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.delete<void>(`${apiUrl}/api/eval/configs/${id}`).pipe(
      catchError((error) => {
        this.notifyError('Deleting eval config', error);
        return of(undefined);
      }),
    );
  }

  public upsertEvalGraders(graders: EvalGraderSnapshot[]): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .post<void>(`${apiUrl}/api/eval/configs/graders`, graders)
      .pipe(
        catchError((error) => {
          this.notifyError('Saving eval graders', error);
          return of(undefined);
        }),
      );
  }

  public deleteEvalGrader(id: string): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.delete<void>(`${apiUrl}/api/eval/graders/${id}`).pipe(
      catchError((error) => {
        this.notifyError('Deleting eval grader', error);
        return of(undefined);
      }),
    );
  }

  public upsertEvalColumns(columns: EvalColumnSnapshot[]): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .post<void>(`${apiUrl}/api/eval/configs/columns`, columns)
      .pipe(
        catchError((error) => {
          this.notifyError('Saving eval columns', error);
          return of(undefined);
        }),
      );
  }

  public deleteEvalColumn(id: string): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.delete<void>(`${apiUrl}/api/eval/columns/${id}`).pipe(
      catchError((error) => {
        this.notifyError('Deleting eval column', error);
        return of(undefined);
      }),
    );
  }

  public upsertEvalRows(rows: EvalRowSnapshot[]): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .post<void>(`${apiUrl}/api/eval/configs/rows`, rows)
      .pipe(
        catchError((error) => {
          this.notifyError('Saving eval rows', error);
          return of(undefined);
        }),
      );
  }

  public deleteEvalRow(id: string): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.delete<void>(`${apiUrl}/api/eval/rows/${id}`).pipe(
      catchError((error) => {
        this.notifyError('Deleting eval row', error);
        return of(undefined);
      }),
    );
  }

  public upsertEvalData(data: EvalDataSnapshot[]): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .post<void>(`${apiUrl}/api/eval/configs/data`, data)
      .pipe(
        catchError((error) => {
          this.notifyError('Saving eval data', error);
          return of(undefined);
        }),
      );
  }

  public deleteEvalData(id: string): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.delete<void>(`${apiUrl}/api/eval/data/${id}`).pipe(
      catchError((error) => {
        this.notifyError('Deleting eval data', error);
        return of(undefined);
      }),
    );
  }

  public startEvalRun(
    evalConfigId: string,
    name?: string | null,
    sampleCount?: number | null,
  ): Observable<string | undefined> {
    const apiUrl = this.settingsService.apiUrl();
    const payload = { name: name ?? null, sampleCount: sampleCount ?? null };
    return this.http
      .post<string>(`${apiUrl}/api/eval/configs/${evalConfigId}/runs`, payload)
      .pipe(
        catchError((error) => {
          this.notifyError('Starting eval run', error);
          return of(undefined);
        }),
      );
  }

  public getEvalRunSummaries(
    evalConfigId: string,
    search = '',
    skip = 0,
    take = 0,
    sortBy: EvalRunSortField = EvalRunSortField.Started,
    sortDirection: SortDirection = SortDirection.Descending,
  ): Observable<EvalRunSummarySnapshot[]> {
    const apiUrl = this.settingsService.apiUrl();
    let params = new HttpParams()
      .set('skip', skip)
      .set('take', take)
      .set('sortBy', sortBy)
      .set('sortDirection', sortDirection);
    if (search.trim().length > 0) {
      params = params.set('search', search.trim());
    }

    return this.http
      .get<EvalRunSummarySnapshot[]>(
        `${apiUrl}/api/eval/configs/${evalConfigId}/runs`,
        { params },
      )
      .pipe(
        catchError((error) => {
          this.notifyError('Loading eval runs', error);
          return of([]);
        }),
      );
  }

  public getEvalRunCount(evalConfigId: string, search = ''): Observable<number> {
    const apiUrl = this.settingsService.apiUrl();
    let params = new HttpParams();
    if (search.trim().length > 0) {
      params = params.set('search', search.trim());
    }

    return this.http
      .get<number>(`${apiUrl}/api/eval/configs/${evalConfigId}/runs/count`, {
        params,
      })
      .pipe(
        catchError((error) => {
          this.notifyError('Loading eval run count', error);
          return of(0);
        }),
      );
  }

  public getEvalRunDetail(evalRunId: string): Observable<EvalRunDetailSnapshot | null> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .get<EvalRunDetailSnapshot>(`${apiUrl}/api/eval/runs/${evalRunId}/detail`)
      .pipe(
        catchError((error) => {
          this.notifyError('Loading eval run detail', error);
          return of(null);
        }),
      );
  }

  public deleteEvalRun(evalRunId: string): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.delete<void>(`${apiUrl}/api/eval/runs/${evalRunId}`).pipe(
      catchError((error) => {
        this.notifyError('Deleting eval run', error);
        return of(undefined);
      }),
    );
  }

  public cancelEvalRun(evalRunId: string): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .post<void>(`${apiUrl}/api/eval/runs/${evalRunId}/cancel`, null)
      .pipe(
        catchError((error) => {
          this.notifyError('Canceling eval run', error);
          return of(undefined);
        }),
      );
  }

  public getEvalRunRows(
    evalRunId: string,
    search = '',
    skip = 0,
    take = 0,
    sortBy: EvalRunRowSortField = EvalRunRowSortField.Name,
    sortDirection: SortDirection = SortDirection.Ascending,
  ): Observable<EvalRunRowDetailSnapshot[]> {
    const apiUrl = this.settingsService.apiUrl();
    let params = new HttpParams()
      .set('skip', skip)
      .set('take', take)
      .set('sortBy', sortBy)
      .set('sortDirection', sortDirection);
    if (search.trim().length > 0) {
      params = params.set('search', search.trim());
    }

    return this.http
      .get<EvalRunRowDetailSnapshot[]>(`${apiUrl}/api/eval/runs/${evalRunId}/rows`, {
        params,
      })
      .pipe(
        catchError((error) => {
          this.notifyError('Loading eval run rows', error);
          return of([]);
        }),
      );
  }

  public getEvalRunRowCount(evalRunId: string, search = ''): Observable<number> {
    const apiUrl = this.settingsService.apiUrl();
    let params = new HttpParams();
    if (search.trim().length > 0) {
      params = params.set('search', search.trim());
    }

    return this.http
      .get<number>(`${apiUrl}/api/eval/runs/${evalRunId}/rows/count`, { params })
      .pipe(
        catchError((error) => {
          this.notifyError('Loading eval run row count', error);
          return of(0);
        }),
      );
  }

  public getSchemaTypeNames(): Observable<string[]> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.get<string[]>(`${apiUrl}/api/schematype`).pipe(
      catchError((error) => {
        this.notifyError('Loading schema type names', error);
        return of([]);
      }),
    );
  }

  public getSchemaType(typeName: string): Observable<string | null> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.get<string>(`${apiUrl}/api/schematype/${typeName}`).pipe(
      catchError((error) => {
        this.notifyError('Loading schema type', error);
        return of(null);
      }),
    );
  }

  public getToolDisplayNames(): Observable<string[]> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.get<string[]>(`${apiUrl}/api/tool`).pipe(
      catchError((error) => {
        this.notifyError('Loading tool display names', error);
        return of([]);
      }),
    );
  }

  public getAssets(
    scope: AssetScope = AssetScope.Library,
    skip = 0,
    take = 0,
    sortBy = AssetSortField.Name,
    sortDirection = SortDirection.Descending,
    search = '',
    runId?: string,
  ): Observable<AssetSummary[]> {
    const apiUrl = this.settingsService.apiUrl();
    let params = new HttpParams()
      .set('scope', scope)
      .set('skip', skip)
      .set('take', take)
      .set('sortBy', sortBy)
      .set('sortDirection', sortDirection);
    if (search.trim().length > 0) {
      params = params.set('search', search.trim());
    }
    if (runId) {
      params = params.set('runId', runId);
    }
    return this.http
      .get<AssetSummary[]>(`${apiUrl}/api/assets`, { params })
      .pipe(
        catchError((error) => {
          this.notifyError('Loading assets', error);
          return of([]);
        }),
      );
  }

  public getAssetsCount(
    scope: AssetScope = AssetScope.Library,
    search = '',
    runId?: string,
  ): Observable<number> {
    const apiUrl = this.settingsService.apiUrl();
    let params = new HttpParams().set('scope', scope);
    if (search.trim().length > 0) {
      params = params.set('search', search.trim());
    }
    if (runId) {
      params = params.set('runId', runId);
    }
    return this.http.get<number>(`${apiUrl}/api/assets/count`, { params }).pipe(
      catchError((error) => {
        this.notifyError('Loading asset count', error);
        return of(0);
      }),
    );
  }

  public uploadAsset(
    file: File,
    name: string,
    scope: AssetScope,
    runId?: string,
  ): Observable<AssetSummary | null> {
    const apiUrl = this.settingsService.apiUrl();
    const formData = new FormData();
    formData.append('file', file);
    formData.append('name', name);
    formData.append('scope', scope);
    if (runId) {
      formData.append('runId', runId);
    }

    return this.http.post<AssetSummary>(`${apiUrl}/api/assets`, formData).pipe(
      catchError((error) => {
        this.notifyError('Uploading asset', error);
        return of(null);
      }),
    );
  }

  public deleteAsset(assetId: string): Observable<void> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.delete<void>(`${apiUrl}/api/assets/${assetId}`).pipe(
      catchError((error) => {
        this.notifyError('Deleting asset', error);
        return of(undefined);
      }),
    );
  }

  public getAssetContent(assetId: string): Observable<Blob | null> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .get(`${apiUrl}/api/assets/${assetId}/content`, { responseType: 'blob' })
      .pipe(
        catchError((error) => {
          this.notifyError('Downloading asset', error);
          return of(null);
        }),
      );
  }

  public getAssetText(assetId: string): Observable<AssetText | null> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .get<AssetText>(`${apiUrl}/api/assets/${assetId}/text`)
      .pipe(
        catchError((error) => {
          this.notifyError('Loading asset text', error);
          return of(null);
        }),
      );
  }

  public updateAssetText(
    assetId: string,
    content: string,
  ): Observable<boolean> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http
      .put<void>(`${apiUrl}/api/assets/${assetId}/text`, { content })
      .pipe(
        map(() => true),
        catchError((error) => {
          this.notifyError('Saving asset text', error);
          return of(false);
        }),
      );
  }

  public getAssetContentUrl(assetId: string): string {
    const apiUrl = this.settingsService.apiUrl();
    return `${apiUrl}/api/assets/${encodeURIComponent(assetId)}/content`;
  }

  public importTransfer(file: File): Observable<TransferImportResult> {
    const apiUrl = this.settingsService.apiUrl();
    const formData = new FormData();
    formData.append('file', file);

    return this.http.post<TransferImportResult>(
      `${apiUrl}/api/transfer/import`,
      formData,
    );
  }

  public exportTransfer(
    request: TransferExportRequest,
  ): Observable<HttpResponse<Blob>> {
    const apiUrl = this.settingsService.apiUrl();
    return this.http.post(`${apiUrl}/api/transfer/export`, request, {
      observe: 'response',
      responseType: 'blob',
    });
  }

  private notifyError(operation: string, error: unknown): void {
    const detail = this.toastService.extractErrorDetail(error);
    const message = detail
      ? `${operation} failed: ${detail}`
      : `${operation} failed.`;
    this.toastService.error(message);
  }
}

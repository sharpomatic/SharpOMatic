import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  ElementRef,
  OnDestroy,
  OnInit,
  TemplateRef,
  ViewChild,
  inject,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  Chart,
  ChartConfiguration,
  ChartDataset,
  registerables,
} from 'chart.js';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { RunStatus } from '../../enumerations/run-status';
import {
  ModelCallMetricBreakdownItem,
  ModelCallMetricBucket,
  ModelCallMetricCallSummary,
  ModelCallMetricMasterItem,
  ModelCallMetricScope,
  ModelCallMetricsDashboard,
} from './interfaces/model-call-metrics-dashboard';
import {
  WorkflowRunMetricMasterItem,
  WorkflowRunMetricRunSummary,
  WorkflowRunMetricScope,
  WorkflowRunMetricsDashboard,
} from './interfaces/workflow-run-metrics-dashboard';

Chart.register(...registerables);

type MetricsRangePreset =
  | '24h'
  | '7d'
  | '30d'
  | '90d'
  | '1y'
  | 'all'
  | 'custom';
type MetricsView = 'model-calls' | 'workflow-runs';

interface MetricsBreakdownSection {
  title: string;
  items: ModelCallMetricBreakdownItem[];
}

@Component({
  selector: 'app-metrics',
  standalone: true,
  imports: [CommonModule, FormsModule, TabComponent],
  templateUrl: './metrics.component.html',
  styleUrls: ['./metrics.component.scss'],
})
export class MetricsComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('metricsTab', { static: true }) metricsTab!: TemplateRef<unknown>;
  @ViewChild('workflowRunsTab', { static: true })
  workflowRunsTab!: TemplateRef<unknown>;
  @ViewChild('callsCanvas') callsCanvas?: ElementRef<HTMLCanvasElement>;
  @ViewChild('costCanvas') costCanvas?: ElementRef<HTMLCanvasElement>;
  @ViewChild('tokensCanvas') tokensCanvas?: ElementRef<HTMLCanvasElement>;
  @ViewChild('durationCanvas') durationCanvas?: ElementRef<HTMLCanvasElement>;
  @ViewChild('workflowRunsCanvas')
  workflowRunsCanvas?: ElementRef<HTMLCanvasElement>;
  @ViewChild('workflowCostCanvas')
  workflowCostCanvas?: ElementRef<HTMLCanvasElement>;
  @ViewChild('workflowTokensCanvas')
  workflowTokensCanvas?: ElementRef<HTMLCanvasElement>;
  @ViewChild('workflowDurationCanvas')
  workflowDurationCanvas?: ElementRef<HTMLCanvasElement>;

  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly changeDetector = inject(ChangeDetectorRef);
  private readonly selectedKeys = new Map<ModelCallMetricScope, string>();
  private readonly workflowSelectedKeys = new Map<WorkflowRunMetricScope, string>();
  private readonly charts: Chart[] = [];
  private chartRenderId: ReturnType<typeof setTimeout> | undefined;
  private masterSearchDebounceId: ReturnType<typeof setTimeout> | undefined;
  private viewReady = false;

  public readonly ModelCallMetricScope = ModelCallMetricScope;
  public readonly ModelCallMetricBucket = ModelCallMetricBucket;
  public readonly WorkflowRunMetricScope = WorkflowRunMetricScope;
  public readonly RunStatus = RunStatus;

  public metricsView: MetricsView = 'model-calls';
  public tabs: TabItem[] = [];
  public workflowTabs: TabItem[] = [];
  public activeTabId = 'all';
  public activeScope = ModelCallMetricScope.All;
  public activeWorkflowTabId = 'all';
  public activeWorkflowScope = WorkflowRunMetricScope.All;
  public dashboard: ModelCallMetricsDashboard | null = null;
  public workflowDashboard: WorkflowRunMetricsDashboard | null = null;
  public breakdownSections: MetricsBreakdownSection[] = [];
  public isLoading = false;
  public rangePreset: MetricsRangePreset = '24h';
  public bucket = ModelCallMetricBucket.Hour;
  public start = this.toDateTimeLocal(
    new Date(Date.now() - 24 * 60 * 60 * 1000),
  );
  public end = this.toDateTimeLocal(new Date());
  public masterSearch = '';
  public recentPage = 1;
  public readonly recentPageSize = 25;

  ngOnInit(): void {
    this.tabs = [
      { id: 'all', title: 'All', content: this.metricsTab },
      { id: 'workflows', title: 'Workflows', content: this.metricsTab },
      { id: 'connectors', title: 'Connectors', content: this.metricsTab },
      { id: 'models', title: 'Models', content: this.metricsTab },
    ];

    this.workflowTabs = [
      { id: 'all', title: 'All', content: this.workflowRunsTab },
      { id: 'workflows', title: 'Workflows', content: this.workflowRunsTab },
      { id: 'errors', title: 'Errors', content: this.workflowRunsTab },
    ];
  }

  ngAfterViewInit(): void {
    this.viewReady = true;
    this.loadDashboard();
    this.changeDetector.detectChanges();
  }

  ngOnDestroy(): void {
    this.destroyCharts();

    if (this.chartRenderId) {
      clearTimeout(this.chartRenderId);
    }

    if (this.masterSearchDebounceId) {
      clearTimeout(this.masterSearchDebounceId);
    }
  }

  onTabChange(tabId: string): void {
    this.activeTabId = tabId;
    this.activeScope = this.scopeFromTabId(tabId);
    this.dashboard = null;
    this.breakdownSections = [];
    this.recentPage = 1;
    this.loadDashboard();
  }

  onWorkflowTabChange(tabId: string): void {
    this.activeWorkflowTabId = tabId;
    this.activeWorkflowScope = this.workflowScopeFromTabId(tabId);
    this.workflowDashboard = null;
    this.recentPage = 1;
    this.loadDashboard();
  }

  onMetricsViewChange(view: MetricsView): void {
    if (this.metricsView === view) {
      return;
    }

    this.metricsView = view;
    this.dashboard = null;
    this.workflowDashboard = null;
    this.breakdownSections = [];
    this.recentPage = 1;
    this.destroyCharts();
    this.loadDashboard();
  }

  onRangePresetChange(): void {
    if (this.rangePreset !== 'custom') {
      const now = new Date();
      const start =
        this.rangePreset === '24h'
          ? new Date(now.getTime() - 24 * 60 * 60 * 1000)
          : this.rangePreset === '1y'
            ? new Date(now.getTime() - 365 * 24 * 60 * 60 * 1000)
            : this.rangePreset === '90d'
              ? new Date(now.getTime() - 90 * 24 * 60 * 60 * 1000)
              : this.rangePreset === '30d'
                ? new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000)
                : new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);

      this.start = this.toDateTimeLocal(start);
      this.end = this.toDateTimeLocal(now);
      this.bucket =
        this.rangePreset === '24h'
          ? ModelCallMetricBucket.Hour
          : this.rangePreset === '1y' || this.rangePreset === 'all'
            ? ModelCallMetricBucket.Week
            : ModelCallMetricBucket.Day;
    }

    this.recentPage = 1;
    this.loadDashboard();
  }

  onCustomDateChange(): void {
    this.rangePreset = 'custom';
    this.recentPage = 1;
    this.loadDashboard();
  }

  onBucketChange(): void {
    this.loadDashboard();
  }

  onMasterSearchChange(): void {
    if (this.masterSearchDebounceId) {
      clearTimeout(this.masterSearchDebounceId);
    }

    this.masterSearchDebounceId = setTimeout(() => this.loadDashboard(), 250);
  }

  selectMasterItem(item: ModelCallMetricMasterItem): void {
    if (this.selectedKeys.get(this.activeScope) === item.key) {
      return;
    }

    this.selectedKeys.set(this.activeScope, item.key);
    this.recentPage = 1;
    this.loadDashboard();
  }

  selectWorkflowMasterItem(item: WorkflowRunMetricMasterItem): void {
    if (this.workflowSelectedKeys.get(this.activeWorkflowScope) === item.key) {
      return;
    }

    this.workflowSelectedKeys.set(this.activeWorkflowScope, item.key);
    this.recentPage = 1;
    this.loadDashboard();
  }

  refresh(): void {
    if (this.rangePreset !== 'custom') {
      this.onRangePresetChange();
      return;
    }

    this.loadDashboard();
  }

  recentPageCount(): number {
    const total = this.dashboard?.recentCallsTotal ?? 0;
    return Math.max(1, Math.ceil(total / this.recentPageSize));
  }

  workflowRecentPageCount(): number {
    const total = this.workflowDashboard?.recentRunsTotal ?? 0;
    return Math.max(1, Math.ceil(total / this.recentPageSize));
  }

  onRecentPageChange(page: number): void {
    const pageCount =
      this.metricsView === 'workflow-runs'
        ? this.workflowRecentPageCount()
        : this.recentPageCount();
    if (page < 1 || page > pageCount || page === this.recentPage) {
      return;
    }

    this.recentPage = page;
    this.loadDashboard();
  }

  selectedKey(): string | null {
    return this.selectedKeys.get(this.activeScope) ?? null;
  }

  selectedWorkflowKey(): string | null {
    return this.workflowSelectedKeys.get(this.activeWorkflowScope) ?? null;
  }

  masterTitle(): string {
    switch (this.activeScope) {
      case ModelCallMetricScope.Workflow:
        return 'Workflows';
      case ModelCallMetricScope.Connector:
        return 'Connectors';
      case ModelCallMetricScope.Model:
        return 'Models';
      default:
        return '';
    }
  }

  workflowMasterTitle(): string {
    switch (this.activeWorkflowScope) {
      case WorkflowRunMetricScope.Workflow:
        return 'Workflows';
      case WorkflowRunMetricScope.Error:
        return 'Errors';
      default:
        return '';
    }
  }

  hasMetricData(): boolean {
    return (this.dashboard?.totals.totalCalls ?? 0) > 0;
  }

  hasWorkflowMetricData(): boolean {
    return (this.workflowDashboard?.totals.totalRuns ?? 0) > 0;
  }

  formatNumber(value: number | null | undefined): string {
    return (value ?? 0).toLocaleString();
  }

  formatCost(value: number | null | undefined): string {
    return (value ?? 0).toLocaleString(undefined, {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 4,
      maximumFractionDigits: 4,
    });
  }

  formatDuration(value: number | null | undefined): string {
    if (value === null || value === undefined) {
      return '-';
    }

    if (value < 1000) {
      return `${Math.round(value)} ms`;
    }

    return `${(value / 1000).toFixed(2)} s`;
  }

  formatPercent(value: number | null | undefined): string {
    return `${((value ?? 0) * 100).toFixed(1)}%`;
  }

  formatDateTime(value: string | null | undefined): string {
    if (!value) {
      return '-';
    }

    return new Date(value).toLocaleString();
  }

  displayConnector(call: ModelCallMetricCallSummary): string {
    return call.connectorName ?? 'No connector';
  }

  displayModel(call: ModelCallMetricCallSummary): string {
    return call.modelName ?? 'No model';
  }

  runStatusLabel(status: RunStatus): string {
    switch (status) {
      case RunStatus.Success:
        return 'Success';
      case RunStatus.Failed:
        return 'Failure';
      case RunStatus.Suspended:
        return 'Suspended';
      case RunStatus.Running:
        return 'Running';
      default:
        return 'Created';
    }
  }

  displayWorkflowRunNode(run: WorkflowRunMetricRunSummary): string {
    return run.failedNodeTitle ?? '';
  }

  private loadDashboard(): void {
    const allTime = this.rangePreset === 'all';
    let start: Date | null = null;
    let end: Date | null = null;
    if (!allTime) {
      start = new Date(this.start);
      end = new Date(this.end);
      if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) {
        return;
      }
    }

    this.isLoading = true;
    this.refreshTabs();
    if (this.metricsView === 'workflow-runs') {
      this.loadWorkflowRunDashboard(start, end, allTime);
      return;
    }

    this.serverRepository
      .getModelCallMetricsDashboard(
        start,
        end,
        this.bucket,
        this.activeScope,
        this.selectedKey(),
        this.masterSearch,
        (this.recentPage - 1) * this.recentPageSize,
        this.recentPageSize,
        allTime,
      )
      .subscribe((dashboard) => {
        if (!dashboard) {
          this.isLoading = false;
          this.refreshTabs();
          this.changeDetector.detectChanges();
          return;
        }

        if (
          this.activeScope !== ModelCallMetricScope.All &&
          !this.selectedKey() &&
          dashboard.masterItems.length > 0
        ) {
          this.selectedKeys.set(this.activeScope, dashboard.masterItems[0].key);
          this.loadDashboard();
          return;
        }

        this.isLoading = false;
        this.dashboard = dashboard;
        this.updateBreakdownSections(dashboard);
        this.refreshTabs();
        this.changeDetector.detectChanges();
        this.scheduleChartRender();
      });
  }

  private loadWorkflowRunDashboard(
    start: Date | null,
    end: Date | null,
    allTime: boolean,
  ): void {
    this.serverRepository
      .getWorkflowRunMetricsDashboard(
        start,
        end,
        this.bucket,
        this.activeWorkflowScope,
        this.selectedWorkflowKey(),
        this.masterSearch,
        (this.recentPage - 1) * this.recentPageSize,
        this.recentPageSize,
        allTime,
      )
      .subscribe((dashboard) => {
        if (!dashboard) {
          this.isLoading = false;
          this.changeDetector.detectChanges();
          return;
        }

        if (
          this.activeWorkflowScope !== WorkflowRunMetricScope.All &&
          !this.selectedWorkflowKey() &&
          dashboard.masterItems.length > 0
        ) {
          this.workflowSelectedKeys.set(
            this.activeWorkflowScope,
            dashboard.masterItems[0].key,
          );
          this.loadDashboard();
          return;
        }

        this.isLoading = false;
        this.workflowDashboard = dashboard;
        this.refreshTabs();
        this.changeDetector.detectChanges();
        this.scheduleChartRender();
      });
  }

  private updateBreakdownSections(dashboard: ModelCallMetricsDashboard): void {
    if (this.activeScope === ModelCallMetricScope.All) {
      this.breakdownSections = [
        { title: 'Top Workflows', items: dashboard.workflowBreakdown },
        { title: 'Top Connectors', items: dashboard.connectorBreakdown },
        { title: 'Top Models', items: dashboard.modelBreakdown },
      ];
      return;
    }

    if (this.activeScope === ModelCallMetricScope.Workflow) {
      this.breakdownSections = [
        { title: 'Top Nodes', items: dashboard.nodeBreakdown },
        { title: 'Top Models', items: dashboard.modelBreakdown },
        { title: 'Top Connectors', items: dashboard.connectorBreakdown },
      ];
      return;
    }

    if (this.activeScope === ModelCallMetricScope.Connector) {
      this.breakdownSections = [
        { title: 'Top Models', items: dashboard.modelBreakdown },
        { title: 'Top Workflows', items: dashboard.workflowBreakdown },
      ];
      return;
    }

    this.breakdownSections = [
      { title: 'Top Workflows', items: dashboard.workflowBreakdown },
      { title: 'Top Nodes', items: dashboard.nodeBreakdown },
    ];
  }

  private refreshTabs(): void {
    this.tabs = this.tabs.map((tab) => ({ ...tab }));
    this.workflowTabs = this.workflowTabs.map((tab) => ({ ...tab }));
  }

  private scheduleChartRender(): void {
    if (!this.viewReady) {
      return;
    }

    if (this.chartRenderId) {
      clearTimeout(this.chartRenderId);
    }

    this.chartRenderId = setTimeout(() => this.renderCharts(), 0);
  }

  private renderCharts(): void {
    if (this.metricsView === 'workflow-runs') {
      this.renderWorkflowRunCharts();
      return;
    }

    if (!this.dashboard) {
      return;
    }

    this.destroyCharts();

    const labels = this.dashboard.timeBuckets.map((bucket) =>
      this.formatBucketLabel(bucket.start),
    );

    this.createChart(this.callsCanvas, {
      type: 'bar',
      data: {
        labels,
        datasets: [
          this.barDataset(
            'Succeeded',
            this.dashboard.timeBuckets.map((b) => b.successfulCalls),
            '#198754',
          ),
          this.barDataset(
            'Failed',
            this.dashboard.timeBuckets.map((b) => b.failedCalls),
            '#dc3545',
          ),
        ],
      },
      options: this.stackedOptions(),
    });

    this.createChart(this.costCanvas, {
      type: 'line',
      data: {
        labels,
        datasets: [
          this.lineDataset(
            'Cost',
            this.dashboard.timeBuckets.map((b) => b.totalCost),
            '#0d6efd',
          ),
        ],
      },
      options: this.lineOptions(),
    });

    this.createChart(this.tokensCanvas, {
      type: 'bar',
      data: {
        labels,
        datasets: [
          this.barDataset(
            'Input',
            this.dashboard.timeBuckets.map((b) => b.inputTokens),
            '#6f42c1',
          ),
          this.barDataset(
            'Output',
            this.dashboard.timeBuckets.map((b) => b.outputTokens),
            '#20c997',
          ),
        ],
      },
      options: this.stackedOptions(),
    });

    this.createChart(this.durationCanvas, {
      type: 'line',
      data: {
        labels,
        datasets: [
          this.lineDataset(
            'Average',
            this.dashboard.timeBuckets.map((b) => b.averageDuration ?? 0),
            '#fd7e14',
          ),
          this.lineDataset(
            'P95',
            this.dashboard.timeBuckets.map((b) => b.p95Duration ?? 0),
            '#dc3545',
          ),
        ],
      },
      options: this.lineOptions(),
    });
  }

  private renderWorkflowRunCharts(): void {
    if (!this.workflowDashboard) {
      return;
    }

    this.destroyCharts();

    const labels = this.workflowDashboard.timeBuckets.map((bucket) =>
      this.formatBucketLabel(bucket.start),
    );

    this.createChart(this.workflowRunsCanvas, {
      type: 'bar',
      data: {
        labels,
        datasets: [
          this.barDataset(
            'Succeeded',
            this.workflowDashboard.timeBuckets.map((b) => b.successfulRuns),
            '#198754',
          ),
          this.barDataset(
            'Failed',
            this.workflowDashboard.timeBuckets.map((b) => b.failedRuns),
            '#dc3545',
          ),
          this.barDataset(
            'Suspended',
            this.workflowDashboard.timeBuckets.map((b) => b.suspendedRuns),
            '#fd7e14',
          ),
        ],
      },
      options: this.stackedOptions(),
    });

    this.createChart(this.workflowCostCanvas, {
      type: 'line',
      data: {
        labels,
        datasets: [
          this.lineDataset(
            'Cost',
            this.workflowDashboard.timeBuckets.map((b) => b.totalModelCost),
            '#0d6efd',
          ),
        ],
      },
      options: this.lineOptions(),
    });

    this.createChart(this.workflowTokensCanvas, {
      type: 'bar',
      data: {
        labels,
        datasets: [
          this.barDataset(
            'Input',
            this.workflowDashboard.timeBuckets.map((b) => b.inputTokens),
            '#6f42c1',
          ),
          this.barDataset(
            'Output',
            this.workflowDashboard.timeBuckets.map((b) => b.outputTokens),
            '#20c997',
          ),
        ],
      },
      options: this.stackedOptions(),
    });

    this.createChart(this.workflowDurationCanvas, {
      type: 'line',
      data: {
        labels,
        datasets: [
          this.lineDataset(
            'Average',
            this.workflowDashboard.timeBuckets.map(
              (b) => b.averageDuration ?? 0,
            ),
            '#0d6efd',
          ),
          this.lineDataset(
            'P95',
            this.workflowDashboard.timeBuckets.map((b) => b.p95Duration ?? 0),
            '#dc3545',
          ),
        ],
      },
      options: this.lineOptions(),
    });
  }

  private createChart(
    canvas: ElementRef<HTMLCanvasElement> | undefined,
    config: ChartConfiguration,
  ): void {
    if (!canvas) {
      return;
    }

    this.charts.push(new Chart(canvas.nativeElement, config));
  }

  private destroyCharts(): void {
    while (this.charts.length > 0) {
      this.charts.pop()?.destroy();
    }
  }

  private barDataset(
    label: string,
    data: number[],
    backgroundColor: string,
  ): ChartDataset<'bar', number[]> {
    return {
      label,
      data,
      backgroundColor,
      borderColor: backgroundColor,
      borderWidth: 1,
    };
  }

  private lineDataset(
    label: string,
    data: number[],
    borderColor: string,
  ): ChartDataset<'line', number[]> {
    return {
      label,
      data,
      borderColor,
      backgroundColor: borderColor,
      tension: 0.25,
      pointRadius: 2,
    };
  }

  private stackedOptions(): ChartConfiguration['options'] {
    return {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { position: 'bottom' },
      },
      scales: {
        x: { stacked: true },
        y: { stacked: true, beginAtZero: true },
      },
    };
  }

  private lineOptions(): ChartConfiguration['options'] {
    return {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { position: 'bottom' },
      },
      scales: {
        y: { beginAtZero: true },
      },
    };
  }

  private scopeFromTabId(tabId: string): ModelCallMetricScope {
    switch (tabId) {
      case 'workflows':
        return ModelCallMetricScope.Workflow;
      case 'connectors':
        return ModelCallMetricScope.Connector;
      case 'models':
        return ModelCallMetricScope.Model;
      default:
        return ModelCallMetricScope.All;
    }
  }

  private workflowScopeFromTabId(tabId: string): WorkflowRunMetricScope {
    switch (tabId) {
      case 'workflows':
        return WorkflowRunMetricScope.Workflow;
      case 'errors':
        return WorkflowRunMetricScope.Error;
      default:
        return WorkflowRunMetricScope.All;
    }
  }

  private formatBucketLabel(value: string): string {
    const date = new Date(value);
    if (this.bucket === ModelCallMetricBucket.Hour) {
      return date.toLocaleString(undefined, {
        month: 'short',
        day: 'numeric',
        hour: 'numeric',
      });
    }

    return date.toLocaleDateString(undefined, {
      month: 'short',
      day: 'numeric',
    });
  }

  private toDateTimeLocal(date: Date): string {
    const offsetMs = date.getTimezoneOffset() * 60 * 1000;
    return new Date(date.getTime() - offsetMs).toISOString().slice(0, 16);
  }
}

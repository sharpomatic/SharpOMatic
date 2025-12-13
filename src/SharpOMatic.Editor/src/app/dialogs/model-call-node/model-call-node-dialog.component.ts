import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Inject, OnInit, Output, TemplateRef, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ContextViewerComponent } from '../../components/context-viewer/context-viewer.component';
import { TabComponent, TabItem } from '../../components/tab/tab.component';
import { ModelCallNodeEntity } from '../../entities/definitions/model-call-node.entity';
import { TraceProgressModel } from '../../pages/workflow/interfaces/trace-progress-model';
import { DIALOG_DATA } from '../services/dialog.service';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { ModelSummary } from '../../metadata/definitions/model-summary';
import { Model } from '../../metadata/definitions/model';
import { ModelConfig } from '../../metadata/definitions/model-config';

@Component({
  selector: 'app-model-call-node-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TabComponent,
    ContextViewerComponent,
  ],
  templateUrl: './model-call-node-dialog.component.html',
  styleUrls: ['./model-call-node-dialog.component.scss'],
})
export class ModelCallNodeDialogComponent implements OnInit {
  @Output() close = new EventEmitter<void>();
  @ViewChild('detailsTab', { static: true }) detailsTab!: TemplateRef<unknown>;
  @ViewChild('inputsTab', { static: true }) inputsTab!: TemplateRef<unknown>;
  @ViewChild('outputsTab', { static: true }) outputsTab!: TemplateRef<unknown>;

  public node: ModelCallNodeEntity;
  public inputTraces: string[];
  public outputTraces: string[];
  public tabs: TabItem[] = [];
  public activeTabId = 'details';
  public availableModels: ModelSummary[] = [];
  public selectedModelId: string | null = null;
  public showTextFields = false;

  private loadedModel: Model | null = null;
  private modelConfig: ModelConfig | null = null;
  private modelConfigsCache: ModelConfig[] = [];

  private readonly serverRepository = inject(ServerRepositoryService);

  constructor(@Inject(DIALOG_DATA) data: { node: ModelCallNodeEntity, nodeTraces: TraceProgressModel[] }) {
    this.node = data.node;
    this.inputTraces = (data.nodeTraces ?? []).map(trace => trace.inputContext).filter((context): context is string => context != null);
    this.outputTraces = (data.nodeTraces ?? []).map(trace => trace.outputContext).filter((context): context is string => context != null);
  }

  ngOnInit(): void {
    this.tabs = [
      { id: 'details', title: 'Details', content: this.detailsTab },
      { id: 'inputs', title: 'Inputs', content: this.inputsTab },
      { id: 'outputs', title: 'Outputs', content: this.outputsTab },
    ];

    this.loadAvailableModels();
  }

  onClose(): void {
    this.close.emit();
  }

  onModelSelectionChange(modelId: string | null): void {
    this.selectedModelId = modelId;
    this.loadedModel = null;
    this.modelConfig = null;
    this.showTextFields = false;

    if (!modelId) {
      this.node.modelId.set('');
      return;
    }

    const summary = this.availableModels.find(model => model.modelId === modelId);
    this.node.modelId.set(summary?.modelId ?? modelId);
    this.loadModel(modelId);
  }

  private loadAvailableModels(): void {
    this.serverRepository.getModelSummaries().subscribe(models => {
      this.availableModels = models;
      this.syncSelectedModel();
    });
  }

  private syncSelectedModel(): void {
    const matchedModel = this.availableModels.find(model => model.modelId === this.node.modelId());

    if (matchedModel) {
      this.selectedModelId = matchedModel.modelId;
      this.node.modelId.set(matchedModel.modelId);
      this.loadModel(matchedModel.modelId);
      return;
    }

    this.selectedModelId = null;
    this.loadedModel = null;
    this.modelConfig = null;
    this.showTextFields = false;
    this.node.modelId.set('');
  }

  private loadModel(modelId: string): void {
    this.serverRepository.getModel(modelId).subscribe(model => {
      this.loadedModel = model;
      this.showTextFields = false;

      if (!model) {
        return;
      }

      this.node.modelId.set(model.modelId);
      this.loadModelConfig(model.configId());
    });
  }

  private loadModelConfig(configId: string): void {
    const applyConfig = (configs: ModelConfig[]) => {
      this.modelConfig = configs.find(config => config.configId === configId) ?? null;
      this.updateTextFieldVisibility();
    };

    if (this.modelConfigsCache.length) {
      applyConfig(this.modelConfigsCache);
      return;
    }

    this.serverRepository.getModelConfigs().subscribe(configs => {
      this.modelConfigsCache = configs;
      applyConfig(configs);
    });
  }

  private updateTextFieldVisibility(): void {
    const model = this.loadedModel;
    const config = this.modelConfig;

    if (!model || !config) {
      this.showTextFields = false;
      return;
    }

    const supportsTextCapability = config.capabilities.some(cap => cap.name === 'SupportsText');

    if (!supportsTextCapability) {
      this.showTextFields = false;
      return;
    }

    if (!config.isCustom) {
      this.showTextFields = true;
      return;
    }

    const customCapabilities = model.customCapabilities();
    this.showTextFields = customCapabilities.has('SupportsText');
  }
}

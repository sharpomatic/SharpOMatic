import { CommonModule } from '@angular/common';
import {
  Component,
  EventEmitter,
  Inject,
  OnInit,
  Output,
  TemplateRef,
  ViewChild,
  inject,
} from '@angular/core';
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
import { FieldDescriptor } from '../../metadata/definitions/field-descriptor';
import { FieldDescriptorType } from '../../metadata/enumerations/field-descriptor-type';
import {
  DynamicFieldsCapabilityContext,
  DynamicFieldsComponent,
} from '../../components/dynamic-fields/dynamic-fields.component';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { MonacoService } from '../../services/monaco.service';

@Component({
  selector: 'app-model-call-node-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TabComponent,
    ContextViewerComponent,
    DynamicFieldsComponent,
    MonacoEditorModule,
  ],
  templateUrl: './model-call-node-dialog.component.html',
  styleUrls: ['./model-call-node-dialog.component.scss'],
})
export class ModelCallNodeDialogComponent implements OnInit {
  @Output() close = new EventEmitter<void>();
  @ViewChild('detailsTab', { static: true }) detailsTab!: TemplateRef<unknown>;
  @ViewChild('chatTab', { static: true }) chatTab!: TemplateRef<unknown>;
  @ViewChild('inputsTab', { static: true }) inputsTab!: TemplateRef<unknown>;
  @ViewChild('outputsTab', { static: true }) outputsTab!: TemplateRef<unknown>;
  @ViewChild('textTab', { static: true }) textTab!: TemplateRef<unknown>;
  @ViewChild('imageTab', { static: true }) imageTab!: TemplateRef<unknown>;
  @ViewChild('toolCallingTab', { static: true })
  toolCallingTab!: TemplateRef<unknown>;
  @ViewChild('structuredTab', { static: true })
  structuredTab!: TemplateRef<unknown>;

  public node: ModelCallNodeEntity;
  public inputTraces: string[];
  public outputTraces: string[];
  public tabs: TabItem[] = [];
  public activeTabId = 'details';
  public availableModels: ModelSummary[] = [];
  public selectedModelId: string | null = null;
  public showTextInFields = false;
  public showTextOutFields = false;
  public structuredSchemaEditorOptions = MonacoService.editorOptionsJson;
  public typeSchemaNames: string[] = [];
  public toolDisplayNames: string[] = [];
  public get capabilityContext(): DynamicFieldsCapabilityContext | null {
    if (!this.modelConfig) {
      return null;
    }

    return {
      capabilities: this.modelConfig.capabilities,
      isCustom: this.modelConfig.isCustom,
      customCapabilities: this.loadedModel?.customCapabilities(),
    };
  }

  private loadedModel: Model | null = null;
  public modelConfig: ModelConfig | null = null;
  private modelConfigsCache: ModelConfig[] = [];
  private typeSchemaNamesLoaded = false;
  private toolDisplayNamesLoaded = false;

  private readonly serverRepository = inject(ServerRepositoryService);

  constructor(
    @Inject(DIALOG_DATA)
    data: {
      node: ModelCallNodeEntity;
      nodeTraces: TraceProgressModel[];
    },
  ) {
    this.node = data.node;
    this.inputTraces = (data.nodeTraces ?? [])
      .map((trace) => trace.inputContext)
      .filter((context): context is string => context != null);
    this.outputTraces = (data.nodeTraces ?? [])
      .map((trace) => trace.outputContext)
      .filter((context): context is string => context != null);
  }

  ngOnInit(): void {
    this.refreshTabs();
    this.loadAvailableModels();
    this.ensureTypeSchemaNamesLoaded();
  }

  onClose(): void {
    this.close.emit();
  }

  onModelSelectionChange(modelId: string | null): void {
    this.selectedModelId = modelId;
    this.loadedModel = null;
    this.modelConfig = null;
    this.showTextInFields = false;
    this.showTextOutFields = false;

    if (!modelId || modelId === '') {
      this.node.modelId.set(null);
      return;
    }

    const summary = this.availableModels.find(
      (model) => model.modelId === modelId,
    );
    this.node.modelId.set(summary?.modelId ?? modelId);
    this.loadModel(modelId);
  }

  private loadAvailableModels(): void {
    this.serverRepository.getModelSummaries().subscribe((models) => {
      this.availableModels = models;
      this.syncSelectedModel();
    });
  }

  private syncSelectedModel(): void {
    const matchedModel = this.availableModels.find(
      (model) => model.modelId === this.node.modelId(),
    );

    if (matchedModel) {
      this.selectedModelId = matchedModel.modelId;
      this.node.modelId.set(matchedModel.modelId);
      this.loadModel(matchedModel.modelId);
      return;
    }

    this.selectedModelId = null;
    this.loadedModel = null;
    this.modelConfig = null;
    this.showTextInFields = false;
    this.showTextOutFields = false;
    this.node.modelId.set(null);
    this.refreshTabs();
  }

  private loadModel(modelId: string): void {
    this.serverRepository.getModel(modelId).subscribe((model) => {
      this.loadedModel = model;
      this.showTextInFields = false;

      if (!model) {
        this.refreshTabs();
        return;
      }

      this.node.modelId.set(model.modelId);
      this.loadModelConfig(model.configId());
    });
  }

  private loadModelConfig(configId: string): void {
    const applyConfig = (configs: ModelConfig[]) => {
      this.modelConfig =
        configs.find((config) => config.configId === configId) ?? null;
      this.updateTextFieldVisibility();
      this.syncCallParameterValues();
      this.refreshTabs();
    };

    if (this.modelConfigsCache.length) {
      applyConfig(this.modelConfigsCache);
      return;
    }

    this.serverRepository.getModelConfigs().subscribe((configs) => {
      this.modelConfigsCache = configs;
      applyConfig(configs);
    });
  }

  private updateTextFieldVisibility(): void {
    this.showTextInFields = this.supportsTextIn;
    this.showTextOutFields = this.supportsTextOut;
  }

  public isCapabilityEnabled(capability: string): boolean {
    return Boolean(
      this.modelConfig?.capabilities.some((c) => c.name === capability),
    );
  }

  public isCustomCapabilityEnabled(capability: string): boolean {
    return this.loadedModel?.customCapabilities().has(capability) ?? false;
  }

  public onParameterValuesChange(values: Record<string, string | null>): void {
    this.node.parameterValues.set(values);
    this.ensureTypeSchemaNamesLoaded();
    this.ensureToolDisplayNamesLoaded();
  }

  public get structuredOutputMode(): string {
    const values = this.node.parameterValues();
    return values['structured_output'] ?? values['structuredOutput'] ?? '';
  }

  public get isSchemaMode(): boolean {
    return this.structuredOutputMode === 'Schema';
  }

  public get isConfiguredTypeMode(): boolean {
    return this.structuredOutputMode === 'Configured Type';
  }

  public onStructuredSchemaChange(value: string): void {
    this.node.parameterValues.update((v) => ({
      ...v,
      structured_output_schema: value ?? '',
    }));
  }

  public onStructuredSchemaNameChange(value: string): void {
    this.node.parameterValues.update((v) => ({
      ...v,
      structured_output_schema_name: value ?? '',
    }));
  }

  public onStructuredSchemaDescriptionChange(value: string): void {
    this.node.parameterValues.update((v) => ({
      ...v,
      structured_output_schema_description: value ?? '',
    }));
  }

  public onStructuredSchemaTypeChange(value: string | null): void {
    this.node.parameterValues.update((v) => ({
      ...v,
      structured_output_configured_type: value ?? '',
    }));
  }

  private syncCallParameterValues(): void {
    if (!this.modelConfig) {
      return;
    }

    const currentValues = this.node.parameterValues();
    const nextValues = this.buildParameterValuesForConfig(
      this.modelConfig,
      currentValues,
    );
    this.node.parameterValues.set(nextValues);
    this.ensureTypeSchemaNamesLoaded();
  }

  private buildParameterValuesForConfig(
    config: ModelConfig,
    previousValues: Record<string, string | null>,
  ): Record<string, string | null> {
    const next: Record<string, string | null> = { ...previousValues };

    config.parameterFields.forEach((field) => {
      if (!field.callDefined) {
        return;
      }

      const capabilityOk =
        !field.capability ||
        (this.isCapabilityEnabled(field.capability) &&
          (!config.isCustom ||
            this.isCustomCapabilityEnabled(field.capability)));

      if (!capabilityOk) {
        return;
      }

      if (field.name in previousValues) {
        next[field.name] = this.applyFieldConstraints(
          field,
          previousValues[field.name],
        );
      } else if (
        field.defaultValue === null ||
        field.defaultValue === undefined
      ) {
        next[field.name] = null;
      } else {
        next[field.name] = String(field.defaultValue);
      }
    });

    return next;
  }

  private applyFieldConstraints(
    field: FieldDescriptor,
    value: string | null,
  ): string | null {
    if (value === null) {
      return null;
    }

    const isNumericField =
      field.type === FieldDescriptorType.Integer ||
      field.type === FieldDescriptorType.Double;
    if (!isNumericField) {
      return value;
    }

    let numeric = Number(value);
    if (!Number.isFinite(numeric)) {
      return value;
    }

    if (field.type === FieldDescriptorType.Integer) {
      numeric = Math.trunc(numeric);
    }

    if (field.min != null && numeric < field.min) {
      numeric = field.min;
    }

    if (field.max != null && numeric > field.max) {
      numeric = field.max;
    }

    return numeric.toString();
  }

  public get supportsTextIn(): boolean {
    return this.hasCapability('SupportsTextIn');
  }

  public get supportsTextOut(): boolean {
    return this.hasCapability('SupportsTextOut');
  }

  public get supportsImageIn(): boolean {
    return this.hasCapability('SupportsImageIn');
  }

  public get supportsImageOut(): boolean {
    return this.hasCapability('SupportsImageOut');
  }

  public get supportsToolCalling(): boolean {
    return this.hasCapability('SupportsToolCalling');
  }

  public get supportsStructuredOutput(): boolean {
    return this.hasCapability('SupportsStructuredOutput');
  }

  private hasCapability(capabilityName: string): boolean {
    if (!this.modelConfig) {
      return false;
    }

    const hasCapability = this.modelConfig.capabilities.some(
      (c) => c.name === capabilityName,
    );
    if (!hasCapability) {
      return false;
    }

    if (!this.modelConfig.isCustom) {
      return true;
    }

    return this.loadedModel?.customCapabilities().has(capabilityName) ?? false;
  }

  private ensureTypeSchemaNamesLoaded(): void {
    if (!this.isConfiguredTypeMode) {
      return;
    }

    if (this.typeSchemaNamesLoaded) {
      return;
    }

    this.typeSchemaNamesLoaded = true;
    this.serverRepository.getSchemaTypeNames().subscribe((names) => {
      this.typeSchemaNames = names ?? [];
    });
  }

  private ensureToolDisplayNamesLoaded(): void {
    if (!this.supportsToolCalling) {
      return;
    }

    if (this.toolDisplayNamesLoaded) {
      return;
    }

    this.toolDisplayNamesLoaded = true;
    this.serverRepository.getToolDisplayNames().subscribe((names) => {
      this.toolDisplayNames = names ?? [];
    });
  }

  public isToolSelected(toolName: string): boolean {
    return this.getSelectedTools().has(toolName);
  }

  public onToolSelectionChange(toolName: string, selected: boolean): void {
    const selectedTools = this.getSelectedTools();
    if (selected) {
      selectedTools.add(toolName);
    } else {
      selectedTools.delete(toolName);
    }

    const ordered = this.toolDisplayNames.filter((name) =>
      selectedTools.has(name),
    );
    const value = ordered.join(',');

    this.node.parameterValues.update((v) => ({
      ...v,
      selected_tools: value,
    }));
  }

  public toolId(toolName: string): string {
    return `tool-${toolName.replace(/[^a-zA-Z0-9_-]/g, '-')}`;
  }

  private getSelectedTools(): Set<string> {
    const raw = this.node.parameterValues()['selected_tools'] ?? '';
    const parts = raw
      .split(',')
      .map((p) => p.trim())
      .filter((p) => p.length > 0);
    return new Set(parts);
  }

  private refreshTabs(): void {
    const newTabs: TabItem[] = [
      { id: 'details', title: 'Details', content: this.detailsTab },
    ];

    if (this.supportsTextIn || this.supportsTextOut) {
      newTabs.push({ id: 'text', title: 'Text', content: this.textTab });
    }

    if (this.supportsImageIn || this.supportsImageOut) {
      newTabs.push({ id: 'image', title: 'Image', content: this.imageTab });
    }

    newTabs.push({ id: 'chat', title: 'Chat', content: this.chatTab });

    if (this.supportsToolCalling) {
      this.ensureToolDisplayNamesLoaded();
      newTabs.push({
        id: 'tool-calling',
        title: 'Tool Calling',
        content: this.toolCallingTab,
      });
    }

    if (this.supportsStructuredOutput) {
      newTabs.push({
        id: 'structured',
        title: 'Structured Output',
        content: this.structuredTab,
      });
    }

    newTabs.push(
      { id: 'inputs', title: 'Inputs', content: this.inputsTab },
      { id: 'outputs', title: 'Outputs', content: this.outputsTab },
    );

    this.tabs = newTabs;

    const hasActive = newTabs.some((t) => t.id === this.activeTabId);
    if (!hasActive) {
      this.activeTabId = 'details';
    }
  }
}

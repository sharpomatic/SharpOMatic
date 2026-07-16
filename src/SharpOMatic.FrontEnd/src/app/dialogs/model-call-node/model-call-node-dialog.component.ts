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
import {
  ModelCallModelSnapshot,
  ModelCallNodeEntity,
} from '../../entities/definitions/model-call-node.entity';
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
import {
  buildModelCostSummary,
  buildModelContextSummary,
  buildModelInformationEntries,
  ModelInformationDisplayEntry,
} from '../../helper/model-information-display';
import { ModelPickerComponent } from '../../components/model-picker/model-picker.component';
import { ModelPickerOption } from '../../components/model-picker/model-picker-option';
import { forkJoin } from 'rxjs';
import { ModelCallToolAgUiOutputMode } from '../../entities/enumerations/model-call-tool-ag-ui-output-mode';
import { BsModalService } from 'ngx-bootstrap/modal';
import { ConfirmDialogComponent } from '../confirm/confirm-dialog.component';

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
    ModelPickerComponent,
  ],
  templateUrl: './model-call-node-dialog.component.html',
  styleUrls: ['./model-call-node-dialog.component.scss'],
  providers: [BsModalService],
})
export class ModelCallNodeDialogComponent implements OnInit {
  @Output() close = new EventEmitter<void>();
  @ViewChild('detailsTab', { static: true }) detailsTab!: TemplateRef<unknown>;
  @ViewChild('streamTab', { static: true }) streamTab!: TemplateRef<unknown>;
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
  public modelPickerOptions: ModelPickerOption[] = [];
  public selectedModelId: string | null = null;
  public showTextInFields = false;
  public showTextOutFields = false;
  public structuredSchemaEditorOptions = MonacoService.editorOptionsJson;
  public typeSchemaNames: string[] = [];
  public toolDisplayNames: string[] = [];
  public readonly toolAgUiOutputModeOptions = [
    { value: ModelCallToolAgUiOutputMode.Inherit, label: 'Inherit' },
    { value: ModelCallToolAgUiOutputMode.Always, label: 'Always' },
    { value: ModelCallToolAgUiOutputMode.Never, label: 'Never' },
  ];
  public informationEntries(): ModelInformationDisplayEntry[] {
    return buildModelInformationEntries(this.modelConfig?.information);
  }

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
  private readonly modelDetailsCache = new Map<string, Model>();
  private modelConfigsCache: ModelConfig[] = [];
  private typeSchemaNamesLoaded = false;
  private toolDisplayNamesLoaded = false;

  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly modalService = inject(BsModalService);

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
      this.refreshTabs();
      return;
    }

    const summary = this.availableModels.find(
      (model) => model.modelId === modelId,
    );
    this.node.modelId.set(summary?.modelId ?? modelId);
    this.loadModel(modelId);
  }

  public fallbackModels(): ModelCallModelSnapshot[] {
    return this.node.fallbackModels();
  }

  public addFallbackModel(): void {
    if (!this.node.modelId()) {
      return;
    }

    const usedIds = new Set(
      this.node.toSnapshot().models.map((model) => model.modelId),
    );
    const candidate = this.availableModels.find(
      (model) => !usedIds.has(model.modelId),
    );
    if (!candidate) {
      return;
    }

    this.node.setFallbackModels([
      ...this.fallbackModels(),
      {
        modelId: candidate.modelId,
        parameterValues: this.buildFallbackParameterValues(
          candidate.modelId,
          {},
        ),
      },
    ]);
  }

  public onFallbackModelSelectionChange(
    index: number,
    modelId: string | null,
  ): void {
    if (!modelId) {
      this.removeFallbackModel(index);
      return;
    }

    const fallbacks = this.fallbackModels();
    const previous = fallbacks[index];
    fallbacks[index] = {
      modelId,
      ...(previous?.disabled ? { disabled: true } : {}),
      parameterValues: this.buildFallbackParameterValues(
        modelId,
        previous?.modelId === modelId ? previous.parameterValues : {},
      ),
    };
    this.node.setFallbackModels(fallbacks);
  }

  public toggleModelDisabled(modelIndex: number): void {
    this.node.setModelDisabled(
      modelIndex,
      !this.node.modelDisabled(modelIndex),
    );
  }

  public onFallbackParameterValuesChange(
    index: number,
    values: Record<string, string | null>,
  ): void {
    const fallbacks = this.fallbackModels();
    const fallback = fallbacks[index];
    if (!fallback) {
      return;
    }

    fallbacks[index] = { ...fallback, parameterValues: { ...values } };
    this.node.setFallbackModels(fallbacks);
  }

  public removeFallbackModel(index: number): void {
    const fallbacks = this.fallbackModels();
    fallbacks.splice(index, 1);
    this.node.setFallbackModels(fallbacks);
  }

  public confirmRemoveFallbackModel(index: number): void {
    const fallback = this.fallbackModels()[index];
    if (!fallback) {
      return;
    }

    const modelName =
      this.availableModels.find((model) => model.modelId === fallback.modelId)
        ?.name ?? `Fallback Model ${index + 1}`;
    const modalRef = this.modalService.show(ConfirmDialogComponent, {
      initialState: {
        title: 'Delete Fallback Model',
        message: `Are you sure you want to delete the fallback model '${modelName}'?`,
      },
    });

    modalRef.onHidden?.subscribe(() => {
      if (modalRef.content?.result) {
        this.removeFallbackModel(index);
      }
    });
  }

  public fallbackModelConfig(modelId: string): ModelConfig | null {
    const model = this.modelDetailsCache.get(modelId);
    return model
      ? (this.modelConfigsCache.find(
          (config) => config.configId === model.configId(),
        ) ?? null)
      : null;
  }

  public fallbackInformationEntries(
    modelId: string,
  ): ModelInformationDisplayEntry[] {
    const fallbackInformation = this.fallbackModelConfig(modelId)?.information;
    if (!fallbackInformation || !this.modelConfig) {
      return [];
    }

    const primaryInformationNames = new Set(
      this.modelConfig.information.map((entry) => entry.name),
    );
    const matchingInformation = fallbackInformation.filter((entry) =>
      primaryInformationNames.has(entry.name),
    );

    return buildModelInformationEntries(matchingInformation);
  }

  public fallbackCapabilityContext(
    modelId: string,
  ): DynamicFieldsCapabilityContext | null {
    const model = this.modelDetailsCache.get(modelId);
    const config = this.fallbackModelConfig(modelId);
    if (!model || !config) {
      return null;
    }

    return {
      capabilities: config.capabilities,
      isCustom: config.isCustom,
      customCapabilities: model.customCapabilities(),
    };
  }

  public fallbackCompatibilityMessages(modelId: string): string[] {
    const primaryModel = this.loadedModel;
    const fallbackModel = this.modelDetailsCache.get(modelId);
    const fallbackConfig = this.fallbackModelConfig(modelId);
    if (!primaryModel || !fallbackModel || !fallbackConfig) {
      return ['Model details are not available.'];
    }

    const messages: string[] = [];
    if (
      !this.modelSupportsCapability(
        fallbackModel,
        fallbackConfig,
        'SupportsTextIn',
      )
    ) {
      messages.push('The fallback model does not support text input.');
    }

    if (
      this.supportsImageIn &&
      this.node.imageInputPath().trim() &&
      !this.modelSupportsCapability(
        fallbackModel,
        fallbackConfig,
        'SupportsImageIn',
      )
    ) {
      messages.push(
        'The configured image input is not supported by the fallback model.',
      );
    }

    if (this.getSelectedTools().size) {
      if (
        !this.modelSupportsCapability(
          fallbackModel,
          fallbackConfig,
          'SupportsToolCalling',
        )
      ) {
        messages.push(
          'The selected tools require tool calling, which is not supported by the fallback model.',
        );
      } else {
        messages.push(
          ...this.capabilitySelectionWarnings(
            fallbackConfig,
            'SupportsToolCalling',
          ),
        );
      }
    }

    const structuredOutput = this.structuredOutputMode;
    if (structuredOutput && structuredOutput !== 'Text') {
      if (
        !this.modelSupportsCapability(
          fallbackModel,
          fallbackConfig,
          'SupportsStructuredOutput',
        )
      ) {
        messages.push(
          `Structured output type '${structuredOutput}' is not supported by the fallback model.`,
        );
      } else {
        messages.push(
          ...this.capabilitySelectionWarnings(
            fallbackConfig,
            'SupportsStructuredOutput',
          ),
        );
      }
    }

    return messages;
  }

  private modelSupportsCapability(
    model: Model,
    config: ModelConfig,
    capability: string,
  ): boolean {
    return (
      config.capabilities.some((item) => item.name === capability) &&
      (!config.isCustom || model.customCapabilities().has(capability))
    );
  }

  private capabilitySelectionWarnings(
    fallbackConfig: ModelConfig,
    capability: string,
  ): string[] {
    if (!this.modelConfig) {
      return [];
    }

    const values = this.node.parameterValues();
    const fallbackFields = new Map(
      fallbackConfig.parameterFields
        .filter((field) => field.capability === capability)
        .map((field) => [field.name, field] as const),
    );

    return this.modelConfig.parameterFields
      .filter((field) => field.callDefined && field.capability === capability)
      .flatMap((primaryField) => {
        const value = values[primaryField.name];
        if (value === null || value === undefined || value === '') {
          return [];
        }

        const fallbackField = fallbackFields.get(primaryField.name);
        if (!fallbackField) {
          return [
            `${primaryField.label} is configured but is not supported by the fallback model.`,
          ];
        }

        if (fallbackField.type !== primaryField.type) {
          return [
            `${primaryField.label} value '${value}' is not supported by the fallback model.`,
          ];
        }

        if (
          fallbackField.enumOptions?.length &&
          !fallbackField.enumOptions.includes(value)
        ) {
          return [
            `${primaryField.label} value '${value}' is not supported by the fallback model.`,
          ];
        }

        const numericValue = Number(value);
        if (
          Number.isFinite(numericValue) &&
          ((fallbackField.min !== null && numericValue < fallbackField.min) ||
            (fallbackField.max !== null && numericValue > fallbackField.max))
        ) {
          return [
            `${primaryField.label} value '${value}' is outside the range supported by the fallback model.`,
          ];
        }

        return [];
      });
  }

  private buildFallbackParameterValues(
    modelId: string,
    previousValues: Record<string, string | null>,
  ): Record<string, string | null> {
    const model = this.modelDetailsCache.get(modelId);
    const config = this.fallbackModelConfig(modelId);
    if (!model || !config) {
      return { ...previousValues };
    }

    const next = { ...previousValues };
    config.parameterFields.forEach((field) => {
      if (!field.callDefined) {
        return;
      }
      if (
        field.capability &&
        config.isCustom &&
        !model.customCapabilities().has(field.capability)
      ) {
        return;
      }

      if (field.name in previousValues) {
        next[field.name] = this.applyFieldConstraints(
          field,
          previousValues[field.name],
        );
      } else {
        next[field.name] =
          field.defaultValue === null || field.defaultValue === undefined
            ? null
            : String(field.defaultValue);
      }
    });
    return next;
  }

  private loadAvailableModels(): void {
    forkJoin({
      models: this.serverRepository.getModelSummaries(),
      configs: this.serverRepository.getModelConfigs(),
    }).subscribe(({ models, configs }) => {
      this.availableModels = models;
      this.modelConfigsCache = configs;
      this.modelDetailsCache.clear();
      this.refreshModelPickerOptions();

      if (!models.length) {
        this.syncSelectedModel();
        return;
      }

      forkJoin(
        models.map((model) => this.serverRepository.getModel(model.modelId)),
      ).subscribe((details) => {
        details.forEach((detail) => {
          if (detail) {
            this.modelDetailsCache.set(detail.modelId, detail);
          }
        });
        this.refreshModelPickerOptions();
        this.syncSelectedModel();
      });
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
    const cached = this.modelDetailsCache.get(modelId) ?? null;
    if (cached) {
      this.applyLoadedModel(cached);
      return;
    }

    this.serverRepository.getModel(modelId).subscribe((model) => {
      if (model) {
        this.modelDetailsCache.set(model.modelId, model);
        this.refreshModelPickerOptions();
      }
      this.applyLoadedModel(model);
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

  private applyLoadedModel(model: Model | null): void {
    this.loadedModel = model;
    this.showTextInFields = false;

    if (!model) {
      this.refreshTabs();
      return;
    }

    this.node.modelId.set(model.modelId);
    this.loadModelConfig(model.configId());
  }

  private refreshModelPickerOptions(): void {
    const configsById = new Map(
      this.modelConfigsCache.map(
        (config) => [config.configId, config] as const,
      ),
    );

    this.modelPickerOptions = this.availableModels.map((model) => {
      const detail = this.modelDetailsCache.get(model.modelId) ?? null;
      const config = detail
        ? (configsById.get(detail.configId()) ?? null)
        : null;
      return {
        id: model.modelId,
        label: model.name,
        costSummary: config ? buildModelCostSummary(config.information) : null,
        contextSummary: config
          ? buildModelContextSummary(config.information)
          : null,
      };
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
      field.type === FieldDescriptorType.Double ||
      field.type === FieldDescriptorType.Currency;
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
      this.removeToolAgUiOutputMode(toolName);
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

  public getToolAgUiOutputMode(toolName: string): ModelCallToolAgUiOutputMode {
    return (
      this.node.toolAgUiOutputModes()[toolName] ??
      ModelCallToolAgUiOutputMode.Inherit
    );
  }

  public onToolAgUiOutputModeChange(
    toolName: string,
    mode: ModelCallToolAgUiOutputMode | string,
  ): void {
    const selectedMode =
      typeof mode === 'string'
        ? (Number(mode) as ModelCallToolAgUiOutputMode)
        : mode;

    this.node.toolAgUiOutputModes.update((current) => {
      const next = { ...current };
      if (selectedMode === ModelCallToolAgUiOutputMode.Inherit) {
        delete next[toolName];
      } else {
        next[toolName] = selectedMode;
      }

      return next;
    });
  }

  public toolId(toolName: string): string {
    return `tool-${toolName.replace(/[^a-zA-Z0-9_-]/g, '-')}`;
  }

  public toolAgUiOutputId(toolName: string): string {
    return `tool-ag-ui-output-${toolName.replace(/[^a-zA-Z0-9_-]/g, '-')}`;
  }

  private getSelectedTools(): Set<string> {
    const raw = this.node.parameterValues()['selected_tools'] ?? '';
    const parts = raw
      .split(',')
      .map((p) => p.trim())
      .filter((p) => p.length > 0);
    return new Set(parts);
  }

  private removeToolAgUiOutputMode(toolName: string): void {
    this.node.toolAgUiOutputModes.update((current) => {
      if (!(toolName in current)) {
        return current;
      }

      const next = { ...current };
      delete next[toolName];
      return next;
    });
  }

  private refreshTabs(): void {
    const newTabs: TabItem[] = [
      { id: 'details', title: 'Details', content: this.detailsTab },
    ];

    if (this.supportsTextIn || this.supportsTextOut) {
      newTabs.push({ id: 'text', title: 'Text', content: this.textTab });
    }

    if (this.supportsImageIn || this.supportsImageOut) {
      newTabs.push({ id: 'image', title: 'File', content: this.imageTab });
    }

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
      { id: 'chat', title: 'Chat', content: this.chatTab },
      { id: 'stream', title: 'AG-UI', content: this.streamTab },
    );

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

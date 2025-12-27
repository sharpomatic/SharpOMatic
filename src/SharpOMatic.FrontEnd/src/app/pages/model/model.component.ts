import { CommonModule } from '@angular/common';
import { Component, HostListener, OnInit, Signal, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ConnectorSummary } from '../../metadata/definitions/connector-summary';
import { FieldDescriptor } from '../../metadata/definitions/field-descriptor';
import { Model } from '../../metadata/definitions/model';
import { ModelCapability } from '../../metadata/definitions/model-capability';
import { ModelConfig } from '../../metadata/definitions/model-config';
import { MetadataService } from '../../services/metadata.service';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { Connector } from '../../metadata/definitions/connector';
import { CanLeaveWithUnsavedChanges } from '../../guards/unsaved-changes.guard';
import { Observable, map } from 'rxjs';
import { DynamicFieldsCapabilityContext, DynamicFieldsComponent } from '../../components/dynamic-fields/dynamic-fields.component';
import { FieldDescriptorType } from '../../metadata/enumerations/field-descriptor-type';

@Component({
  selector: 'app-model',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DynamicFieldsComponent,
  ],
  templateUrl: './model.component.html',
  styleUrls: ['./model.component.scss'],
})
export class ModelComponent implements OnInit, CanLeaveWithUnsavedChanges {
  private readonly route = inject(ActivatedRoute);
  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly metadataService = inject(MetadataService);

  public model: Model = new Model(Model.defaultSnapshot());
  public modelConfig: ModelConfig | null = null;
  public readonly modelConfigs = this.metadataService.modelConfigs;
  private readonly connectorConfigId = signal<string | null>(null);
  private readonly modelVersion = signal(0);
  public readonly availableModelConfigs: Signal<ModelConfig[]>;
  public connectorSummaries: ConnectorSummary[] = [];

  constructor() {
    this.availableModelConfigs = computed(() => {
      const configId = this.connectorConfigId();
      if (!configId) {
        return [];
      }

      return this.modelConfigs()
        .filter(cfg => cfg.connectorConfigId === configId)
        .slice()
        .sort((a, b) => {
          if (a.isCustom && !b.isCustom) {
            return 1;
          }
          if (!a.isCustom && b.isCustom) {
            return -1;
          }
          return a.displayName.localeCompare(b.displayName);
        });
    });

    effect(() => {
      this.modelVersion();
      const connectorId = this.model.connectorId();
      this.loadConnectorConfig(connectorId);
    });

    effect(() => {
      this.modelVersion();
      const configId = this.model.configId();
      const configs = this.availableModelConfigs();
      if (!configs.length) {
        return;
      }

      if (configId && configs.length) {
        if (!this.modelConfig || this.modelConfig.configId !== configId) {
          this.setModelConfig(configId, false);
        }
      } else {
        this.modelConfig = null;
      }
    });

    effect(() => {
      this.modelVersion();
      const configs = this.availableModelConfigs();
      const currentConfigId = this.model.configId();
      if (!configs.length) {
        return;
      }

      const isValid = configs.some(c => c.configId === currentConfigId);
      if (!isValid) {
        this.onModelConfigChange(configs[0].configId);
      }
    });

  }

  ngOnInit(): void {
    this.serverRepository.getConnectorSummaries().subscribe(connectors => {
      this.connectorSummaries = [...connectors].sort((a, b) => a.name.localeCompare(b.name));
    });

    const modelId = this.route.snapshot.paramMap.get('id');
    if (modelId) {
      this.serverRepository.getModel(modelId).subscribe(model => {
        if (model) {
          this.model = model;
          this.setModelConfig(model.configId(), false);
          this.modelVersion.update(v => v + 1);
        }
      });
    }
  }

  save(): void {
    this.saveChanges().subscribe();
  }

  public onModelConfigChange(configId: string): void {
    this.setModelConfig(configId, true);
    this.setNameFromSelectedConfig();
  }

  public get parameterValuesRecord(): Record<string, string | null> {
    return this.mapToRecord(this.model.parameterValues());
  }

  public get capabilityContext(): DynamicFieldsCapabilityContext | null {
    if (!this.modelConfig) {
      return null;
    }

    return {
      capabilities: this.modelConfig.capabilities,
      isCustom: this.modelConfig.isCustom,
      customCapabilities: this.model.customCapabilities(),
    };
  }

  public onParameterValuesChange(values: Record<string, string | null>): void {
    this.model.parameterValues.set(this.recordToMap(values));
  }

  public capabilityEntries(): { capability: ModelCapability; selected: boolean }[] {
    if (!this.modelConfig) {
      return [];
    }

    return this.modelConfig.capabilities.map(capability => ({
      capability,
      selected: this.isCustomCapabilityEnabled(capability.name),
    }));
  }

  public onCapabilityChange(capability: string, enabled: boolean): void {
    this.model.customCapabilities.update(set => {
      const next = new Set(set);
      if (enabled) {
        next.add(capability);
      } else {
        next.delete(capability);
      }
      return next;
    });
  }

  public isCapabilityEnabled(capability: string): boolean {
    return Boolean(this.modelConfig?.capabilities.some(c => c.name === capability));
  }

  public isCustomCapabilityEnabled(capability: string): boolean {
    return this.model.customCapabilities().has(capability);
  }

  private setModelConfig(configId: string, resetValues: boolean): void {
    if (!configId) {
      this.modelConfig = null;
      this.model.configId.set('');
      if (resetValues) {
        this.model.parameterValues.set(new Map());
        this.model.customCapabilities.set(new Set());
      }
      return;
    }

    const previousValues = new Map(this.model.parameterValues());

    const availableConfigs = this.availableModelConfigs();
    const searchConfigs = availableConfigs.length ? availableConfigs : this.modelConfigs();
    this.modelConfig = searchConfigs.find(config => config.configId === configId) ?? null;
    this.model.configId.set(this.modelConfig?.configId ?? (availableConfigs.length ? '' : configId));

    if (resetValues && this.modelConfig) {
      const capabilityNames = this.modelConfig.capabilities.map(c => c.name);
      this.model.customCapabilities.set(new Set(capabilityNames));
      const nextValues = this.buildParameterValuesForConfig(this.modelConfig, previousValues);
      this.model.parameterValues.set(nextValues);
    }
  }

  private setNameFromSelectedConfig(): void {
    if (!this.modelConfig) {
      return;
    }

    const currentName = this.model.name().trim();
    if (!currentName || currentName === 'Untitled') {
      this.model.name.set(this.modelConfig.displayName);
    }
  }

  private buildParameterValuesForConfig(config: ModelConfig, previousValues: Map<string, string | null>): Map<string, string | null> {
    // Start with all previous values so switching away and back preserves user input
    const next = new Map<string, string | null>(previousValues);
    config.parameterFields.forEach(field => {
      const capabilityOk = !field.capability || (this.isCapabilityEnabled(field.capability) && (!config.isCustom || this.isCustomCapabilityEnabled(field.capability)));
      if (!capabilityOk) {
        return;
      }

      if (previousValues.has(field.name)) {
        const constrained = this.applyFieldConstraints(field, previousValues.get(field.name) ?? null);
        next.set(field.name, constrained);
      } else if (field.defaultValue === null || field.defaultValue === undefined) {
        next.set(field.name, null);
      } else {
        next.set(field.name, String(field.defaultValue));
      }
    });
    return next;
  }

  private applyFieldConstraints(field: FieldDescriptor, value: string | null): string | null {
    if (value === null) {
      return null;
    }

    const isNumericField = field.type === FieldDescriptorType.Integer || field.type === FieldDescriptorType.Double;
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

  private loadConnectorConfig(connectorId: string | null): void {
    if (!connectorId) {
      this.connectorConfigId.set(null);
      return;
    }

    this.serverRepository.getConnector(connectorId).subscribe((connector: Connector | null) => {
      this.connectorConfigId.set(connector?.configId() ?? null);
    });
  }

  private mapToRecord(values: Map<string, string | null>): Record<string, string | null> {
    const entries = Array.from(values.entries()).map(([key, value]) => [key, value ?? null] as const);
    return Object.fromEntries(entries);
  }

  private recordToMap(values: Record<string, string | null>): Map<string, string | null> {
    const entries = Object.entries(values ?? {}).map(([key, value]) => [key, value ?? null] as const);
    return new Map(entries);
  }

  hasUnsavedChanges(): boolean {
    return this.model.isDirty();
  }

  saveChanges(): Observable<void> {
    return this.serverRepository.upsertModel(this.model).pipe(
      map(() => {
        this.model?.markClean();
        return;
      })
    );
  }

  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.hasUnsavedChanges()) {
      event.preventDefault();
      event.returnValue = '';
    }
  }
}

import { CommonModule } from '@angular/common';
import { Component, OnInit, Signal, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ConnectionSummary } from '../../metadata/definitions/connection summary';
import { FieldDescriptor } from '../../metadata/definitions/field-descriptor';
import { Model } from '../../metadata/definitions/model';
import { ModelCapabilities, ModelCapabilitiesSnapshot } from '../../metadata/definitions/model-capabilities';
import { ModelConfig } from '../../metadata/definitions/model-config';
import { FieldDescriptorType } from '../../metadata/enumerations/field-descriptor-type';
import { MetadataService } from '../../services/metadata.service';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { Connection } from '../../metadata/definitions/connection';

@Component({
  selector: 'app-model',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
  ],
  templateUrl: './model.component.html',
  styleUrls: ['./model.component.scss'],
})
export class ModelComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly metadataService = inject(MetadataService);

  public model: Model = new Model(Model.defaultSnapshot());
  public modelConfig: ModelConfig | null = null;
  public readonly modelConfigs = this.metadataService.modelConfigs;
  private readonly connectionConfigId = signal<string | null>(null);
  private readonly modelVersion = signal(0);
  public readonly availableModelConfigs: Signal<ModelConfig[]>;
  public connectionSummaries: ConnectionSummary[] = [];
  public readonly fieldDescriptorType = FieldDescriptorType;

  constructor() {
    this.availableModelConfigs = computed(() => {
      const configId = this.connectionConfigId();
      if (!configId) {
        return [];
      }

      return this.modelConfigs()
        .filter(cfg => cfg.connectionConfigId === configId)
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
      const connectionId = this.model.connectionId();
      this.loadConnectionConfig(connectionId);
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
    this.serverRepository.getConnectionSummaries().subscribe(connections => {
      this.connectionSummaries = [...connections].sort((a, b) => a.name.localeCompare(b.name));
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
    this.serverRepository.upsertModel(this.model)
      .subscribe(() => {
        this.model?.markClean();
    });
  }

  public onModelConfigChange(configId: string): void {
    this.setModelConfig(configId, true);
  }

  public capabilityEntries(): { key: keyof ModelCapabilitiesSnapshot; label: string; value: boolean }[] {
    if (!this.modelConfig) {
      return [];
    }

    const configCaps = this.modelConfig.capabilities.toSnapshot();
    const customCaps = this.model.customCapabilities().toSnapshot();

    return Object.entries(configCaps)
      .filter(([, enabled]) => Boolean(enabled))
      .map(([key]) => ({
        key: key as keyof ModelCapabilitiesSnapshot,
        label: this.formatCapabilityLabel(key),
        value: Boolean(customCaps[key as keyof ModelCapabilitiesSnapshot]),
      }));
  }

  public onCapabilityChange(capability: keyof ModelCapabilitiesSnapshot, enabled: boolean): void {
    const snapshot = this.model.customCapabilities().toSnapshot();
    const updated = { ...snapshot, [capability]: enabled } as ModelCapabilitiesSnapshot;
    this.model.customCapabilities.set(ModelCapabilities.fromSnapshot(updated));
  }

  public isCapabilityEnabled(capability: string): boolean {
    if (!this.modelConfig) {
      return false;
    }
    const caps = this.modelConfig.capabilities.toSnapshot();
    const key = capability as keyof ModelCapabilitiesSnapshot;
    const record = caps as Record<keyof ModelCapabilitiesSnapshot, boolean | undefined>;
    return Boolean(record[key]);
  }

  public isCustomCapabilityEnabled(capability: string): boolean {
    const caps = this.model.customCapabilities().toSnapshot();
    const key = capability as keyof ModelCapabilitiesSnapshot;
    const record = caps as Record<keyof ModelCapabilitiesSnapshot, boolean | undefined>;
    return Boolean(record[key]);
  }

  private formatCapabilityLabel(key: string): string {
    return key
      .replace(/([A-Z])/g, ' $1')
      .replace(/^./, c => c.toUpperCase())
      .trim();
  }

  public getParameterValue(field: FieldDescriptor): string {
    const value = this.getResolvedParameterValue(field);
    return value ?? '';
  }

  public onParameterValueChange(field: FieldDescriptor, value: string): void {
    this.model.parameterValues.update(map => {
      const next = new Map(map);
      next.set(field.name, value === '' ? null : value ?? '');
      return next;
    });
  }

  public onParameterStringBlur(field: FieldDescriptor, rawValue: string | null): void {
    if (field.type === FieldDescriptorType.Secret) {
      return;
    }

    if (rawValue !== '') {
      return;
    }

    if (field.isRequired && field.defaultValue != null) {
      this.model.parameterValues.update(map => {
        const next = new Map(map);
        next.set(field.name, String(field.defaultValue));
        return next;
      });
    }
  }

  public getParameterNumericValue(field: FieldDescriptor): string {
    const value = this.getResolvedParameterValue(field);
    return value ?? '';
  }

  public onParameterNumericChange(field: FieldDescriptor, value: string | number): void {
    this.model.parameterValues.update(map => {
      const next = new Map(map);
      if (value === '' || value === null || value === undefined) {
        next.set(field.name, null);
      } else {
        next.set(field.name, String(value));
      }
      return next;
    });
  }

  public isFieldMissing(field: FieldDescriptor): boolean {
    if (!field.isRequired) {
      return false;
    }

    const value = this.getResolvedParameterValue(field);
    return value === null || value === '';
  }

  public onParameterNumericBlur(field: FieldDescriptor, rawValue: string | null): void {
    if (rawValue === null || rawValue === '') {
      const shouldApplyDefault = field.isRequired && field.defaultValue != null;
      const defaultValue = shouldApplyDefault ? String(field.defaultValue) : null;
      this.model.parameterValues.update(map => {
        const next = new Map(map);
        next.set(field.name, defaultValue);
        return next;
      });
      return;
    }

    let numeric = Number(rawValue);
    if (!Number.isFinite(numeric)) {
      return;
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

    const finalValue = numeric.toString();
    this.model.parameterValues.update(map => {
      const next = new Map(map);
      next.set(field.name, finalValue);
      return next;
    });
  }

  public getParameterBooleanValue(field: FieldDescriptor): boolean {
    const value = this.model.parameterValues().get(field.name);

    if (value != null) {
      return value.toLowerCase() === 'true';
    }

    return field.defaultValue === true;
  }

  public onParameterBooleanChange(field: FieldDescriptor, checked: boolean): void {
    this.model.parameterValues.update(map => {
      const next = new Map(map);
      next.set(field.name, checked ? 'true' : 'false');
      return next;
    });
  }

  private setModelConfig(configId: string, resetValues: boolean): void {
    if (!configId) {
      this.modelConfig = null;
      this.model.configId.set('');
      if (resetValues) {
        this.model.parameterValues.set(new Map());
        this.model.customCapabilities.set(ModelCapabilities.fromSnapshot(ModelCapabilities.defaultSnapshot()));
      }
      return;
    }

    const previousValues = new Map(this.model.parameterValues());

    const availableConfigs = this.availableModelConfigs();
    const searchConfigs = availableConfigs.length ? availableConfigs : this.modelConfigs();
    this.modelConfig = searchConfigs.find(config => config.configId === configId) ?? null;
    this.model.configId.set(this.modelConfig?.configId ?? (availableConfigs.length ? '' : configId));

    if (resetValues && this.modelConfig) {
      this.model.customCapabilities.set(ModelCapabilities.fromSnapshot(this.modelConfig.capabilities.toSnapshot()));
      const nextValues = this.buildParameterValuesForConfig(this.modelConfig, previousValues);
      this.model.parameterValues.set(nextValues);
    }
  }

  private buildParameterValuesForConfig(config: ModelConfig, previousValues: Map<string, string | null>): Map<string, string | null> {
    const next = new Map<string, string | null>();
    config.parameterFields.forEach(field => {
      const capabilityOk = !field.capability || (this.isCapabilityEnabled(field.capability) && (!config.isCustom || this.isCustomCapabilityEnabled(field.capability)));
      if (!capabilityOk) {
        return;
      }

      if (previousValues.has(field.name)) {
        next.set(field.name, previousValues.get(field.name) ?? null);
      } else if (field.defaultValue === null || field.defaultValue === undefined) {
        next.set(field.name, null);
      } else {
        next.set(field.name, String(field.defaultValue));
      }
    });
    return next;
  }

  private getResolvedParameterValue(field: FieldDescriptor): string | null {
    const parameterValues = this.model.parameterValues();
    if (parameterValues.has(field.name)) {
      return parameterValues.get(field.name) ?? null;
    }

    if (field.defaultValue != null) {
      return String(field.defaultValue);
    }

    return null;
  }

  private loadConnectionConfig(connectionId: string | null): void {
    if (!connectionId) {
      this.connectionConfigId.set(null);
      return;
    }

    this.serverRepository.getConnection(connectionId).subscribe((connection: Connection | null) => {
      this.connectionConfigId.set(connection?.configId() ?? null);
    });
  }
}

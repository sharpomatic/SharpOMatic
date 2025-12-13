import { CommonModule } from '@angular/common';
import { Component, HostListener, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Connector } from '../../metadata/definitions/connector';
import { ConnectorConfig } from '../../metadata/definitions/connector-config';
import { FieldDescriptor } from '../../metadata/definitions/field-descriptor';
import { FieldDescriptorType } from '../../metadata/enumerations/field-descriptor-type';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { MetadataService } from '../../services/metadata.service';
import { FormsModule } from '@angular/forms';
import { CanLeaveWithUnsavedChanges } from '../../guards/unsaved-changes.guard';
import { Observable, map } from 'rxjs';

@Component({
  selector: 'app-connector',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule
  ],
  templateUrl: './connector.component.html',
  styleUrls: ['./connector.component.scss'],
})
export class ConnectorComponent implements OnInit, CanLeaveWithUnsavedChanges {
  private readonly route = inject(ActivatedRoute);
  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly metadataService = inject(MetadataService);

  public connector: Connector = new Connector(Connector.defaultSnapshot());
  public connectorConfig: ConnectorConfig | null = null;
  public readonly connectorConfigs = this.metadataService.connectorConfigs;
  public readonly fieldDescriptorType = FieldDescriptorType;

  ngOnInit(): void {
    const connectorId = this.route.snapshot.paramMap.get('id');
    if (connectorId) {
      this.serverRepository.getConnector(connectorId).subscribe(connector => {
        if (connector) {
          this.connector = connector;
          this.setConnectorConfig(connector.configId(), false);
        }
      });
    }
  }

  save(): void {
    this.saveChanges().subscribe();
  }

  public onConnectorConfigChange(configId: string): void {
    this.setConnectorConfig(configId, true);
  }

  private setConnectorConfig(configId: string, resetFieldValues: boolean): void {
    if (!configId) {
      this.connectorConfig = null;
      this.connector.configId.set('');
      this.connector.authenticationModeId.set('');
      if (resetFieldValues) {
        this.connector.fieldValues.set(new Map());
      }
      return;
    }

    const configs = this.connectorConfigs();
    this.connectorConfig = configs.find(config => config.configId === configId) ?? null;
    debugger;
    this.connector.configId.set(this.connectorConfig?.configId ?? '');

    this.ensureAuthMode(resetFieldValues);
  }

  public get selectedAuthMode() {
    const authModeId = this.connector.authenticationModeId();
    return this.connectorConfig?.authModes.find(mode => mode.id === authModeId);
  }

  public getFieldValue(field: FieldDescriptor): string {
    const value = this.getResolvedFieldValue(field);
    return value ?? '';
  }

  public onFieldValueChange(field: FieldDescriptor, value: string): void {
    this.connector.fieldValues.update(map => {
      const next = new Map(map);
      next.set(field.name, value === '' ? null : value ?? '');
      return next;
    });
  }

  public onFieldStringBlur(field: FieldDescriptor, rawValue: string | null): void {
    if (field.type === FieldDescriptorType.Secret) {
      return;
    }

    if (rawValue !== '') {
      return;
    }

    if (field.isRequired && field.defaultValue != null) {
      this.connector.fieldValues.update(map => {
        const next = new Map(map);
        next.set(field.name, String(field.defaultValue));
        return next;
      });
    }
  }

  public getFieldNumericValue(field: FieldDescriptor): string {
    const value = this.getResolvedFieldValue(field);
    return value ?? '';
  }

  public onFieldNumericChange(field: FieldDescriptor, value: string | number): void {
    this.connector.fieldValues.update(map => {
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

    const value = this.getResolvedFieldValue(field);
    return value === null || value === '';
  }

  public onFieldNumericBlur(field: FieldDescriptor, rawValue: string | null): void {
    if (rawValue === null || rawValue === '') {
      const shouldApplyDefault = field.isRequired && field.defaultValue != null;
      const defaultValue = shouldApplyDefault ? String(field.defaultValue) : null;
      this.connector.fieldValues.update(map => {
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
    this.connector.fieldValues.update(map => {
      const next = new Map(map);
      next.set(field.name, finalValue);
      return next;
    });
  }

  private getResolvedFieldValue(field: FieldDescriptor): string | null {
    const fieldValues = this.connector.fieldValues();
    if (fieldValues.has(field.name)) {
      return fieldValues.get(field.name) ?? null;
    }

    if (field.defaultValue != null) {
      return String(field.defaultValue);
    }

    return null;
  }

  public getFieldBooleanValue(field: FieldDescriptor): boolean {
    const value = this.connector.fieldValues().get(field.name);

    if (value != null) {
      return value.toLowerCase() === 'true';
    }

    return field.defaultValue === true;
  }

  public onFieldBooleanChange(field: FieldDescriptor, checked: boolean): void {
    this.connector.fieldValues.update(map => {
      const next = new Map(map);
      next.set(field.name, checked ? 'true' : 'false');
      return next;
    });
  }

  private ensureAuthMode(resetFieldValues: boolean): void {
    const authModes = this.connectorConfig?.authModes ?? [];
    if (!authModes.length) {
      this.connector.authenticationModeId.set('');
      if (resetFieldValues) {
        this.connector.fieldValues.set(new Map());
      }
      return;
    }

    let current = this.connector.authenticationModeId();
    if (!current || !authModes.some(mode => mode.id === current)) {
      current = authModes[0].id;
      this.connector.authenticationModeId.set(current);
    }

    if (resetFieldValues) {
      this.resetFieldsForSelectedAuthMode();
    }
  }

  private resetFieldsForSelectedAuthMode(): void {
    const mode = this.selectedAuthMode;
    if (!mode) {
      this.connector.fieldValues.set(new Map());
      return;
    }

    const next = new Map<string, string | null>();
    mode.fields.forEach(field => {
      if (field.defaultValue === null || field.defaultValue === undefined) {
        next.set(field.name, null);
      } else {
        next.set(field.name, String(field.defaultValue));
      }
    });

    this.connector.fieldValues.set(next);
  }

  hasUnsavedChanges(): boolean {
    return this.connector.isDirty();
  }

  saveChanges(): Observable<void> {
    return this.serverRepository.upsertConnector(this.connector)
      .pipe(
        map(() => {
          this.connector?.markClean();
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

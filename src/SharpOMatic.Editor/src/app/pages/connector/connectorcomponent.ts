import { CommonModule } from '@angular/common';
import { Component, HostListener, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Connector } from '../../metadata/definitions/connector';
import { ConnectorConfig } from '../../metadata/definitions/connector-config';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { MetadataService } from '../../services/metadata.service';
import { FormsModule } from '@angular/forms';
import { CanLeaveWithUnsavedChanges } from '../../guards/unsaved-changes.guard';
import { Observable, map } from 'rxjs';
import { DynamicFieldsComponent } from '../../components/dynamic-fields/dynamic-fields.component';

@Component({
  selector: 'app-connector',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DynamicFieldsComponent,
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
    this.setNameFromSelectedConfig();
  }

  public get connectorFieldValuesRecord(): Record<string, string | null> {
    return this.mapToRecord(this.connector.fieldValues());
  }

  public onConnectorFieldValuesChange(values: Record<string, string | null>): void {
    this.connector.fieldValues.set(this.recordToMap(values));
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
    this.connector.configId.set(this.connectorConfig?.configId ?? '');

    this.ensureAuthMode(resetFieldValues);
  }

  private setNameFromSelectedConfig(): void {
    if (!this.connectorConfig) {
      return;
    }

    const currentName = this.connector.name().trim();
    if (!currentName || currentName === 'Untitled') {
      this.connector.name.set(this.connectorConfig.displayName);
    }
  }

  public get selectedAuthMode() {
    const authModeId = this.connector.authenticationModeId();
    return this.connectorConfig?.authModes.find(mode => mode.id === authModeId);
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

  private mapToRecord(values: Map<string, string | null>): Record<string, string | null> {
    const entries = Array.from(values.entries()).map(([key, value]) => [key, value ?? null] as const);
    return Object.fromEntries(entries);
  }

  private recordToMap(values: Record<string, string | null>): Map<string, string | null> {
    const entries = Object.entries(values ?? {}).map(([key, value]) => [key, value ?? null] as const);
    return new Map(entries);
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

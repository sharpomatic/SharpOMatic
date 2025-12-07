import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Connection } from '../../metadata/definitions/connection';
import { ConnectionConfig } from '../../metadata/definitions/connection-config';
import { FieldDescriptor } from '../../metadata/definitions/field-descriptor';
import { FieldDescriptorType } from '../../metadata/enumerations/field-descriptor-type';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { MetadataService } from '../../services/metadata.service';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-connection',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule
  ],
  templateUrl: './connection.component.html',
  styleUrls: ['./connection.component.scss'],
})
export class ConnectionComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly metadataService = inject(MetadataService);

  public connection: Connection = new Connection(Connection.defaultSnapshot());
  public connectionConfig: ConnectionConfig | null = null;
  public readonly connectionConfigs = this.metadataService.connectionConfigs;
  public readonly fieldDescriptorType = FieldDescriptorType;

  ngOnInit(): void {
    const connectionId = this.route.snapshot.paramMap.get('id');
    if (connectionId) {
      this.serverRepository.getConnection(connectionId).subscribe(connection => {
        if (connection) {
          this.connection = connection;
          this.setConnectionConfig(connection.configId(), false);
        }
      });
    }
  }

  save(): void {
    this.serverRepository.upsertConnection(this.connection)
      .subscribe(() => {
        this.connection?.markClean();
    });
  }

  public onConnectionConfigChange(configId: string): void {
    this.setConnectionConfig(configId, true);
  }

  private setConnectionConfig(configId: string, resetFieldValues: boolean): void {
    if (!configId) {
      this.connectionConfig = null;
      this.connection.configId.set('');
      this.connection.authenticationModeId.set('');
      if (resetFieldValues) {
        this.connection.fieldValues.set(new Map());
      }
      return;
    }

    const configs = this.connectionConfigs();
    this.connectionConfig = configs.find(config => config.configId === configId) ?? null;
    this.connection.configId.set(this.connectionConfig?.configId ?? '');

    this.ensureAuthMode(resetFieldValues);
  }

  public get selectedAuthMode() {
    const authModeId = this.connection.authenticationModeId();
    return this.connectionConfig?.authModes.find(mode => mode.id === authModeId);
  }

  public getFieldValue(field: FieldDescriptor): string {
    const value = this.connection.fieldValues().get(field.name);

    if (value != null) {
      return value;
    }

    if (field.defaultValue != null) {
      return String(field.defaultValue);
    }

    return '';
  }

  public onFieldValueChange(field: FieldDescriptor, value: string): void {
    this.connection.fieldValues.update(map => {
      const next = new Map(map);
      next.set(field.name, value ?? '');
      return next;
    });
  }

  public getFieldBooleanValue(field: FieldDescriptor): boolean {
    const value = this.connection.fieldValues().get(field.name);

    if (value != null) {
      return value.toLowerCase() === 'true';
    }

    return field.defaultValue === true;
  }

  public onFieldBooleanChange(field: FieldDescriptor, checked: boolean): void {
    this.connection.fieldValues.update(map => {
      const next = new Map(map);
      next.set(field.name, checked ? 'true' : 'false');
      return next;
    });
  }

  private ensureAuthMode(resetFieldValues: boolean): void {
    const authModes = this.connectionConfig?.authModes ?? [];
    if (!authModes.length) {
      this.connection.authenticationModeId.set('');
      if (resetFieldValues) {
        this.connection.fieldValues.set(new Map());
      }
      return;
    }

    let current = this.connection.authenticationModeId();
    if (!current || !authModes.some(mode => mode.id === current)) {
      current = authModes[0].id;
      this.connection.authenticationModeId.set(current);
    }

    if (resetFieldValues) {
      this.resetFieldsForSelectedAuthMode();
    }
  }

  private resetFieldsForSelectedAuthMode(): void {
    const mode = this.selectedAuthMode;
    if (!mode) {
      this.connection.fieldValues.set(new Map());
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

    this.connection.fieldValues.set(next);
  }
}

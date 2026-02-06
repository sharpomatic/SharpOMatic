import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FieldDescriptor } from '../../metadata/definitions/field-descriptor';
import { ModelCapability } from '../../metadata/definitions/model-capability';
import { FieldDescriptorType } from '../../metadata/enumerations/field-descriptor-type';

export interface DynamicFieldsCapabilityContext {
  capabilities: ModelCapability[];
  isCustom: boolean;
  customCapabilities?: Set<string>;
}

@Component({
  selector: 'app-dynamic-fields',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './dynamic-fields.component.html',
  styleUrls: ['./dynamic-fields.component.scss'],
})
export class DynamicFieldsComponent {
  @Input() fields: FieldDescriptor[] = [];
  @Input() values: Record<string, string | null> = {};
  @Input() capabilityContext: DynamicFieldsCapabilityContext | null = null;
  @Input() allowedCapabilities: string[] | null = null;
  @Input() ignoredCapabilities: string[] | null = null;
  @Input() includeCallDefined = true;
  @Input() includeNonCallDefined = true;
  @Input() disabled = false;
  @Output() valuesChange = new EventEmitter<Record<string, string | null>>();

  public readonly fieldDescriptorType = FieldDescriptorType;

  public shouldShowField(field: FieldDescriptor): boolean {
    const isCallDefined = field.callDefined === true;

    if (isCallDefined && !this.includeCallDefined) {
      return false;
    }

    if (!isCallDefined && !this.includeNonCallDefined) {
      return false;
    }

    if (this.allowedCapabilities && this.allowedCapabilities.length) {
      if (
        !field.capability ||
        !this.allowedCapabilities.includes(field.capability)
      ) {
        return false;
      }
    }

    if (this.ignoredCapabilities && this.ignoredCapabilities.length) {
      if (
        field.capability &&
        this.ignoredCapabilities.includes(field.capability)
      ) {
        return false;
      }
    }

    if (!field.capability) {
      return true;
    }

    const context = this.capabilityContext;
    if (!context) {
      return true;
    }

    const capabilityExists = context.capabilities.some(
      (c) => c.name === field.capability,
    );
    if (!capabilityExists) {
      return false;
    }

    if (!context.isCustom) {
      return true;
    }

    return context.customCapabilities?.has(field.capability) ?? false;
  }

  public isFieldMissing(field: FieldDescriptor): boolean {
    if (!field.isRequired) {
      return false;
    }

    const value = this.getResolvedValue(field);
    return value === null || value === '';
  }

  public getStringValue(field: FieldDescriptor): string {
    const value = this.getResolvedValue(field);
    return value ?? '';
  }

  public getNumericValue(field: FieldDescriptor): string {
    const value = this.getResolvedValue(field);
    return value ?? '';
  }

  public getBooleanValue(field: FieldDescriptor): boolean {
    const value = this.values?.[field.name];

    if (value != null) {
      return value.toLowerCase() === 'true';
    }

    return field.defaultValue === true;
  }

  public onStringChange(field: FieldDescriptor, value: string): void {
    this.emitValues({
      ...this.values,
      [field.name]: value === '' ? null : (value ?? ''),
    });
  }

  public onStringBlur(field: FieldDescriptor, rawValue: string | null): void {
    if (field.type === FieldDescriptorType.Secret) {
      return;
    }

    if (rawValue !== '') {
      return;
    }

    if (field.isRequired && field.defaultValue != null) {
      this.emitValues({
        ...this.values,
        [field.name]: String(field.defaultValue),
      });
    }
  }

  public onNumericChange(field: FieldDescriptor, value: string | number): void {
    this.emitValues({
      ...this.values,
      [field.name]:
        value === '' || value === null || value === undefined
          ? null
          : String(value),
    });
  }

  public onNumericBlur(field: FieldDescriptor, rawValue: string | null): void {
    if (rawValue === null || rawValue === '') {
      const shouldApplyDefault = field.isRequired && field.defaultValue != null;
      const defaultValue = shouldApplyDefault
        ? String(field.defaultValue)
        : null;
      this.emitValues({
        ...this.values,
        [field.name]: defaultValue,
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
    this.emitValues({
      ...this.values,
      [field.name]: finalValue,
    });
  }

  public onBooleanChange(field: FieldDescriptor, checked: boolean): void {
    this.emitValues({
      ...this.values,
      [field.name]: checked ? 'true' : 'false',
    });
  }

  private getResolvedValue(field: FieldDescriptor): string | null {
    const currentValues = this.values ?? {};
    if (field.name in currentValues) {
      return currentValues[field.name];
    }

    if (field.defaultValue != null) {
      return String(field.defaultValue);
    }

    return null;
  }

  private emitValues(values: Record<string, string | null>): void {
    this.valuesChange.emit(values);
  }
}

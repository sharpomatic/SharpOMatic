import { ModelInformation } from '../metadata/definitions/model-information';
import { FieldDescriptorType } from '../metadata/enumerations/field-descriptor-type';

export interface ModelInformationDisplayEntry {
  label: string;
  value: string;
}

const integerFormatter = new Intl.NumberFormat('en-US', {
  maximumFractionDigits: 0,
});

const numberFormatter = new Intl.NumberFormat('en-US', {
  maximumFractionDigits: 20,
});

const currencyFormatter = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 2,
  maximumFractionDigits: 4,
});

export function buildModelInformationEntries(
  information: ModelInformation[] | null | undefined,
): ModelInformationDisplayEntry[] {
  return (information ?? []).map((entry) => ({
    label: getInformationLabel(entry),
    value: formatInformationValue(entry),
  }));
}

function getInformationLabel(entry: ModelInformation): string {
  const displayName = entry.displayName?.trim();
  return displayName && displayName.length > 0 ? displayName : entry.name;
}

function formatInformationValue(entry: ModelInformation): string {
  switch (entry.type) {
    case FieldDescriptorType.Integer:
      return formatInteger(entry.value);
    case FieldDescriptorType.Double:
      return formatNumber(entry.value);
    case FieldDescriptorType.Currency:
      return formatCurrency(entry.value);
    default:
      return formatGenericValue(entry.value);
  }
}

function formatInteger(value: unknown): string {
  const numeric = tryGetNumber(value);
  if (numeric === null) {
    return formatGenericValue(value);
  }

  return integerFormatter.format(Math.trunc(numeric));
}

function formatNumber(value: unknown): string {
  const numeric = tryGetNumber(value);
  if (numeric === null) {
    return formatGenericValue(value);
  }

  return numberFormatter.format(numeric);
}

function formatCurrency(value: unknown): string {
  const numeric = tryGetNumber(value);
  if (numeric === null) {
    return formatGenericValue(value);
  }

  return currencyFormatter.format(numeric);
}

function tryGetNumber(value: unknown): number | null {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === 'string') {
    const numeric = Number(value);
    return Number.isFinite(numeric) ? numeric : null;
  }

  return null;
}

function formatGenericValue(value: unknown): string {
  if (value === null || value === undefined) {
    return '';
  }

  if (typeof value === 'string') {
    return value;
  }

  if (typeof value === 'boolean') {
    return value ? 'True' : 'False';
  }

  return String(value);
}

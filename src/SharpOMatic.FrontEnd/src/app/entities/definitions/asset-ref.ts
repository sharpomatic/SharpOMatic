export interface AssetRef {
  assetId: string;
  name: string;
  mediaType: string;
  sizeBytes: number;
}

const isAssetRef = (value: unknown): value is AssetRef => {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const candidate = value as AssetRef;
  return typeof candidate.assetId === 'string'
    && typeof candidate.name === 'string'
    && typeof candidate.mediaType === 'string'
    && typeof candidate.sizeBytes === 'number';
};

export const parseAssetRefValue = (value: string): AssetRef | null => {
  if (!value) {
    return null;
  }

  try {
    const parsed = JSON.parse(value);
    return isAssetRef(parsed) ? parsed : null;
  } catch {
    return null;
  }
};

export const parseAssetRefListValue = (value: string): AssetRef[] => {
  if (!value) {
    return [];
  }

  try {
    const parsed = JSON.parse(value);
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed.filter(isAssetRef);
  } catch {
    return [];
  }
};

export const buildAssetRefValue = (asset: AssetRef | null): string => {
  return asset ? JSON.stringify(asset) : '';
};

export const buildAssetRefListValue = (assets: AssetRef[]): string => {
  return JSON.stringify(assets);
};

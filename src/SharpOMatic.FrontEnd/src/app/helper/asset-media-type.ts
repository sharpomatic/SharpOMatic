export const normalizeMediaType = (mediaType?: string | null): string => {
  if (!mediaType) {
    return '';
  }

  return mediaType.split(';', 2)[0].trim().toLowerCase();
};

export const isTextLikeMediaType = (mediaType?: string | null): boolean => {
  const normalized = normalizeMediaType(mediaType);
  if (!normalized) {
    return false;
  }

  if (normalized.startsWith('text/')) {
    return true;
  }

  return normalized === 'application/json' || normalized === 'application/xml';
};

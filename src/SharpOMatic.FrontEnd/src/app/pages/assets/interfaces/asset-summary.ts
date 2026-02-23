import { AssetScope } from '../../../enumerations/asset-scope';

export interface AssetSummary {
  assetId: string;
  name: string;
  mediaType: string;
  sizeBytes: number;
  scope: AssetScope;
  created: string;
  folderId?: string | null;
  folderName?: string | null;
}

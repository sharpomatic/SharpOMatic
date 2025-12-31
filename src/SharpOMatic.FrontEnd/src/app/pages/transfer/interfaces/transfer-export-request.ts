import { TransferSelection } from './transfer-selection';

export interface TransferExportRequest {
  includeSecrets: boolean;
  workflows?: TransferSelection;
  connectors?: TransferSelection;
  models?: TransferSelection;
  assets?: TransferSelection;
}

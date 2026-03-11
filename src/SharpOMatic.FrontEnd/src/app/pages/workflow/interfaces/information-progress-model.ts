import { InformationType } from '../../../enumerations/information-type';

export interface InformationProgressModel {
  informationId: string;
  traceId: string;
  runId: string;
  created: string;
  informationType: InformationType;
  text: string;
  data?: string | null;
}

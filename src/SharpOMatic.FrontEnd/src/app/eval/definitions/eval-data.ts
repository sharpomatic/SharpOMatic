export interface EvalDataSnapshot {
  evalDataId: string;
  evalRowId: string;
  evalColumnId: string;
  stringValue: string | null;
  intValue: number | null;
  doubleValue: number | null;
  boolValue: boolean | null;
}

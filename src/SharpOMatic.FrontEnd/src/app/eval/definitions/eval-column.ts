import { WritableSignal, signal } from '@angular/core';
import { ContextEntryType } from '../../entities/enumerations/context-entry-type';

export interface EvalColumnSnapshot {
  evalColumnId: string;
  evalConfigId: string;
  name: string;
  order: number;
  entryType: ContextEntryType;
  optional: boolean;
  inputPath: string | null;
}

export class EvalColumn {
  public readonly evalColumnId: string;
  public name: WritableSignal<string>;
  public entryType: WritableSignal<ContextEntryType>;
  public optional: WritableSignal<boolean>;
  public inputPath: WritableSignal<string | null>;

  constructor(snapshot: EvalColumnSnapshot) {
    this.evalColumnId = snapshot.evalColumnId;
    this.name = signal(snapshot.name);
    this.entryType = signal(snapshot.entryType);
    this.optional = signal(snapshot.optional);
    this.inputPath = signal(snapshot.inputPath ?? null);
  }

  public toSnapshot(order: number, evalConfigId: string): EvalColumnSnapshot {
    return {
      evalColumnId: this.evalColumnId,
      evalConfigId,
      name: this.name(),
      entryType: this.entryType(),
      optional: this.optional(),
      inputPath: this.inputPath() ?? null,
      order,
    };
  }

  public static fromSnapshot(snapshot: EvalColumnSnapshot): EvalColumn {
    return new EvalColumn(snapshot);
  }

  public static defaultSnapshot(
    order: number,
    evalConfigId: string,
  ): EvalColumnSnapshot {
    return {
      evalColumnId: crypto.randomUUID(),
      evalConfigId,
      name: '',
      entryType: ContextEntryType.String,
      optional: false,
      inputPath: null,
      order,
    };
  }
}

import { ContextEntryPurpose } from '../enumerations/context-entry-purpose';
import { ContextEntryType } from '../enumerations/context-entry-type';
import { Entity, EntitySnapshot } from './entity.entity';
import { computed, signal, Signal, WritableSignal } from '@angular/core';

export interface ContextEntrySnapshot extends EntitySnapshot {
  purpose: ContextEntryPurpose;
  inputPath: string;
  outputPath: string;
  optional: boolean;
  entryType: ContextEntryType;
  entryValue: string
}

export class ContextEntryEntity extends Entity<ContextEntrySnapshot> {
  public purpose: WritableSignal<ContextEntryPurpose>;
  public inputPath: WritableSignal<string>;
  public outputPath: WritableSignal<string>;
  public optional: WritableSignal<boolean>;
  public entryType: WritableSignal<ContextEntryType>;
  public entryValue: WritableSignal<string>;
  public isDirty: Signal<boolean>;

  constructor(snapshot: ContextEntrySnapshot) {
    super(snapshot);

    this.purpose = signal(snapshot.purpose);
    this.inputPath = signal(snapshot.inputPath);
    this.outputPath = signal(snapshot.outputPath);
    this.optional = signal(snapshot.optional);
    this.entryType = signal(snapshot.entryType);
    this.entryValue = signal(snapshot.entryValue);

    this.isDirty = computed(() => {
      const snapshot = this.snapshot();

      // Must touch all property signals
      const currentPurpose = this.purpose();
      const currentInputPath = this.inputPath();
      const currentOutputPath = this.outputPath();
      const currentOptional = this.optional();
      const currentEntryType = this.entryType();
      const currentEntryValue = this.entryValue();

      return (currentPurpose !== snapshot.purpose) ||
             (currentInputPath !== snapshot.inputPath) ||
             (currentOutputPath !== snapshot.outputPath) ||
             (currentOptional !== snapshot.optional) ||
             (currentEntryType !== snapshot.entryType) ||
             (currentEntryValue !== snapshot.entryValue);
    });
  }

  public override toSnapshot(): ContextEntrySnapshot {
    return {
      id: this.id,
      purpose: this.purpose(),
      inputPath: this.inputPath(),
      outputPath: this.outputPath(),
      optional: this.optional(),
      entryType: this.entryType(),
      entryValue: this.entryValue(),
    };
  }

  public static fromSnapshot(snapshot: ContextEntrySnapshot): ContextEntryEntity {
    return new ContextEntryEntity(snapshot);
  }

  public static override defaultSnapshot(): ContextEntrySnapshot {
    return {
      id: crypto.randomUUID(),
      purpose: ContextEntryPurpose.Input,
      inputPath: '',
      outputPath: '',
      optional: false,
      entryType: ContextEntryType.String,
      entryValue: ''
    };
  }
}

import {
  effect,
  inject,
  Injectable,
  WritableSignal,
  signal,
} from '@angular/core';
import { ServerRepositoryService } from './server.repository.service';
import { SignalrService } from './signalr.service';

@Injectable({
  providedIn: 'root',
})
export class SamplesService {
  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly signalrService = inject(SignalrService);
  private readonly _sampleNames: WritableSignal<string[]> = signal([]);
  readonly sampleNames = this._sampleNames.asReadonly();

  constructor() {
    effect(() => {
      if (this.signalrService.isConnected()) {
        this.loadSampleNames();
      } else {
        this._sampleNames.set([]);
      }
    });
  }

  private loadSampleNames(): void {
    this.serverRepository.getSampleWorkflowNames().subscribe((names) => {
      const sorted = [...names].sort(compareSampleNames);
      this._sampleNames.set(sorted);
    });
  }
}

function compareSampleNames(a: string, b: string): number {
  const groupComparison = sampleGroup(a) - sampleGroup(b);
  if (groupComparison !== 0) {
    return groupComparison;
  }

  return a.localeCompare(b);
}

function sampleGroup(name: string): number {
  if (name.startsWith('Basic')) {
    return 0;
  }

  if (name.startsWith('AG-UI')) {
    return 1;
  }

  return 2;
}

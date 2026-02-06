import { inject, Injectable, NgZone, OnDestroy } from '@angular/core';
import { Subject } from 'rxjs';
import { ServerMonacoService } from './server.monaco.service';
import * as monaco from 'monaco-editor';

type ITextModel = monaco.editor.ITextModel;
type IDisposable = monaco.IDisposable;
type IModelContentChangedEvent = monaco.editor.IModelContentChangedEvent;

@Injectable({
  providedIn: 'root',
})
export class MonacoService implements OnDestroy {
  public static editorOptionsJson = {
    theme: 'vs-dark',
    language: 'json',
    automaticLayout: true,
    minimap: { enabled: false },
    scrollBeyondLastLine: false,
    tabSize: 2,
    insertSpaces: true,
  };

  public static editorOptionsCSharp = {
    theme: 'vs-dark',
    language: 'csharp',
    automaticLayout: true,
    minimap: { enabled: false },
    scrollBeyondLastLine: false,
    tabSize: 2,
    insertSpaces: true,
  };

  private static CODE_CHECK_FREQUENCY = 500;

  private readonly serverMonaco = inject(ServerMonacoService);
  private monaco: any;
  private modelListeners = new Map<ITextModel, IDisposable>();
  private isInitialized = false;
  private codeCheck = new Subject<ITextModel>();
  public readonly codeCheck$ = this.codeCheck.asObservable();

  constructor(private zone: NgZone) {}

  public init(): void {
    if (this.isInitialized) {
      return;
    }

    this.monaco = (window as any).monaco;
    if (!this.monaco) {
      return;
    }

    this.codeCheck$.subscribe((model) => this.performCodeCheck(model));
    this.setupGlobalListeners(this.monaco);
    this.isInitialized = true;
  }

  performCodeCheck(model: ITextModel) {
    this.serverMonaco.codeCheck(model.getValue()).subscribe((results) => {
      this.zone.run(() => {
        let markers: monaco.editor.IMarkerData[] = [];

        if (results) {
          for (let result of results) {
            const posStart = model.getPositionAt(result.from);
            const posEnd = model.getPositionAt(result.to);
            markers.push({
              severity: result.severity as monaco.MarkerSeverity,
              startLineNumber: posStart.lineNumber,
              startColumn: posStart.column,
              endLineNumber: posEnd.lineNumber,
              endColumn: posEnd.column,
              message: result.message,
              code: result.id,
            });
          }
        }

        this.monaco.editor.setModelMarkers(model, 'csharp', markers);
      });
    });
  }

  private setupGlobalListeners(monaco: any): void {
    this.monaco.editor.onDidCreateModel((model: ITextModel) => {
      if (model.getLanguageId() !== 'csharp') {
        return;
      }

      let handle: number | undefined;
      const changeListener = model.onDidChangeContent(
        (event: IModelContentChangedEvent) => {
          clearTimeout(handle);
          handle = setTimeout(() => {
            this.zone.run(() => this.codeCheck.next(model));
          }, MonacoService.CODE_CHECK_FREQUENCY);
        },
      );
      this.modelListeners.set(model, changeListener);
    });

    this.monaco.editor.onWillDisposeModel((model: ITextModel) => {
      if (this.modelListeners.has(model)) {
        this.modelListeners.get(model)?.dispose();
        this.modelListeners.delete(model);
      }
    });
  }

  ngOnDestroy(): void {
    this.modelListeners.forEach((listener) => listener.dispose());
  }
}

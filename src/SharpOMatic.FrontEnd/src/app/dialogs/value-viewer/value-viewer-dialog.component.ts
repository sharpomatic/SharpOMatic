import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BsModalRef } from 'ngx-bootstrap/modal';
import { MonacoEditorModule } from 'ngx-monaco-editor-v2';
import { MonacoService } from '../../services/monaco.service';

type ValueViewerMode = 'text' | 'json';

@Component({
  selector: 'app-value-viewer-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MonacoEditorModule],
  templateUrl: './value-viewer-dialog.component.html',
  styleUrls: ['./value-viewer-dialog.component.scss'],
})
export class ValueViewerDialogComponent implements OnInit {
  public title = '';
  public value = '';
  public displayValue = '';
  public jsonValue = '';
  public mode: ValueViewerMode = 'text';
  public isJson = false;
  public readonly jsonEditorOptions = {
    ...MonacoService.editorOptionsJson,
    readOnly: true,
  };

  constructor(public bsModalRef: BsModalRef) {}

  ngOnInit(): void {
    this.displayValue = this.value;
    const parsedJson = this.tryFormatJson(this.value);

    if (!parsedJson) {
      return;
    }

    this.isJson = true;
    this.jsonValue = parsedJson;
    this.mode = 'json';
  }

  setMode(mode: ValueViewerMode): void {
    if (mode === 'json' && !this.isJson) {
      return;
    }

    this.mode = mode;
  }

  copyToClipboard(): void {
    const text = this.mode === 'json' ? this.jsonValue : this.displayValue;
    void navigator.clipboard?.writeText(text);
  }

  close(): void {
    this.bsModalRef.hide();
  }

  private tryFormatJson(value: string): string | null {
    const trimmed = value.trim();
    if (!trimmed.startsWith('{') && !trimmed.startsWith('[')) {
      return null;
    }

    try {
      const parsed = JSON.parse(trimmed);
      if (parsed === null || typeof parsed !== 'object') {
        return null;
      }

      return JSON.stringify(parsed, null, 2);
    } catch {
      return null;
    }
  }
}

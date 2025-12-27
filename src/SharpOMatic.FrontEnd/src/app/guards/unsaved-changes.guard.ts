import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { BsModalRef, BsModalService } from 'ngx-bootstrap/modal';
import { Observable, catchError, from, map, of, switchMap, take } from 'rxjs';
import {
  UnsavedChangesDecision,
  UnsavedChangesDialogComponent,
} from '../dialogs/unsaved-changes/unsaved-changes-dialog.component';

export interface CanLeaveWithUnsavedChanges {
  hasUnsavedChanges(): boolean;
  saveChanges(): Observable<unknown> | Promise<unknown> | void;
}

export const unsavedChangesGuard: CanDeactivateFn<CanLeaveWithUnsavedChanges> = (component) => {
  if (!component?.hasUnsavedChanges?.() || !component.hasUnsavedChanges()) {
    return true;
  }

  const modalService = inject(BsModalService);
  const modalRef: BsModalRef<UnsavedChangesDialogComponent> | undefined = modalService.show(
    UnsavedChangesDialogComponent,
    {
      initialState: {
        title: 'Unsaved changes',
        message: 'You have unsaved changes. What would you like to do?',
      },
      keyboard: false,
      backdrop: 'static',
    }
  );

  if (!modalRef?.onHidden) {
    return false;
  }

  return modalRef.onHidden.pipe(
    take(1),
    map(() => modalRef.content?.result ?? 'stay'),
    switchMap((decision: UnsavedChangesDecision) => {
      if (decision === 'discard') {
        return of(true);
      }

      if (decision === 'stay') {
        return of(false);
      }

      try {
        const saveResult = component.saveChanges();
        if (!saveResult) {
          return of(true);
        }

        return from(saveResult).pipe(
          map(() => true),
          catchError(() => of(false))
        );
      } catch {
        return of(false);
      }
    })
  );
};

import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { BsModalService, BsModalRef } from 'ngx-bootstrap/modal';
import { NotConnectedDialogComponent } from '../../dialogs/not-connected/not-connected-dialog.component';
import { ToastService } from '../../services/toast.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  providers: [BsModalService],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App implements OnInit, OnDestroy {
  private readonly modalService = inject(BsModalService);
  protected readonly toastService = inject(ToastService);
  private modalRef?: BsModalRef;

  isSidebarClosed = false;

  constructor() {}

  toggleSidebar(): void {
    this.isSidebarClosed = !this.isSidebarClosed;
  }

  ngOnInit(): void {
    document.documentElement.setAttribute('data-bs-theme', 'dark');
  }

  private openNotConnectedDialog(): void {
    if (this.modalRef) return;
    this.modalRef = this.modalService.show(NotConnectedDialogComponent, {
      initialState: {},
      backdrop: 'static',
      keyboard: false,
    });
  }

  private closeNotConnectedDialog(): void {
    this.modalRef?.hide();
    this.modalRef = undefined;
  }

  ngOnDestroy(): void {
    this.closeNotConnectedDialog();
  }
}

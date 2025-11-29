import { Component, effect, inject, OnDestroy, OnInit } from '@angular/core';
import { CommonModule, TitleCasePipe } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { BsModalService, BsModalRef } from 'ngx-bootstrap/modal';
import { ServerConnectionService } from '../../services/server.connection.service';
import { NotConnectedDialogComponent } from '../../dialogs/not-connected/not-connected-dialog.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    TitleCasePipe,
  ],
  providers: [BsModalService],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit, OnDestroy {
  private readonly serverConnectionService = inject(ServerConnectionService);
  private readonly modalService = inject(BsModalService);
  private modalRef?: BsModalRef;

  activeTheme: string = 'auto';
  activeThemeIcon: string = 'bi-circle-half';
  isSidebarClosed = false;

  constructor() {
    effect(() => {
      if (!this.serverConnectionService.isConnected()) {
        this.openNotConnectedDialog();
      } else {
        this.closeNotConnectedDialog();
      }
    });
  }

  toggleSidebar(): void {
    this.isSidebarClosed = !this.isSidebarClosed;
  }

  ngOnInit(): void {
    this.initializeTheme();
  }

  setTheme(theme: string): void {
    this.activeTheme = theme;
    if (theme === 'auto') {
      this.activeThemeIcon = 'bi-circle-half';
      const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
      document.documentElement.setAttribute('data-bs-theme', prefersDark ? 'dark' : 'light');
    } else {
      this.activeThemeIcon = theme === 'light' ? 'bi-sun-fill' : 'bi-moon-stars-fill';
      document.documentElement.setAttribute('data-bs-theme', theme);
    }
    localStorage.setItem('theme', theme);
  }

  private initializeTheme(): void {
    const storedTheme = localStorage.getItem('theme') ?? 'auto';
    this.setTheme(storedTheme);

    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
      if (this.activeTheme === 'auto') {
        this.setTheme('auto');
      }
    });
  }

  private openNotConnectedDialog(): void {
    if (this.modalRef) return;
    this.modalRef = this.modalService.show(NotConnectedDialogComponent, {
      initialState: {},
      backdrop: 'static',
      keyboard: false
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

import { Component, inject, Signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ServerConnectionService } from '../../services/server.connection.service';
import { API_URL } from '../../components/app/app.tokens';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './settings.component.html',
  styleUrls: ['./settings.component.scss']
})
export class SettingsComponent {
  private readonly serverConnectionService = inject(ServerConnectionService);
  public readonly apiUrl = inject(API_URL);
  public isConnected: Signal<boolean>;

  constructor() {
    this.isConnected = this.serverConnectionService.isConnected;
  }
}

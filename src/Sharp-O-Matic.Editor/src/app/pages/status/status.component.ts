import { Component, inject, Signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ServerConnectionService } from '../../services/server.connection.service';
import { API_URL } from '../../components/app/app.tokens';

@Component({
  selector: 'app-status',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './status.component.html',
  styleUrls: ['./status.component.scss']
})
export class StatusComponent {
  private readonly serverConnectionService = inject(ServerConnectionService);
  public readonly apiUrl = inject(API_URL);
  public isConnected: Signal<boolean>;

  constructor() {
    this.isConnected = this.serverConnectionService.isConnected;
  }
}

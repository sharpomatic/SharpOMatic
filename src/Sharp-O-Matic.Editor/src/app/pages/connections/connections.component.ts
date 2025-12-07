import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BsModalRef, BsModalService } from 'ngx-bootstrap/modal';
import { Router, RouterLink } from '@angular/router';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { ConnectionSummary } from '../../metadata/definitions/connection summary';
import { ConfirmDialogComponent } from '../../dialogs/confirm/confirm-dialog.component';
import { Connection } from '../../metadata/definitions/connection';

@Component({
  selector: 'app-connections',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink
  ],
  templateUrl: './connections.component.html',
  styleUrls: ['./connections.component.scss'],
  providers: [BsModalService]
})
export class ConnectionsComponent {
  private readonly serverWorkflow = inject(ServerRepositoryService);  
  private readonly modalService = inject(BsModalService);
  private readonly router = inject(Router);  
  private confirmModalRef: BsModalRef<ConfirmDialogComponent> | undefined;
  
  public connections: ConnectionSummary[] = [];

  ngOnInit(): void {
    this.serverWorkflow.getConnectionSummaries().subscribe(connections => {
      this.connections = connections;
    });
  }

  newConnection(): void {
    const newConnection = new Connection({
      ...Connection.defaultSnapshot(),
      configId: '',
      name: 'Untitled',
      description: 'Connection needs a description.',
    });

    this.serverWorkflow.upsertConnection(newConnection).subscribe(() => {
      this.router.navigate(['/connections', newConnection.connectionId]);
    });
  }

  deleteConnection(connection: ConnectionSummary) {
    this.confirmModalRef = this.modalService.show(ConfirmDialogComponent, {
      initialState: {
        title: 'Delete Connection',
        message: `Are you sure you want to delete the connection '${connection.name}'?`
      }
    });

    this.confirmModalRef.onHidden?.subscribe(() => {
      if (this.confirmModalRef?.content?.result) {
        this.serverWorkflow.deleteConnection(connection.connectionId).subscribe(() => {
          this.connections = this.connections.filter(c => c.connectionId !== connection.connectionId);
        });
      }
    });
  }  
}

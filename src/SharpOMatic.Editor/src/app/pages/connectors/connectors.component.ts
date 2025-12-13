import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BsModalRef, BsModalService } from 'ngx-bootstrap/modal';
import { Router, RouterLink } from '@angular/router';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { ConnectorSummary } from '../../metadata/definitions/connector-summary';
import { ConfirmDialogComponent } from '../../dialogs/confirm/confirm-dialog.component';
import { Connector } from '../../metadata/definitions/connector';

@Component({
  selector: 'app-connectors',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink
  ],
  templateUrl: './connectors.component.html',
  styleUrls: ['./connectors.component.scss'],
  providers: [BsModalService]
})
export class ConnectorsComponent {
  private readonly serverWorkflow = inject(ServerRepositoryService);  
  private readonly modalService = inject(BsModalService);
  private readonly router = inject(Router);  
  private confirmModalRef: BsModalRef<ConfirmDialogComponent> | undefined;
  
  public connectors: ConnectorSummary[] = [];

  ngOnInit(): void {
    this.serverWorkflow.getConnectorSummaries().subscribe(connectors => {
      this.connectors = connectors;
    });
  }

  newConnector(): void {
    const newConnector = new Connector({
      ...Connector.defaultSnapshot(),
      configId: '',
      name: 'Untitled',
      description: 'Connector needs a description.',
    });

    this.serverWorkflow.upsertConnector(newConnector).subscribe(() => {
      this.router.navigate(['/connectors', newConnector.connectorId]);
    });
  }

  deleteConnector(connector: ConnectorSummary) {
    this.confirmModalRef = this.modalService.show(ConfirmDialogComponent, {
      initialState: {
        title: 'Delete Connector',
        message: `Are you sure you want to delete the connector '${connector.name}'?`
      }
    });

    this.confirmModalRef.onHidden?.subscribe(() => {
      if (this.confirmModalRef?.content?.result) {
        this.serverWorkflow.deleteConnector(connector.connectorId).subscribe(() => {
          this.connectors = this.connectors.filter(c => c.connectorId !== connector.connectorId);
        });
      }
    });
  }  
}

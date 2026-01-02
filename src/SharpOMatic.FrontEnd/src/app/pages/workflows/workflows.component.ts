import { Component, OnInit, inject } from '@angular/core';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { BsModalService, BsModalRef } from 'ngx-bootstrap/modal';
import { ConfirmDialogComponent } from '../../dialogs/confirm/confirm-dialog.component';
import { WorkflowEntity } from '../../entities/definitions/workflow.entity';
import { WorkflowSummaryEntity } from '../../entities/definitions/workflow.summary.entity';

@Component({
  selector: 'app-workflows',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink
  ],
  templateUrl: './workflows.component.html',
  styleUrls: ['./workflows.component.scss'],
  providers: [BsModalService]
})
export class WorkflowsComponent implements OnInit {
  private readonly serverWorkflow = inject(ServerRepositoryService);
  private readonly modalService = inject(BsModalService);
  private readonly router = inject(Router);
  private bsModalRef: BsModalRef<ConfirmDialogComponent> | undefined;

  public workflows: WorkflowSummaryEntity[] = [];
  public isLoading = true;

  ngOnInit(): void {
    this.loadWorkflows();
  }

  private loadWorkflows(): void {
    this.isLoading = true;
    this.serverWorkflow.getWorkflows().subscribe({
      next: (workflows) => {
        this.workflows = workflows;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
      },
    });
  }

  newWorkflow(): void {
    const newWorkflow = WorkflowEntity.create('Untitled', 'New workflow needs a description.');
    this.serverWorkflow.upsertWorkflow(newWorkflow).subscribe(() => {
      this.router.navigate(['/workflows', newWorkflow.id]);
    });
  }

  deleteWorkflow(workflow: WorkflowSummaryEntity) {
    this.bsModalRef = this.modalService.show(ConfirmDialogComponent, {
      initialState: {
        title: 'Delete Workflow',
        message: `Are you sure you want to delete the workflow '${workflow.name()}'?`
      }
    });

    this.bsModalRef.onHidden?.subscribe(() => {
      if (this.bsModalRef?.content?.result) {
        this.serverWorkflow.deleteWorkflow(workflow.id).subscribe(() => {
          this.workflows = this.workflows.filter(w => w.id !== workflow.id);
        });
      }
    });
  }

  copyWorkflow(workflow: WorkflowSummaryEntity): void {
    this.serverWorkflow.copyWorkflow(workflow.id).subscribe((newWorkflowId) => {
      if (newWorkflowId) {
        this.loadWorkflows();
      }
    });
  }
}

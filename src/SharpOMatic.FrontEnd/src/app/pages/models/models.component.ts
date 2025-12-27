import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { BsModalRef, BsModalService } from 'ngx-bootstrap/modal';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { ModelSummary } from '../../metadata/definitions/model-summary';
import { Model } from '../../metadata/definitions/model';
import { ConfirmDialogComponent } from '../../dialogs/confirm/confirm-dialog.component';

@Component({
  selector: 'app-models',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
  ],
  templateUrl: './models.component.html',
  styleUrls: ['./models.component.scss'],
  providers: [BsModalService],
})
export class ModelsComponent {
  private readonly serverRepository = inject(ServerRepositoryService);
  private readonly modalService = inject(BsModalService);
  private readonly router = inject(Router);
  private confirmModalRef: BsModalRef<ConfirmDialogComponent> | undefined;

  public models: ModelSummary[] = [];

  ngOnInit(): void {
    this.serverRepository.getModelSummaries().subscribe(models => {
      this.models = models;
    });
  }

  newModel(): void {
    const newModel = new Model({
      ...Model.defaultSnapshot(),
      name: 'Untitled',
      description: 'Model needs a description.',
    });

    this.serverRepository.upsertModel(newModel).subscribe(() => {
      this.router.navigate(['/models', newModel.modelId]);
    });
  }

  deleteModel(model: ModelSummary) {
    this.confirmModalRef = this.modalService.show(ConfirmDialogComponent, {
      initialState: {
        title: 'Delete Model',
        message: `Are you sure you want to delete the model '${model.name}'?`,
      },
    });

    this.confirmModalRef.onHidden?.subscribe(() => {
      if (this.confirmModalRef?.content?.result) {
        this.serverRepository.deleteModel(model.modelId).subscribe(() => {
          this.models = this.models.filter(m => m.modelId !== model.modelId);
        });
      }
    });
  }
}

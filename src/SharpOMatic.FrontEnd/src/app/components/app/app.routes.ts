import { Routes } from '@angular/router';
import { WorkflowsComponent } from '../../pages/workflows/workflows.component';
import { SettingsComponent } from '../../pages/settings/settings.component';
import { WorkflowComponent } from '../../pages/workflow/components/workflow.component';
import { ConnectorsComponent } from '../../pages/connectors/connectors.component';
import { ConnectorComponent } from '../../pages/connector/connectorcomponent';
import { ModelsComponent } from '../../pages/models/models.component';
import { ModelComponent } from '../../pages/model/model.component';
import { EvaluationsComponent } from '../../pages/evaluations/evaluations.component';
import { EvaluationComponent } from '../../pages/evaluation/evaluation.component';
import { AssetsComponent } from '../../pages/assets/assets.component';
import { TransferComponent } from '../../pages/transfer/transfer.component';
import { unsavedChangesGuard } from '../../helper/unsaved-changes.guard';

export const routes: Routes = [
  { path: '', redirectTo: '/workflows', pathMatch: 'full' },
  { path: 'workflows', component: WorkflowsComponent },
  { path: 'workflows/:id', component: WorkflowComponent, canDeactivate: [unsavedChangesGuard] },
  { path: 'models/:id', component: ModelComponent, canDeactivate: [unsavedChangesGuard] },
  { path: 'models', component: ModelsComponent },
  { path: 'evaluations', component: EvaluationsComponent },
  { path: 'evaluations/:id', component: EvaluationComponent, canDeactivate: [unsavedChangesGuard] },
  { path: 'connectors/:id', component: ConnectorComponent, canDeactivate: [unsavedChangesGuard] },
  { path: 'connectors', component: ConnectorsComponent },
  { path: 'assets', component: AssetsComponent },
  { path: 'transfer', component: TransferComponent },
  { path: 'settings', component: SettingsComponent },
];

import { Routes } from '@angular/router';
import { WorkflowsComponent } from '../../pages/workflows/workflows.component';
import { StatusComponent } from '../../pages/status/status.component';
import { WorkflowComponent } from '../../pages/workflow/components/workflow.component';

export const routes: Routes = [
  { path: '', redirectTo: '/workflows', pathMatch: 'full' },
  { path: 'workflows', component: WorkflowsComponent },
  { path: 'workflows/:id', component: WorkflowComponent },
  { path: 'status', component: StatusComponent },
];

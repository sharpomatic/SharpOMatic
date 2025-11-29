import { Component, TemplateRef, ViewChild, inject, OnInit, signal, computed } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { WorkflowService } from '../services/workflow.service';
import { DesignerComponent } from '../../../components/designer/components/designer.component';
import { DesignerUpdateService } from '../../../components/designer/services/designer-update.service';
import { BsDropdownModule } from 'ngx-bootstrap/dropdown';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BsModalService } from 'ngx-bootstrap/modal';
import { TracebarComponent } from './tracebar.component';
import { NodeType } from '../../../entities/enumerations/node-type';
import { TabComponent, TabItem } from '../../../components/tab/tab.component';

@Component({
  selector: 'app-workflow',
  imports: [
    CommonModule,
    FormsModule,
    BsDropdownModule,
    TabComponent,
    DesignerComponent,
    TracebarComponent
  ],
  templateUrl: './workflow.component.html',
  styleUrl: './workflow.component.scss'
})
export class WorkflowComponent implements OnInit {
  @ViewChild('designTab', { static: true }) designTab!: TemplateRef<unknown>;
  @ViewChild('detailsTab', { static: true }) detailsTab!: TemplateRef<unknown>;

  public readonly route = inject(ActivatedRoute);
  public readonly updateService = inject(DesignerUpdateService);
  public readonly workflowService = inject(WorkflowService);
  public readonly modalService = inject(BsModalService);

  public activeTab = 'Design';
  public activeTabId = 'design';
  public isTracebarClosed = signal(true);
  public tracebarWidth = signal(800);
  public tabs: TabItem[] = [];
  public readonly hasStartNode = computed(() => this.workflowService.workflow().nodes().some(node => node.nodeType() === NodeType.Start));

  ngOnInit(): void {
    this.tabs = [
      { id: 'design', title: 'Design', content: this.designTab },
      { id: 'details', title: 'Details', content: this.detailsTab }
    ];

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.workflowService.load(id);
    }
  }

  save(): void {
    this.workflowService.save();
  }

  run(): void {
    this.workflowService.run().subscribe(() => {
      this.isTracebarClosed.set(false);
    });
  }

  toggleTracebar(): void {
    this.isTracebarClosed.set(!this.isTracebarClosed());
  }

  onActiveTabChange(tab: TabItem): void {
    this.activeTab = tab.title;
  }

  onAddStartNode(event: Event): void {
    if (this.hasStartNode()) {
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    this.updateService.addStartNode(this.workflowService.workflow());
  }

  onTracebarWidthChange(width: number): void {
    this.tracebarWidth.set(width);
  }
}

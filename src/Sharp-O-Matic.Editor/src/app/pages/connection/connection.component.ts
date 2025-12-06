import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { Connection } from '../../metadata/definitions/connection';
import { ServerRepositoryService } from '../../services/server.repository.service';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-connection',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule
  ],
  templateUrl: './connection.component.html',
  styleUrls: ['./connection.component.scss'],
})
export class ConnectionComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly serverRepository = inject(ServerRepositoryService);

  public connection: Connection = new Connection(Connection.defaultSnapshot());

  ngOnInit(): void {
    const connectionId = this.route.snapshot.paramMap.get('id');
    if (connectionId) {
      this.serverRepository.getConnection(connectionId).subscribe(connection => {
        if (connection) {
          this.connection = connection;
        }
      });
    }
  }

  save(): void {
    this.serverRepository.upsertConnection(this.connection)
      .subscribe(() => {
        this.connection?.markClean();
    });
  }  
}

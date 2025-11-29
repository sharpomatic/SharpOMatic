import { Injectable, signal } from '@angular/core';
import { Entity, EntitySnapshot } from '../../../entities/definitions/entity.entity';

@Injectable({
  providedIn: 'root',
})
export class DesignerSelectionService {
  private readonly _selectedEntities = signal<Entity<EntitySnapshot>[]>([]);
  public readonly selectedEntities = this._selectedEntities.asReadonly();

  isSelected(entity: Entity<EntitySnapshot>) {
    return this._selectedEntities().includes(entity);
  }

  setSelection(entities: Entity<EntitySnapshot>[]) {
    this._selectedEntities.set(entities);
  }

  clearSelection(): void {
    this.setSelection([]);
  }

  selectEntities(entities: Entity<EntitySnapshot>[]) {
    this._selectedEntities.update(ids => {
      const existing = ids.filter(id => !entities.includes(id));
      return [...existing, ...entities];
    });
  }

  deselectEntities(entities: Entity<EntitySnapshot>[]) {
    this._selectedEntities.update(existing => existing.filter(entity => !entities.includes(entity)));
  }
}

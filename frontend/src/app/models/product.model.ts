import { ChecklistTemplate } from './checklist-template.enum';

/**
 * Statisk definition av en produkt i produktkatalogen, hämtad från
 * `GET /api/init-data`. `selected` är ett rent UI-fält som läggs till
 * i frontend för kryssrutorna och skickas aldrig till backend.
 */
export interface ProductDefinition {
  code: string;
  displayName: string;
  family: string;
  unNumber: string;
  loadingStationId: string;
  template: ChecklistTemplate;
  selected?: boolean;
}

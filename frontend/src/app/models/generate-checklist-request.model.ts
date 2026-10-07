import { ChecklistTemplate } from './checklist-template.enum';

/** En fordonsenhet (dragbil eller släp) som skickas till backend. */
export interface VehicleUnitRequest {
  regNr: string;
  isNew: boolean;
}

/** En vald produkt (namn + UN-nummer) för en specifik checklista. */
export interface ProductItemRequest {
  name: string;
  unNumber: string;
}

/** En tankplats i det format backend förväntar sig (se `GenerateChecklistRequest`). */
export interface TankSlotRequest {
  tankCode: string;
  inspectionType: string;
  lastInspectionMonthYear: string;
  expiryFormatted: string;
  isExpired: boolean;
}

/** Begäran som skickas till `POST /api/checklist/generate` - en per checklista. */
export interface GenerateChecklistRequest {
  templateType: ChecklistTemplate;
  operatorName: string;
  driverName: string;
  driverAdrExpiry: string;
  isNewDriver: boolean;
  akeri?: string | null;
  truck: VehicleUnitRequest;
  trailers: VehicleUnitRequest[];
  tankSlots: TankSlotRequest[];
  selectedProducts: ProductItemRequest[];
}

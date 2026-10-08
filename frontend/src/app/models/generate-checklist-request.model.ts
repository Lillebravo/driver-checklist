import { ChecklistTemplate } from './checklist-template.enum';
import { AssistType } from './assist-type.enum';

/** En fordonsenhet (dragbil eller släp) som skickas till backend. */
export interface VehicleUnitRequest {
  regNr: string;
  isNew: boolean;
  approvalExpiry?: string | null;
}

/** En vald produkt (namn, UN-nummer och produktfamilj) för en specifik checklista. */
export interface ProductItemRequest {
  name: string;
  unNumber: string;
  family: string;
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
  assistType: AssistType;
  firstPage?: ChecklistPage;
}

export interface ChecklistPage {
  timestamp: string;
  sapNumber: string;
  loadingAmount: string;
  containerNumber: string;
  unNumbers: string[];
  rows: ChecklistRowValue[];
  roles: { row: number; selected: string[] }[];
  compartmentVolumes: string[];
}

export interface ChecklistRowValue {
  row: number;
  tt: boolean;
  tc: boolean;
  rc: boolean;
  comment: string;
}

export interface ChecklistPageResponse {
  unNumbers: string[];
  assistOptions: string[];
  rows: {
    row: number;
    question: string;
    instruction: string;
    commentInstruction: string;
    enabled: boolean[];
  }[];
  sections: { row: number; title: string; roles: string[] }[];
}

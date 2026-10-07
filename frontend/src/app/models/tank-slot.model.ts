/**
 * En beräknad tankplats (Tank 1-4) på checklistan, härledd från dragbilens
 * tankkod samt släpens fack i rätt ordning. Beräknas i frontend av
 * `TankCalculationService`.
 */
export interface TankSlot {
  slotNo: number;
  source: string;
  tankCode: string;
  inspectionType: string;
  nextTest?: string;
  lastInspectionMonthYear: string;
  expiryFormatted: string;
  isExpired: boolean;
}

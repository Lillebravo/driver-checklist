import { Injectable } from '@angular/core';
import { Trailer, TankSlot } from '../models';

/**
 * Beräknar Tank 1-4 i exakt ordning: Tank 1 är alltid dragbilen, därefter
 * släp 1:s fack och sedan släp 2:s fack. Räknar även ut giltighetsdatum
 * för provtryckning (3 år för tankbil/trailer, 2,5 år för tankcontainer)
 * och växlar varannan gång mellan inspektionstyp L och P.
 */
@Injectable({ providedIn: 'root' })
export class TankCalculationService {
  private static readonly YEARS_TANK_CONTAINER = 2.5;
  private static readonly YEARS_TANK_TRUCK_TRAILER = 3.0;

  calculateTankSlots(
    truckRegNr: string,
    truckTankCode: string,
    availableTrailers: Trailer[],
    trailer1Reg: string,
    trailer2Reg: string,
  ): TankSlot[] {
    const slots: TankSlot[] = [];

    // Tank 1: Alltid dragbilen. Dragbilens eget besiktnings-/trycktestdatum
    // hanteras inte ännu i masterdata - se README.md för planerad utökning.
    slots.push({
      slotNo: 1,
      source: `Bil (${truckRegNr})`,
      tankCode: truckTankCode,
      inspectionType: 'P',
      lastInspectionMonthYear: '2025-01',
      expiryFormatted: '2028-01',
      isExpired: false,
    });

    const addTrailerSlots = (trailerReg: string) => {
      const trailer = availableTrailers.find((t) => t.regNr === trailerReg);
      if (!trailer) {
        return;
      }

      trailer.compartments.forEach((comp) => {
        const yearsToAdd = comp.isTankContainer
          ? TankCalculationService.YEARS_TANK_CONTAINER
          : TankCalculationService.YEARS_TANK_TRUCK_TRAILER;

        const [yearStr, monthStr] = comp.lastTest.split('-');
        const testDate = new Date(parseInt(yearStr, 10), parseInt(monthStr, 10) - 1, 1);
        testDate.setMonth(testDate.getMonth() + yearsToAdd * 12);

        const expiryFormatted = `${testDate.getFullYear()}-${String(testDate.getMonth() + 1).padStart(2, '0')}`;
        const isExpired = testDate < new Date();
        const nextTest = comp.testType === 'P' ? 'L' : 'P';

        slots.push({
          slotNo: slots.length + 1,
          source: `${trailer.regNr} (Fack ${comp.compartmentNo})`,
          tankCode: comp.tankCode,
          inspectionType: comp.testType,
          nextTest,
          lastInspectionMonthYear: comp.lastTest,
          expiryFormatted,
          isExpired,
        });
      });
    };

    if (trailer1Reg) addTrailerSlots(trailer1Reg);
    if (trailer2Reg) addTrailerSlots(trailer2Reg);

    return slots;
  }
}

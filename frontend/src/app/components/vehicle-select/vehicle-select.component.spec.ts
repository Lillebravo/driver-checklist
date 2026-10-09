import { VehicleSelectComponent } from './vehicle-select.component';
import { Truck } from '../../models';
import { TankCalculationService } from '../../services/tank-calculation.service';
import { allTrailers } from '../../core/vehicle-registry.util';
import { TestBed } from '@angular/core/testing';

describe('Vehicle registry', () => {
  const trucks: Truck[] = [
    { regNr: 'ABC 123', tankCode: 'ADR', approvalExpiry: '2028-01-01', trailers: [
      { regNr: 'ONE 123', approvalExpiry: '2028-02-01', compartments: [] },
    ] },
    { regNr: 'DEF 456', tankCode: 'ADR', approvalExpiry: '2028-01-01', trailers: [
      { regNr: 'TWO 456', approvalExpiry: '2028-03-01', compartments: [
        { compartmentNo: 1, tankCode: 'L4BH', lastTest: '2026-06', testType: 'P', isTankContainer: false },
      ] },
      { regNr: 'one123', approvalExpiry: '2028-02-01', compartments: [] },
    ] },
  ];

  it('does not mark a registered trailer as new when used with another or unknown truck', () => {
    const component = new VehicleSelectComponent();
    component.trucks = trucks;
    component.truckRegNr = 'abc123';
    const spy = spyOn(component.isNewTrailer1Change, 'emit');
    component.onTrailer1Input('two456');
    expect(spy).toHaveBeenCalledWith(false);
    component.truckRegNr = 'NEW 789';
    component.onTrailer1Input('TWO 456');
    expect(spy).toHaveBeenCalledWith(false);
    component.onTrailer1Input('NEW 123');
    expect(spy).toHaveBeenCalledWith(true);
    component.onTrailer1Input('');
    expect(spy).toHaveBeenCalledWith(false);
  });

  it('lists usual trailers first and other registered trailers separately without duplicates', () => {
    const component = new VehicleSelectComponent();
    component.trucks = trucks;
    component.truckRegNr = 'abc123';
    component.activeTrailer = 1;
    expect(component.trailerGroups.map(g => g.trailers.map(t => t.regNr))).toEqual([['ONE 123'], ['TWO 456']]);
    component.onTrailer1Input('two');
    expect(component.trailerGroups[1].trailers[0].regNr).toBe('TWO 456');
  });

  it('lists and selects trailers without a truck association in either slot', () => {
    const component = new VehicleSelectComponent();
    component.trucks = trucks;
    component.registeredTrailers = [{
      regNr: 'SOLO 123', approvalExpiry: '', compartments: [], containerNumber: 'TEST123456-7',
    }];
    component.activeTrailer = 1;
    expect(component.trailerGroups[1].trailers.some(t => t.regNr === 'SOLO 123')).toBeTrue();
    const new1 = spyOn(component.isNewTrailer1Change, 'emit');
    const new2 = spyOn(component.isNewTrailer2Change, 'emit');
    component.onTrailer1Input('solo123');
    component.onTrailer2Input('SOLO 123');
    expect(new1).toHaveBeenCalledWith(false);
    expect(new2).toHaveBeenCalledWith(false);
    expect(component.trailer1ContainerNumber).toBe('TEST123456-7');
    expect(component.trailer2ContainerNumber).toBe('TEST123456-7');
  });

  it('independently toggles and emits container numbers beside all three registration fields', async () => {
    const fixture = TestBed.createComponent(VehicleSelectComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    const element: HTMLElement = fixture.nativeElement;
    const component = fixture.componentInstance;
    const units = [
      { enabled: 'truckIsTankContainer', toggle: component.truckIsTankContainerChange,
        changed: component.truckContainerNumberChange, field: 'truckContainerNumber', reg: 'truckRegNr' },
      { enabled: 'trailer1IsTankContainer', toggle: component.trailer1IsTankContainerChange,
        changed: component.trailer1ContainerNumberChange, field: 'trailer1ContainerNumber', reg: 'trailer1Reg' },
      { enabled: 'trailer2IsTankContainer', toggle: component.trailer2IsTankContainerChange,
        changed: component.trailer2ContainerNumberChange, field: 'trailer2ContainerNumber', reg: 'trailer2Reg' },
    ] as const;
    for (const unit of units) {
      expect(element.querySelector(`#${unit.field}`)).toBeNull();
      const row = element.querySelector(`#${unit.reg}`)!.closest('.vehicle-row')!;
      const toggle = row.querySelector<HTMLInputElement>('.container-toggle input')!;
      const toggled = spyOn(unit.toggle, 'emit');
      toggle.click();
      expect(toggled).toHaveBeenCalledWith(true);
      component[unit.enabled] = true;
      fixture.detectChanges();
      await fixture.whenStable();
      const input = row.querySelector<HTMLInputElement>(`#${unit.field}`)!;
      const changed = spyOn(unit.changed, 'emit');
      input.value = 'CONT-123';
      input.dispatchEvent(new Event('input'));
      expect(changed).toHaveBeenCalledWith('CONT-123');
      expect(element.querySelectorAll('.container-field').length).toBe(1);
      component[unit.enabled] = false;
      fixture.detectChanges();
      expect(element.querySelector(`#${unit.field}`)).toBeNull();
    }
  });

  it('uses global trailer tank data regardless of whitespace, case and truck association', () => {
    const slots = new TankCalculationService().calculateTankSlots('NEW123', 'ADR', allTrailers(trucks), 'two456', '');
    expect(slots.length).toBe(2);
    expect(slots[1].tankCode).toBe('L4BH');
    expect(slots[1].lastInspectionMonthYear).toBe('2026-06');
  });

  it('automatically selects TC and container number for either trailer and clears stale data on change', () => {
    const component = new VehicleSelectComponent();
    component.trucks = [{
      regNr: 'ABC123', tankCode: 'ADR', approvalExpiry: '2027-01-01', trailers: [
        { regNr: 'XYZ 789', approvalExpiry: '2027-09-01', compartments: [],
          containerNumber: 'TEST725001-0', containerTankCode: 'L4BN' },
        { regNr: 'UVW 123', approvalExpiry: '2027-09-01', compartments: [] },
      ],
    }];
    const toggle1 = spyOn(component.trailer1IsTankContainerChange, 'emit');
    const number1 = spyOn(component.trailer1ContainerNumberChange, 'emit');
    const toggle2 = spyOn(component.trailer2IsTankContainerChange, 'emit');
    const number2 = spyOn(component.trailer2ContainerNumberChange, 'emit');
    component.onTrailer1Input('xyz789');
    expect(toggle1).toHaveBeenCalledWith(true);
    expect(number1).toHaveBeenCalledWith('TEST725001-0');
    expect(component.trailer1IsTankContainer).toBeTrue();
    component.onTrailer2Input('XYZ 789');
    expect(toggle2).toHaveBeenCalledWith(true);
    expect(number2).toHaveBeenCalledWith('TEST725001-0');
    component.onTrailer1Input('UVW123');
    expect(toggle1).toHaveBeenCalledWith(false);
    expect(number1).toHaveBeenCalledWith('');
    expect(component.trailer2ContainerNumber).toBe('TEST725001-0');
    component.onTrailer2Input('');
    expect(toggle2).toHaveBeenCalledWith(false);
    expect(number2).toHaveBeenCalledWith('');
  });
});

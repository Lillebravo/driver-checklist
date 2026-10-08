import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ChecklistEditModalComponent } from './checklist-edit-modal.component';
import { ApiService } from '../../services/api.service';
import { AssistType, ChecklistPageResponse, ChecklistTemplate, GenerateChecklistRequest, ProductDefinition } from '../../models';

describe('First-page editor', () => {
  const definition: ChecklistPageResponse = {
    unNumbers: ['UN 3264', 'UN 2582'], assistOptions: ['Full assist', 'Halv assist', 'Själv lastn'],
    rows: [
      { row: 14, question: 'Identitet?', instruction: '', commentInstruction: '', enabled: [true, true, false] },
      { row: 15, question: 'ADR?', instruction: '', commentInstruction: '', enabled: [true, true, false] },
      { row: 29, question: 'Fyllnadsgrad?', instruction: '', commentInstruction: '', enabled: [true, true, true] },
    ],
    sections: [
      { row: 13, title: 'Check in', roles: ['Fordonskontrollant', 'Vakt', 'Exp'] },
      { row: 28, title: 'Före lastning', roles: ['Operatör(Full assist)', 'Chaufför(Halv assist)', 'Chaufför(Själv lastn)'] },
    ],
  };
  const request: GenerateChecklistRequest = {
    templateType: ChecklistTemplate.Type1_PixPaxSasBdp, operatorName: 'Jerry', driverName: 'Testing',
    driverAdrExpiry: '2028-01-01', isNewDriver: false, akeri: 'PH Tank',
    truck: { regNr: 'ABC123', isNew: false }, trailers: [], tankSlots: [],
    selectedProducts: [{ name: 'PIX 311', family: 'PIX', unNumber: 'UN 2582' }],
    assistType: AssistType.SelfLoading,
  };
  const products: ProductDefinition[] = [
    { code: 'PIX311', displayName: 'PIX 311', family: 'PIX', unNumber: 'UN 2582', loadingStationId: 'PIX', template: 0 },
    { code: 'BDP865', displayName: 'BDP 865', family: 'BDP', unNumber: 'UN 3264', loadingStationId: 'BDP', template: 0 },
  ];

  beforeEach(() => TestBed.configureTestingModule({
    imports: [ChecklistEditModalComponent],
    providers: [{ provide: ApiService, useValue: { getFirstPage: () => of(definition) } }],
  }));

  it('loads template order, preserves automatic checks and pads all four editable tanks', () => {
    const component = TestBed.createComponent(ChecklistEditModalComponent).componentInstance;
    component.request = request;
    expect(component.sections.map(s => s.row)).toEqual([13, 28]);
    expect(component.page?.rows[0]).toEqual({ row: 14, tt: true, tc: false, rc: false, comment: '' });
    expect(component.model?.tankSlots.length).toBe(4);
    expect(component.page?.compartmentVolumes.length).toBe(6);
    expect(component.page?.roles[1].selected).toEqual(['Chaufför(Själv lastn)']);
    component.page!.rows[0].comment = 'Changed';
    expect(request.firstPage).toBeUndefined();
  });

  it('persists general fields, unticked automatic checks, comments, roles and inspection inputs', () => {
    const fixture = TestBed.createComponent(ChecklistEditModalComponent);
    const component = fixture.componentInstance;
    component.request = request;
    const save = spyOn(component.save, 'emit');
    component.page!.sapNumber = 'SAP-123';
    component.page!.rows[0].tt = false;
    component.page!.rows[0].comment = 'Reviewed';
    component.toggle(component.page!.roles[0].selected, 'Exp', true);
    component.setInspection(0, 'L', true);
    component.model!.tankSlots[0].lastInspectionMonthYear = '2026-08';
    component.onSave();
    expect(save).toHaveBeenCalled();
    const saved = save.calls.mostRecent().args[0];
    if (!saved) throw new Error('Editor must emit a request.');
    expect(saved.firstPage?.rows[0].tt).toBeFalse();
    expect(saved.firstPage?.sapNumber).toBe('SAP-123');
    expect(saved.tankSlots[0].inspectionType).toBe('L');
  });

  it('disallows self-loading roles for new drivers and preserves manual check edits when reopening', () => {
    const component = TestBed.createComponent(ChecklistEditModalComponent).componentInstance;
    component.request = request;
    component.model!.isNewDriver = true;
    component.changeDriverStatus();
    expect(component.model!.assistType).toBe(AssistType.Unspecified);
    expect(component.roleAllowed('Chaufför(Själv lastn)')).toBeFalse();
    component.page!.rows[0].tt = false;
    component.model!.firstPage = component.page!;
    component.request = component.model;
    expect(component.page!.rows[0].tt).toBeFalse();
  });

  it('uses a dropdown containing all products and switches station without mixing products', () => {
    const fixture = TestBed.createComponent(ChecklistEditModalComponent);
    const component = fixture.componentInstance;
    component.products = products;
    component.isOpen = true;
    component.request = request;
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelectorAll('#edit-product option').length).toBe(3);
    component.addProduct('BDP865');
    expect(component.model!.selectedProducts.map(p => p.name)).toEqual(['BDP 865']);
    expect(component.page!.unNumbers).toEqual(['UN 3264']);
  });

  it('keeps input nodes stable across edits despite template-backed row getters', () => {
    const fixture = TestBed.createComponent(ChecklistEditModalComponent);
    fixture.componentInstance.isOpen = true;
    fixture.componentInstance.request = request;
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    const textarea = element.querySelector('textarea');
    fixture.componentInstance.page!.rows[0].comment = 'typing';
    fixture.detectChanges();
    expect(element.querySelector('textarea')).toBe(textarea);
  });
});

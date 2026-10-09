import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AppComponent } from './app.component';
import { HttpTestingController } from '@angular/common/http/testing';
import { ChecklistTemplate, ProductDefinition } from './models';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should default the operator name to Vakt', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app.operatorName).toEqual('Vakt');
  });

  it('should render the header title', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h2')?.textContent).toContain('Checklista Generator');
  });

  it('should fill PH Tank when a known driver is selected', () => {
    const app = TestBed.createComponent(AppComponent).componentInstance;
    app.akeri = 'Previous carrier';
    app.onIsNewDriverChange(false);
    expect(app.akeri).toBe('PH Tank');
  });

  it('identifies an Excel source and does not calculate demo inspection data', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(r => r.url.endsWith('/init-data')).flush({
      defaultOperator: 'Vakt', operators: [], drivers: [], products: [],
      trucks: [{ regNr: 'MBP 94C', tankCode: 'L4BN', approvalExpiry: '2027-03-19', trailers: [] }],
      vehicleRegistrySource: 'Regnummer.xlsx',
    });
    fixture.detectChanges();
    const app = fixture.componentInstance;
    expect(app.trucks[0].regNr).toBe('MBP 94C');
    expect(app.drivers).toEqual([]);
    app.selectedTruckReg = 'MBP 94C';
    expect(app.calculateTankSlots()[0].tankCode).toBe('L4BN');
    expect(app.calculateTankSlots().every(s =>
      !s.inspectionType && !s.lastInspectionMonthYear && !s.expiryFormatted)).toBeTrue();
    expect((fixture.nativeElement as HTMLElement).querySelector('[role="status"]')?.textContent)
      .toContain('Regnummer.xlsx');
    http.verify();
  });

  it('carries registry ADR and trailer descriptions through preview, drafts and generation without invented inspection data', () => {
    const app = TestBed.createComponent(AppComponent).componentInstance;
    app.vehicleRegistrySource = 'Regnummer.xlsx';
    app.trucks = [{ regNr: 'AAA 123', tankCode: 'ADR', approvalExpiry: '2027-06-17', trailers: [] }];
    app.registeredTrailers = [{
      regNr: 'BBB 123', tankCode: 'FACK 1&3: L4BH (+)VP FACK 2: L4BH',
      approvalExpiry: '2026-09-30', compartments: [],
    }];
    app.selectedTruckReg = 'aaa123';
    app.selectedTrailer1Reg = 'BBB123';
    app.selectedDriverName = 'Test Driver';
    app.driverAdrExpiry = '2030-01-01';
    app.products = [{
      code: 'PIX311', displayName: 'PIX 311', family: 'PIX', unNumber: 'UN 2582',
      template: ChecklistTemplate.Type1_PixPaxSasBdp, loadingStationId: 'PIX', selected: true,
    }];
    expect(app.calculateTankSlots().map(s => s.tankCode)).toEqual(['ADR', app.registeredTrailers[0].tankCode!, '']);
    app.openDraft(app.getPrintJobs()[0]);
    expect(app.editingRequest!.tankSlots[0].tankCode).toBe('ADR');
    expect(app.editingRequest!.tankSlots[1].tankCode).toContain('FACK 2');
    app.onEditSave(app.editingRequest!);
    app.generateChecklists();
    const http = TestBed.inject(HttpTestingController);
    const call = http.expectOne(r => r.url.endsWith('/generate'));
    expect(call.request.body.tankSlots[0].tankCode).toBe('ADR');
    expect(call.request.body.tankSlots[1].tankCode).toContain('FACK 1&3');
    expect(call.request.body.tankSlots.every((s: { inspectionType: string; lastInspectionMonthYear: string; expiryFormatted: string }) =>
      !s.inspectionType && !s.lastInspectionMonthYear && !s.expiryFormatted)).toBeTrue();
    call.flush(new Blob(['test']));
    app.selectedTruckReg = 'UNKNOWN';
    expect(app.calculateTankSlots()[0].tankCode).toBe('');
    app.selectedTruckReg = 'AAA 123';
    app.selectedTrailer1Reg = '';
    app.selectedTrailer2Reg = 'BBB 123';
    expect(app.calculateTankSlots().map(s => s.tankCode)).toEqual(['ADR', '', app.registeredTrailers[0].tankCode!]);
    http.verify();
  });

  it('shows the Excel import error returned by the backend', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(r => r.url.endsWith('/init-data')).flush(
      { detail: 'Blad Vehicles, rad 4: ogiltigt giltighetsdatum.' },
      { status: 503, statusText: 'Service Unavailable' },
    );
    expect(fixture.componentInstance.errorMessage).toContain('rad 4');
    expect(fixture.componentInstance.trucks).toEqual([]);
    http.verify();
  });

  it('does not describe an unexpected server error as a stopped backend', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(r => r.url.endsWith('/init-data')).flush(
      null, { status: 500, statusText: 'Internal Server Error' },
    );
    expect(fixture.componentInstance.errorMessage).toContain('konsolfönster');
    expect(fixture.componentInstance.errorMessage).not.toContain('Är backend igång');
    http.verify();
  });

  it('uses the imported driver carrier and truck association instead of PH Tank and the first truck', () => {
    const app = TestBed.createComponent(AppComponent).componentInstance;
    app.vehicleRegistrySource = 'Regnummer.xlsx';
    app.selectedDriverName = 'Test Driver';
    app.drivers = [{ name: 'Test Driver', adrExpiry: '2030-01-01', haulier: 'Carrier A', truckRegNrs: ['BBB123'] }];
    app.trucks = [
      { regNr: 'AAA123', tankCode: 'L4BH', approvalExpiry: '2027-01-01', trailers: [] },
      { regNr: 'BBB 123', tankCode: 'ADR', approvalExpiry: '2028-01-01', trailers: [] },
    ];
    app.onIsNewDriverChange(false);
    expect(app.akeri).toBe('Carrier A');
    expect(app.selectedTruckReg).toBe('BBB 123');
    expect(app.truckTankCode).toBe('ADR');
    app.drivers[0].haulier = null;
    app.onIsNewDriverChange(false);
    expect(app.akeri).toBe('');
  });

  it('requires manual verification of a missing ADR date before Excel-mode generation', () => {
    const app = TestBed.createComponent(AppComponent).componentInstance;
    app.vehicleRegistrySource = 'Regnummer.xlsx';
    app.selectedDriverName = 'Test Driver';
    app.generateChecklists();
    expect(app.errorMessage).toContain('ADR-datum');
    TestBed.inject(HttpTestingController).expectNone(r => r.url.endsWith('/generate'));
  });

  it('does not infer self-loading authorization from an imported driver', () => {
    const app = TestBed.createComponent(AppComponent).componentInstance;
    app.vehicleRegistrySource = 'Regnummer.xlsx';
    app.products = [{
      code: 'PIX311', displayName: 'PIX 311', family: 'PIX', unNumber: 'UN 2582',
      template: ChecklistTemplate.Type1_PixPaxSasBdp, loadingStationId: 'PIX', selected: true,
    }];
    app.selectedDriverName = 'Test Driver';
    app.driverAdrExpiry = '2030-01-01';
    app.openDraft(app.getPrintJobs()[0]);
    expect(app.editingRequest!.assistType).toBe(0);
  });

  it('should clear PH Tank when switching to an unknown driver', () => {
    const app = TestBed.createComponent(AppComponent).componentInstance;
    app.onIsNewDriverChange(false);
    app.onIsNewDriverChange(true);
    expect(app.akeri).toBe('');
  });

  it('should preserve a manually entered carrier for an unknown driver', () => {
    const app = TestBed.createComponent(AppComponent).componentInstance;
    app.akeri = 'Manual carrier';
    app.onIsNewDriverChange(true);
    expect(app.akeri).toBe('Manual carrier');
  });

  it('saves an editable draft without calling generation and uses it when generating', () => {
    const app = TestBed.createComponent(AppComponent).componentInstance;
    const product: ProductDefinition = {
      code: 'PIX311', displayName: 'PIX 311', family: 'PIX', unNumber: 'UN 2582',
      template: ChecklistTemplate.Type1_PixPaxSasBdp, loadingStationId: 'PIX', selected: true,
    };
    app.products = [product];
    app.selectedDriverName = 'Testing';
    app.driverAdrExpiry = '2028-01-01';
    app.selectedTruckReg = 'ABC123';
    const http = TestBed.inject(HttpTestingController);
    app.openDraft(app.getPrintJobs()[0]);
    expect(app.editingRequest).not.toBeNull();
    const edited = structuredClone(app.editingRequest!);
    edited.driverName = 'Edited before generation';
    app.onEditSave(edited);
    http.expectNone(r => r.url.endsWith('/generate'));
    expect(app.drafts.size).toBe(1);
    app.generateChecklists();
    const call = http.expectOne(r => r.url.endsWith('/generate'));
    expect(call.request.body.driverName).toBe('Edited before generation');
    call.flush(new Blob(['test']));
    expect(app.generatedChecklists[0].wasEdited).toBeTrue();
    expect(app.editingRequest).toBeNull();
    http.verify();
  });

  it('requires reviewing a draft again if the underlying vehicle or driver changes', () => {
    const app = TestBed.createComponent(AppComponent).componentInstance;
    app.products = [{
      code: 'PIX311', displayName: 'PIX 311', family: 'PIX', unNumber: 'UN 2582',
      template: ChecklistTemplate.Type1_PixPaxSasBdp, loadingStationId: 'PIX', selected: true,
    }];
    app.openDraft(app.getPrintJobs()[0]);
    app.onEditSave(app.editingRequest!);
    app.selectedTruckReg = 'DIFFERENT';
    app.generateChecklists();
    expect(app.errorMessage).toContain('Underlaget har ändrats');
    TestBed.inject(HttpTestingController).expectNone(r => r.url.endsWith('/generate'));
  });

  it('passes manual container numbers to drafts and generation and omits them when TC is off', () => {
    const app = TestBed.createComponent(AppComponent).componentInstance;
    app.products = [{
      code: 'PIX311', displayName: 'PIX 311', family: 'PIX', unNumber: 'UN 2582',
      template: ChecklistTemplate.Type1_PixPaxSasBdp, loadingStationId: 'PIX', selected: true,
    }];
    app.truckIsTankContainer = true;
    app.truckContainerNumber = ' CONT-123 ';
    app.trailer1IsTankContainer = true;
    app.trailer1ContainerNumber = ' CONT-456 ';
    app.trailer2IsTankContainer = true;
    app.trailer2ContainerNumber = ' CONT-789 ';
    app.openDraft(app.getPrintJobs()[0]);
    expect(app.editingRequest!.firstPage?.containerNumber).toBe('CONT-123 / CONT-456 / CONT-789');
    app.onEditSave(app.editingRequest!);
    app.generateChecklists();
    const http = TestBed.inject(HttpTestingController);
    const call = http.expectOne(r => r.url.endsWith('/generate'));
    expect(call.request.body.firstPage.containerNumber).toBe('CONT-123 / CONT-456 / CONT-789');
    call.flush(new Blob(['test']));
    app.truckIsTankContainer = false;
    app.trailer1IsTankContainer = false;
    app.trailer2IsTankContainer = false;
    app.openDraft(app.getPrintJobs()[0]);
    expect(app.editingRequest!.firstPage?.containerNumber).toBe('');
    app.onEditSave(app.editingRequest!);
    app.generateChecklists();
    const withoutContainer = http.expectOne(r => r.url.endsWith('/generate'));
    expect(withoutContainer.request.body.firstPage.containerNumber).toBe('');
    withoutContainer.flush(new Blob(['test']));
    http.verify();
  });
});

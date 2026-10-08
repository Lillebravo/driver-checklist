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
});

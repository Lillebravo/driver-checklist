import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AppComponent } from './app.component';

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
});

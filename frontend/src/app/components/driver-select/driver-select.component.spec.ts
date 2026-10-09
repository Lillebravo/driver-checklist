import { DriverSelectComponent } from './driver-select.component';

describe('DriverSelectComponent', () => {
  it('uses a known ADR date and clears it for an imported driver without a date', () => {
    const component = new DriverSelectComponent();
    component.drivers = [
      { name: 'Known Driver', adrExpiry: '2030-01-01' },
      { name: 'Missing Date', adrExpiry: '' },
    ];
    const changes: string[] = [];
    component.adrExpiryChange.subscribe(value => changes.push(value));
    component.onNameInput('Known Driver');
    component.onNameInput('Missing Date');
    expect(changes).toEqual(['2030-01-01', '']);
    expect(component.adrExpiry).toBe('');
  });

  it('does not carry a previous driver ADR date into manual entry', () => {
    const component = new DriverSelectComponent();
    component.drivers = [{ name: 'Known Driver', adrExpiry: '2030-01-01' }];
    component.onNameInput('Known Driver');
    component.onNameInput('New Driver');
    expect(component.adrExpiry).toBe('');
  });
});

import { ChecklistTemplate, ProductDefinition } from '../models';
import { PrintJobPlannerService } from './print-job-planner.service';

describe('PrintJobPlannerService', () => {
  const product = (code: string, family: string, loadingStationId: string): ProductDefinition => ({
    code,
    displayName: code.replace('_', ' '),
    family,
    unNumber: 'UN 1760',
    template: ChecklistTemplate.Type1_PixPaxSasBdp,
    loadingStationId,
    selected: true,
  });

  it('creates separate checklists for PAX and BDP while grouping variants within each family', () => {
    const planner = new PrintJobPlannerService();
    const products = [
      product('PAX_15', 'PAX', 'STATION_PAX'),
      product('PAX_60', 'PAX', 'STATION_PAX'),
      product('BDP_865', 'BDP', 'STATION_BDP'),
      product('BDP_870', 'BDP', 'STATION_BDP'),
    ];

    const jobs = planner.getPrintJobs(products);

    expect(jobs.length).toBe(2);
    expect(jobs.map(job => job.station)).toEqual(['STATION_PAX', 'STATION_BDP']);
    expect(jobs.map(job => job.products.length)).toEqual([2, 2]);
  });
});

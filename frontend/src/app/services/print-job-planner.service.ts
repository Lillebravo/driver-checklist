import { Injectable } from '@angular/core';
import { ProductDefinition, PrintJob } from '../models';

/**
 * Grupperar valda produkter per unik kombination av (ChecklistType, LoadingStationId)
 * enligt README.md avsnitt 2.2 - varje grupp motsvarar exakt en utskriven checklista.
 */
@Injectable({ providedIn: 'root' })
export class PrintJobPlannerService {
  getPrintJobs(products: ProductDefinition[]): PrintJob[] {
    const selected = products.filter((p) => p.selected);
    const groups = new Map<string, PrintJob>();

    selected.forEach((p) => {
      const key = `${p.template}_${p.loadingStationId}`;
      if (!groups.has(key)) {
        groups.set(key, { template: p.template, station: p.loadingStationId, products: [] });
      }
      groups.get(key)!.products.push(p);
    });

    return Array.from(groups.values());
  }
}

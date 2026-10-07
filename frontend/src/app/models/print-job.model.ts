import { ChecklistTemplate } from './checklist-template.enum';
import { ProductDefinition } from './product.model';

/**
 * En grupp av valda produkter som delar checklistemall och utlastningsplats
 * (station) - motsvarar exakt en utskriven checklista, se README.md avsnitt 2.2.
 */
export interface PrintJob {
  template: ChecklistTemplate;
  station: string;
  products: ProductDefinition[];
}

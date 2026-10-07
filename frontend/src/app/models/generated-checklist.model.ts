import { PrintJob } from './print-job.model';
import { GenerateChecklistRequest } from './generate-checklist-request.model';

/**
 * En genererad checklista som hålls i minnet i frontend efter att
 * `POST /api/checklist/generate` anropats. `request` sparas så att samma
 * checklista kan redigeras (regenereras med nya värden) via
 * `ChecklistEditModalComponent` utan att behöva fylla i allt på nytt.
 */
export interface GeneratedChecklist {
  /** Unik nyckel inom sessionen, t.ex. stationens id. */
  key: string;
  printJob: PrintJob;
  request: GenerateChecklistRequest;
  blob: Blob;
  fileName: string;
  /** True efter att checklistan redigerats och regenererats minst en gång. */
  wasEdited: boolean;
}

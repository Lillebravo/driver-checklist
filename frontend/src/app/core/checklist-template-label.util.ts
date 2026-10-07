import { ChecklistTemplate } from '../models';

/** Läsbara svenska etiketter för de olika checklistemallarna, används i UI. */
const LABELS: Record<ChecklistTemplate, string> = {
  [ChecklistTemplate.Type1_PixPaxSasBdp]: 'Typ 1 (Saltsyra / PIX / PAX / BDP)',
  [ChecklistTemplate.Type2_SvsAkd]: 'Typ 2 (Svavelsyra / Fennosize)',
  [ChecklistTemplate.Type3_AlsLut]: 'Typ 3 (ALS / LUT)',
};

export function checklistTemplateLabel(template: ChecklistTemplate): string {
  return LABELS[template] ?? `Mall ${template}`;
}

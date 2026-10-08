import { AssistType } from '../models';

/** Läsbara svenska etiketter för lastningsassistans, används i redigeringsmodalen. */
const LABELS: Record<AssistType, string> = {
  [AssistType.Unspecified]: 'Ej specificerat (inget ikryssat)',
  [AssistType.FullAssist]: 'Full assist',
  [AssistType.HalfAssist]: 'Halv assist',
  [AssistType.SelfLoading]: 'Själv lastn',
};

export function assistTypeLabel(assistType: AssistType): string {
  return LABELS[assistType] ?? `${assistType}`;
}

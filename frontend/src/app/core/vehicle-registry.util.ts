import { Trailer, Truck } from '../models';

export const normalizeRegNr = (value: string): string => value.replace(/\s/g, '').toUpperCase();

export function allTrailers(trucks: Truck[]): Trailer[] {
  const trailers = new Map<string, Trailer>();
  for (const truck of trucks) {
    for (const trailer of truck.trailers) {
      const key = normalizeRegNr(trailer.regNr);
      if (!trailers.has(key)) trailers.set(key, trailer);
    }
  }
  return [...trailers.values()];
}

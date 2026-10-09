import { Trailer, Truck } from '../models';

export const normalizeRegNr = (value: string): string => value.replace(/[\s-]/g, '').toUpperCase();

export function allTrailers(trucks: Truck[], registeredTrailers: Trailer[] = []): Trailer[] {
  const trailers = new Map<string, Trailer>();
  for (const trailer of registeredTrailers) {
    trailers.set(normalizeRegNr(trailer.regNr), trailer);
  }
  for (const truck of trucks) {
    for (const trailer of truck.trailers) {
      const key = normalizeRegNr(trailer.regNr);
      if (!trailers.has(key)) trailers.set(key, trailer);
    }
  }
  return [...trailers.values()];
}

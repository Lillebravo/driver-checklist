/**
 * Vem som assisterar lastningen. Värdena motsvarar exakt backendens
 * `AssistType`-enum (DriverChecklist.Api.Models.Enums) och styr vilken
 * kryssruta som kryssas i vid E16 samt Operatör/Chaufför-rutorna längre ner
 * i dokumentet (A21/A28/A33). `Unspecified` kryssar medvetet ingenting.
 */
export enum AssistType {
  Unspecified = 0,
  FullAssist = 1,
  HalfAssist = 2,
  SelfLoading = 3,
}

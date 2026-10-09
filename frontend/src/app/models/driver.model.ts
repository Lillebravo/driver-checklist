/** En chaufför enligt master-Excelen, med ADR-utgångsdatum. */
export interface Driver {
  name: string;
  adrExpiry: string;
  haulier?: string | null;
  truckRegNrs?: string[] | null;
}

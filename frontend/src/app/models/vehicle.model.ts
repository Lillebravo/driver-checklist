/** Ett fack (compartment) på en släpvagn, med egen tankkod och eget provtryckningsdatum. */
export interface Compartment {
  compartmentNo: number;
  tankCode: string;
  lastTest: string;
  testType: string;
  isTankContainer: boolean;
}

/** Ett släp som en dragbil brukar köra med, inklusive dess fack. */
export interface Trailer {
  regNr: string;
  approvalExpiry: string;
  compartments: Compartment[];
}

/** En dragbil och dess vanliga släpkombinationer enligt master-Excelen. */
export interface Truck {
  regNr: string;
  tankCode: string;
  approvalExpiry: string;
  trailers: Trailer[];
}

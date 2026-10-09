import { Driver } from './driver.model';
import { Trailer, Truck } from './vehicle.model';
import { ProductDefinition } from './product.model';

/** Svaret från `GET /api/init-data`. */
export interface InitDataResponse {
  defaultOperator: string;
  operators: string[];
  drivers: Driver[];
  trucks: Truck[];
  trailers?: Trailer[] | null;
  products: ProductDefinition[];
  vehicleRegistrySource?: string | null;
  importWarnings?: string[] | null;
}

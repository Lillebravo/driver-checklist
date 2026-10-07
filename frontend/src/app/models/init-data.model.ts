import { Driver } from './driver.model';
import { Truck } from './vehicle.model';
import { ProductDefinition } from './product.model';

/** Svaret från `GET /api/init-data`. */
export interface InitDataResponse {
  defaultOperator: string;
  drivers: Driver[];
  trucks: Truck[];
  products: ProductDefinition[];
}

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../core/api.config';
import { InitDataResponse, GenerateChecklistRequest } from '../models';

/** Tunt HTTP-lager mot DriverChecklist.Api. Innehåller ingen affärslogik. */
@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private readonly http: HttpClient) {}

  getInitData(): Observable<InitDataResponse> {
    return this.http.get<InitDataResponse>(`${API_BASE_URL}/api/init-data`);
  }

  generateChecklist(request: GenerateChecklistRequest): Observable<Blob> {
    return this.http.post(`${API_BASE_URL}/api/checklist/generate`, request, {
      responseType: 'blob',
    });
  }
}

import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { EventFeatureCollection } from './event.model';

@Injectable({ providedIn: 'root' })
export class EventService {
  private http = inject(HttpClient);
  private base = environment.apiBase;

  getEvents(eventType?: string, countyFips?: string): Observable<EventFeatureCollection> {
    let params = new HttpParams();
    if (eventType)   params = params.set('type', eventType);
    if (countyFips)  params = params.set('county', countyFips);
    return this.http.get<EventFeatureCollection>(`${this.base}/events`, { params });
  }
}

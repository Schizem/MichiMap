import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class SubmissionService {
  private http = inject(HttpClient);
  private base = environment.apiBase;

  getCounties(): Observable<string[]> {
    return this.http.get<string[]>(`${this.base}/submissions/counties`);
  }

  submitMorel(formData: FormData): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base}/submissions/morel`, formData);
  }
}

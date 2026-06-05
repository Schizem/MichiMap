import { Component, input, output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule }    from '@angular/material/icon';
import { MatButtonModule }  from '@angular/material/button';
import { MatChipsModule }   from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import {
  EventFeature, EventType, Severity,
  EVENT_LAYER_CONFIG, SEVERITY_COLOR
} from '../events/event.model';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule, MatChipsModule, MatDividerModule],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss'
})
export class SidebarComponent {
  event = input.required<EventFeature>();
  closed = output<void>();

  get cfg() {
    return EVENT_LAYER_CONFIG[this.event().properties.eventType];
  }

  get severityColor(): string | null {
    const sev = this.event().properties.severity as Severity | null;
    return sev ? SEVERITY_COLOR[sev] : null;
  }

  get isLive(): boolean {
    return this.event().properties.eventYear === null;
  }

  get formattedFetchDate(): string {
    return new Date(this.event().properties.fetchedAt).toLocaleString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric',
      hour: 'numeric', minute: '2-digit'
    });
  }

  // Only show the expiry date when it's soon (within 14 days) - for historical
  // records the expiry is a system cleanup date that is not meaningful to users.
  get showExpires(): boolean {
    const exp = this.event().properties.expiresAt;
    if (!exp) return false;
    return (new Date(exp).getTime() - Date.now()) < 14 * 24 * 60 * 60 * 1000;
  }
}

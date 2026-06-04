import { Component, signal, viewChild, inject, computed, effect } from '@angular/core';
import { DOCUMENT }         from '@angular/common';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule }  from '@angular/material/button';
import { MatIconModule }    from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MapComponent }     from './map/map.component';
import { SidebarComponent } from './sidebar/sidebar.component';
import { EventFeature, EventType, EVENT_LAYER_CONFIG } from './events/event.model';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [MatToolbarModule, MatButtonModule, MatIconModule, MatTooltipModule, MapComponent, SidebarComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private doc = inject(DOCUMENT);

  selectedEvent   = signal<EventFeature | null>(null);
  activeTypes     = signal<ReadonlySet<EventType>>(new Set<EventType>());
  hasActiveFilter = computed(() => this.activeTypes().size > 0);
  layerEntries    = Object.entries(EVENT_LAYER_CONFIG) as [EventType, typeof EVENT_LAYER_CONFIG[EventType]][];

  darkMode = signal(
    localStorage.getItem('michimap-theme') === 'dark' ||
    (localStorage.getItem('michimap-theme') === null &&
     window.matchMedia('(prefers-color-scheme: dark)').matches)
  );

  private map = viewChild(MapComponent);

  constructor() {
    effect(() => {
      const dark = this.darkMode();
      this.doc.documentElement.classList.toggle('dark-theme', dark);
      localStorage.setItem('michimap-theme', dark ? 'dark' : 'light');
    });
  }

  toggleDarkMode() {
    this.darkMode.update(v => !v);
  }

  onEventSelected(event: EventFeature | null) {
    this.selectedEvent.set(event);
  }

  closePanel() {
    this.selectedEvent.set(null);
  }

  toggleType(type: EventType) {
    const next = new Set(this.activeTypes());
    if (next.has(type)) next.delete(type);
    else next.add(type);
    this.activeTypes.set(next);
  }

  clearFilters() {
    this.activeTypes.set(new Set<EventType>());
  }

  isTypeVisible(type: EventType): boolean {
    return !this.hasActiveFilter() || this.activeTypes().has(type);
  }
}

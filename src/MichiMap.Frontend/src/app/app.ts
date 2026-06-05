import { Component, signal, viewChild, inject, computed, effect } from '@angular/core';
import { DOCUMENT }         from '@angular/common';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule }  from '@angular/material/button';
import { MatIconModule }    from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MapComponent }        from './map/map.component';
import { SidebarComponent }     from './sidebar/sidebar.component';
import { AboutPanelComponent }  from './about-panel/about-panel.component';
import { EventFeature, EventType, EVENT_LAYER_CONFIG } from './events/event.model';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [MatToolbarModule, MatButtonModule, MatIconModule, MatTooltipModule, MapComponent, SidebarComponent, AboutPanelComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private doc = inject(DOCUMENT);

  selectedEvent = signal<EventFeature | null>(null);
  showAbout     = signal(false);
  layerEntries  = Object.entries(EVENT_LAYER_CONFIG) as [EventType, typeof EVENT_LAYER_CONFIG[EventType]][];

  // Set of types the user has clicked to SHOW. Empty = show nothing.
  selectedTypes   = signal<ReadonlySet<EventType>>(new Set<EventType>());
  hasSelection    = computed(() => this.selectedTypes().size > 0);

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

  toggleDarkMode() { this.darkMode.update(v => !v); }

  onEventSelected(event: EventFeature | null) { this.selectedEvent.set(event); }
  closePanel() { this.selectedEvent.set(null); }

  toggleAbout() { this.showAbout.update(v => !v); }
  closeAbout()  { this.showAbout.set(false); }

  toggleType(type: EventType) {
    const next = new Set(this.selectedTypes());
    if (next.has(type)) next.delete(type);
    else next.add(type);
    this.selectedTypes.set(next);
  }

  clearSelection() { this.selectedTypes.set(new Set<EventType>()); }

  isSelected(type: EventType): boolean { return this.selectedTypes().has(type); }
}

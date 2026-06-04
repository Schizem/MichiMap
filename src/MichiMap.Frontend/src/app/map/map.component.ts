import {
  Component, OnInit, OnDestroy, output, inject,
  signal, input, effect
} from '@angular/core';
import { CommonModule }             from '@angular/common';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import * as L                       from 'leaflet';
import { EventService }             from '../events/event.service';
import {
  EventFeature, EventType, EVENT_LAYER_CONFIG, SEVERITY_COLOR, Severity
} from '../events/event.model';

const MICHIGAN_CENTER: L.LatLngExpression = [44.3, -84.5];
const MICHIGAN_ZOOM = 7;

@Component({
  selector: 'app-map',
  standalone: true,
  imports: [CommonModule, MatProgressSpinnerModule],
  templateUrl: './map.component.html',
  styleUrl: './map.component.scss'
})
export class MapComponent implements OnInit, OnDestroy {
  private eventService = inject(EventService);

  // Parent passes the active filter set; empty set means show all types.
  activeTypes = input<ReadonlySet<EventType>>(new Set<EventType>());

  eventSelected = output<EventFeature | null>();

  loading = signal(true);
  error   = signal<string | null>(null);

  private leafletMap!: L.Map;
  private layerGroups = new Map<EventType, L.LayerGroup>();

  constructor() {
    // Re-apply layer visibility whenever the parent changes the active filter.
    effect(() => {
      if (this.leafletMap) this.applyLayerVisibility(this.activeTypes());
    });
  }

  ngOnInit() {
    this.leafletMap = L.map('map', {
      center: MICHIGAN_CENTER,
      zoom:   MICHIGAN_ZOOM,
      zoomControl: true
    });

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
      maxZoom: 18
    }).addTo(this.leafletMap);

    for (const type of Object.keys(EVENT_LAYER_CONFIG) as EventType[]) {
      const group = L.layerGroup().addTo(this.leafletMap);
      this.layerGroups.set(type, group);
    }

    this.loadEvents();
  }

  ngOnDestroy() {
    this.leafletMap?.remove();
  }

  private loadEvents() {
    this.loading.set(true);
    this.error.set(null);

    this.eventService.getEvents().subscribe({
      next: (fc) => {
        this.renderMarkers(fc.features);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load events. Is the API running?');
        this.loading.set(false);
      }
    });
  }

  private renderMarkers(features: EventFeature[]) {
    for (const group of this.layerGroups.values()) group.clearLayers();

    for (const feature of features) {
      const [lng, lat] = feature.geometry.coordinates;
      const type       = feature.properties.eventType;
      const cfg        = EVENT_LAYER_CONFIG[type];
      const severity   = feature.properties.severity as Severity | null;
      const fillColor  = severity ? SEVERITY_COLOR[severity] : cfg.color;

      const marker = L.circleMarker([lat, lng], {
        radius:      8,
        color:       cfg.color,
        fillColor,
        fillOpacity: 0.85,
        weight:      2,
        className:   `event-marker event-marker-${type.toLowerCase()}`
      });

      marker.bindTooltip(feature.properties.title, { direction: 'top', offset: [0, -8] });
      marker.on('click', () => this.eventSelected.emit(feature));

      this.layerGroups.get(type)?.addLayer(marker);
    }

    // Re-apply the current filter after a data reload so newly added markers
    // immediately respect whatever the parent has selected.
    this.applyLayerVisibility(this.activeTypes());
  }

  private applyLayerVisibility(active: ReadonlySet<EventType>) {
    for (const [type, group] of this.layerGroups) {
      const visible = active.size === 0 || active.has(type);
      if (visible && !this.leafletMap.hasLayer(group))  this.leafletMap.addLayer(group);
      if (!visible && this.leafletMap.hasLayer(group))  this.leafletMap.removeLayer(group);
    }
  }

  reload() {
    this.loadEvents();
  }
}

import {
  Component, OnInit, OnDestroy, output, inject,
  signal, computed
} from '@angular/core';
import { CommonModule }   from '@angular/common';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import * as L from 'leaflet';
import { EventService }  from '../events/event.service';
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

  eventSelected = output<EventFeature | null>();

  loading   = signal(true);
  error     = signal<string | null>(null);
  activeType = signal<EventType | null>(null);

  layerConfigs = Object.entries(EVENT_LAYER_CONFIG) as [EventType, typeof EVENT_LAYER_CONFIG[EventType]][];

  private map!: L.Map;
  private layerGroups = new Map<EventType, L.LayerGroup>();
  private allFeatures: EventFeature[] = [];

  ngOnInit() {
    this.map = L.map('map', {
      center: MICHIGAN_CENTER,
      zoom:   MICHIGAN_ZOOM,
      zoomControl: true
    });

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
      maxZoom: 18
    }).addTo(this.map);

    for (const type of Object.keys(EVENT_LAYER_CONFIG) as EventType[]) {
      const group = L.layerGroup().addTo(this.map);
      this.layerGroups.set(type, group);
    }

    this.loadEvents();
  }

  ngOnDestroy() {
    this.map?.remove();
  }

  private loadEvents() {
    this.loading.set(true);
    this.error.set(null);

    this.eventService.getEvents().subscribe({
      next: (fc) => {
        this.allFeatures = fc.features;
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
        weight:      2
      });

      marker.bindTooltip(feature.properties.title, { direction: 'top', offset: [0, -8] });
      marker.on('click', () => this.eventSelected.emit(feature));

      this.layerGroups.get(type)?.addLayer(marker);
    }
  }

  toggleType(type: EventType) {
    if (this.activeType() === type) {
      this.activeType.set(null);
      for (const [t, group] of this.layerGroups) {
        if (!this.map.hasLayer(group)) this.map.addLayer(group);
      }
    } else {
      this.activeType.set(type);
      for (const [t, group] of this.layerGroups) {
        if (t === type) {
          if (!this.map.hasLayer(group)) this.map.addLayer(group);
        } else {
          if (this.map.hasLayer(group)) this.map.removeLayer(group);
        }
      }
    }
  }

  isActive(type: EventType): boolean {
    return this.activeType() === null || this.activeType() === type;
  }

  reload() {
    this.loadEvents();
  }

  get layerConfigEntries() {
    return this.layerConfigs;
  }
}

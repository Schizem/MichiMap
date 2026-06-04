export type EventType = 'FLOOD' | 'WILDFIRE' | 'BURN' | 'AIR_QUALITY' | 'FISH_STOCK';
export type Severity  = 'LOW' | 'MODERATE' | 'HIGH' | 'CRITICAL';

export interface EventProperties {
  eventId:    string;
  eventType:  EventType;
  title:      string;
  description: string | null;
  severity:   Severity | null;
  countyFips: string | null;
  sourceUrl:  string | null;
  fetchedAt:  string;
  expiresAt:  string | null;
}

export interface EventFeature {
  type:       'Feature';
  geometry:   { type: 'Point'; coordinates: [number, number] };
  properties: EventProperties;
}

export interface EventFeatureCollection {
  type:     'FeatureCollection';
  features: EventFeature[];
}

export interface LayerConfig {
  label: string;
  color: string;
  icon:  string;
}

export const EVENT_LAYER_CONFIG: Record<EventType, LayerConfig> = {
  FLOOD:       { label: 'Flood',           color: '#2196F3', icon: '🌊' },
  WILDFIRE:    { label: 'Wildfire',        color: '#FF5722', icon: '🔥' },
  BURN:        { label: 'Controlled Burn', color: '#FF9800', icon: '🌿' },
  AIR_QUALITY: { label: 'Air Quality',     color: '#9C27B0', icon: '💨' },
  FISH_STOCK:  { label: 'Fish Atlas',      color: '#4CAF50', icon: '🐟' }
};

export const SEVERITY_COLOR: Record<Severity, string> = {
  LOW:      '#8BC34A',
  MODERATE: '#FFC107',
  HIGH:     '#FF5722',
  CRITICAL: '#B71C1C'
};

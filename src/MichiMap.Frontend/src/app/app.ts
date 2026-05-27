import { Component, signal, viewChild, inject } from '@angular/core';
import { MatToolbarModule }  from '@angular/material/toolbar';
import { MatButtonModule }   from '@angular/material/button';
import { MatIconModule }     from '@angular/material/icon';
import { MatDialog }         from '@angular/material/dialog';
import { MapComponent }      from './map/map.component';
import { SidebarComponent }  from './sidebar/sidebar.component';
import { MorelFormComponent } from './submissions/morel-form/morel-form.component';
import { EventFeature }      from './events/event.model';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [MatToolbarModule, MatButtonModule, MatIconModule, MapComponent, SidebarComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private dialog = inject(MatDialog);

  selectedEvent = signal<EventFeature | null>(null);
  private map = viewChild(MapComponent);

  onEventSelected(event: EventFeature | null) {
    this.selectedEvent.set(event);
  }

  closePanel() {
    this.selectedEvent.set(null);
  }

  openSubmitDialog() {
    this.dialog.open(MorelFormComponent, { width: '420px' })
      .afterClosed()
      .subscribe(submitted => {
        if (submitted) this.map()?.reload();
      });
  }
}

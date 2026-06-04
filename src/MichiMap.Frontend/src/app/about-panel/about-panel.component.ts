import { Component, output } from '@angular/core';
import { MatIconModule }    from '@angular/material/icon';
import { MatButtonModule }  from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';

@Component({
  selector: 'app-about-panel',
  standalone: true,
  imports: [MatIconModule, MatButtonModule, MatDividerModule],
  templateUrl: './about-panel.component.html',
  styleUrl: './about-panel.component.scss'
})
export class AboutPanelComponent {
  closed = output<void>();
}

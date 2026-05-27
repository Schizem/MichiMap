import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule }        from '@angular/common';
import { ReactiveFormsModule, FormControl, FormGroup, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule }  from '@angular/material/form-field';
import { MatSelectModule }     from '@angular/material/select';
import { MatInputModule }      from '@angular/material/input';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatButtonModule }     from '@angular/material/button';
import { MatIconModule }       from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { SubmissionService }   from '../submission.service';

@Component({
  selector: 'app-morel-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './morel-form.component.html',
  styleUrl:    './morel-form.component.scss'
})
export class MorelFormComponent implements OnInit {
  private submissions = inject(SubmissionService);
  private dialogRef   = inject(MatDialogRef<MorelFormComponent>);

  counties  = signal<string[]>([]);
  submitting = signal(false);
  serverError = signal<string | null>(null);

  readonly today   = new Date();
  readonly minDate = new Date(Date.now() - 180 * 24 * 60 * 60 * 1000);

  photoFile:  File | null = null;
  photoError: string | null = null;

  form = new FormGroup({
    county:       new FormControl<string>('',   Validators.required),
    observedDate: new FormControl<Date | null>(null, Validators.required),
    notes:        new FormControl<string>('',   Validators.maxLength(1000))
  });

  ngOnInit() {
    this.submissions.getCounties().subscribe(list => this.counties.set(list));
  }

  onFileChange(event: Event) {
    this.photoError = null;
    this.photoFile  = null;
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    if (!file) return;

    const allowed = ['image/jpeg', 'image/png', 'image/webp'];
    if (!allowed.includes(file.type)) {
      this.photoError = 'Photo must be JPEG, PNG, or WebP.';
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      this.photoError = 'Photo must be 5 MB or smaller.';
      return;
    }
    this.photoFile = file;
  }

  clearPhoto() {
    this.photoFile  = null;
    this.photoError = null;
  }

  get notesLength(): number {
    return this.form.value.notes?.length ?? 0;
  }

  submit() {
    if (this.form.invalid || this.submitting()) return;
    this.serverError.set(null);
    this.submitting.set(true);

    const { county, observedDate, notes } = this.form.value;
    const d = observedDate as Date;
    const dateStr = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

    const fd = new FormData();
    fd.append('county', county!);
    fd.append('observedDate', dateStr);
    if (notes) fd.append('notes', notes);
    if (this.photoFile) fd.append('photo', this.photoFile);

    this.submissions.submitMorel(fd).subscribe({
      next: () => {
        this.submitting.set(false);
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.submitting.set(false);
        const msg = err?.error?.error ?? err?.error?.title ?? 'Submission failed. Please try again.';
        this.serverError.set(msg);
      }
    });
  }
}

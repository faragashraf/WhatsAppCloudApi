import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

@Component({
  selector: 'app-contact',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatIconModule, MatInputModule, MatFormFieldModule, MatSnackBarModule],
  template: `
    <section class="min-h-screen bg-gradient-to-b from-slate-50 to-white dark:from-slate-950 dark:to-slate-900 pt-32 pb-24">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div class="text-center mb-16">
          <h1 class="text-4xl sm:text-5xl font-bold text-slate-900 dark:text-white mb-4">
            Get in touch
          </h1>
          <p class="text-lg text-slate-600 dark:text-slate-400 max-w-2xl mx-auto">
            Have questions? We'd love to hear from you. Send us a message and we'll respond as soon as possible.
          </p>
        </div>

        <div class="grid lg:grid-cols-2 gap-16 max-w-5xl mx-auto">
          <!-- Contact Form -->
          <div class="bg-white dark:bg-slate-800/50 rounded-2xl border border-slate-200 dark:border-slate-700/50 p-8 shadow-sm">
            <form (ngSubmit)="onSubmit()" class="space-y-5">
              <mat-form-field appearance="outline" class="w-full">
                <mat-label>Full Name</mat-label>
                <input matInput [(ngModel)]="form.name" name="name" required>
              </mat-form-field>

              <mat-form-field appearance="outline" class="w-full">
                <mat-label>Email Address</mat-label>
                <input matInput type="email" [(ngModel)]="form.email" name="email" required>
              </mat-form-field>

              <mat-form-field appearance="outline" class="w-full">
                <mat-label>Subject</mat-label>
                <input matInput [(ngModel)]="form.subject" name="subject" required>
              </mat-form-field>

              <mat-form-field appearance="outline" class="w-full">
                <mat-label>Message</mat-label>
                <textarea matInput [(ngModel)]="form.message" name="message" rows="5" required></textarea>
              </mat-form-field>

              <button mat-flat-button type="submit" class="!bg-emerald-600 !text-white !rounded-xl !px-8 !py-3 w-full hover:!bg-emerald-700">
                Send Message
                <mat-icon class="ml-2">send</mat-icon>
              </button>
            </form>
          </div>

          <!-- Contact Info -->
          <div class="space-y-8">
            @for (info of contactInfo; track info.title) {
              <div class="flex gap-4">
                <div class="shrink-0 w-12 h-12 rounded-xl bg-emerald-100 dark:bg-emerald-900/40 flex items-center justify-center">
                  <mat-icon class="!text-emerald-600 dark:!text-emerald-400">{{ info.icon }}</mat-icon>
                </div>
                <div>
                  <h4 class="font-semibold text-slate-900 dark:text-white mb-1">{{ info.title }}</h4>
                  <p class="text-sm text-slate-600 dark:text-slate-400">{{ info.description }}</p>
                  @if (info.link) {
                    <a [href]="info.link" target="_blank" class="text-sm text-emerald-600 dark:text-emerald-400 hover:underline mt-1 inline-block no-underline">
                      {{ info.linkText }}
                    </a>
                  }
                </div>
              </div>
            }

            <!-- WhatsApp CTA -->
            <div class="rounded-2xl bg-gradient-to-br from-emerald-600 to-green-700 p-8 text-white">
              <div class="flex items-center gap-3 mb-4">
                <mat-icon class="!text-[28px]">chat</mat-icon>
                <h3 class="text-lg font-bold">Chat with us on WhatsApp</h3>
              </div>
              <p class="text-emerald-100 text-sm mb-4">
                Get instant support from our team via WhatsApp. We're available 24/7.
              </p>
              <a href="https://wa.me/1234567890" target="_blank"
                class="inline-flex items-center gap-2 bg-white text-emerald-700 px-6 py-2.5 rounded-xl font-semibold text-sm hover:bg-emerald-50 transition-colors no-underline">
                <mat-icon class="!text-[18px]">open_in_new</mat-icon>
                Start Chat
              </a>
            </div>
          </div>
        </div>
      </div>
    </section>
  `,
})
export class ContactComponent {
  form = { name: '', email: '', subject: '', message: '' };

  contactInfo = [
    { icon: 'email', title: 'Email', description: 'Our team typically responds within 2 hours.', link: 'mailto:support@wacloud.dev', linkText: 'support&#64;wacloud.dev' },
    { icon: 'schedule', title: 'Working Hours', description: 'Monday — Friday, 9 AM — 6 PM (UTC+2)', link: null, linkText: null },
    { icon: 'location_on', title: 'Location', description: 'Cairo, Egypt — Serving customers worldwide.', link: null, linkText: null },
  ];

  constructor(private snackBar: MatSnackBar) {}

  onSubmit(): void {
    this.snackBar.open('Message sent! We\'ll get back to you soon.', 'Close', { duration: 4000 });
    this.form = { name: '', email: '', subject: '', message: '' };
  }
}

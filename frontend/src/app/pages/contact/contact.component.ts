import { Component, inject, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';

@Component({
  selector: 'app-contact',
  standalone: true,
  imports: [NgClass, FormsModule, ButtonModule, InputTextModule, ToastModule],
  providers: [MessageService],
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
            <p-toast />
            <form (ngSubmit)="onSubmit()" class="space-y-5">
              <div class="flex flex-col gap-1">
                <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Full Name</label>
                <input pInputText [(ngModel)]="form.name" name="name" required class="w-full">
              </div>

              <div class="flex flex-col gap-1">
                <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Email Address</label>
                <input pInputText type="email" [(ngModel)]="form.email" name="email" required class="w-full">
              </div>

              <div class="flex flex-col gap-1">
                <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Subject</label>
                <input pInputText [(ngModel)]="form.subject" name="subject" required class="w-full">
              </div>

              <div class="flex flex-col gap-1">
                <label class="text-sm font-medium text-slate-700 dark:text-slate-300">Message</label>
                <textarea pInputText [(ngModel)]="form.message" name="message" rows="5" required class="w-full"></textarea>
              </div>

              <button pButton type="submit" class="!bg-emerald-600 !text-white !rounded-xl !px-8 !py-3 w-full hover:!bg-emerald-700 !border-0">
                Send Message
                <i class="pi pi-send ml-2"></i>
              </button>
            </form>
          </div>

          <!-- Contact Info -->
          <div class="space-y-8">
            @for (info of contactInfo; track info.title) {
              <div class="flex gap-4">
                <div class="shrink-0 w-12 h-12 rounded-xl bg-emerald-100 dark:bg-emerald-900/40 flex items-center justify-center">
                  <i class="pi text-emerald-600 dark:text-emerald-400" [ngClass]="'pi-' + info.icon"></i>
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
                <i class="pi pi-comments text-[28px]"></i>
                <h3 class="text-lg font-bold">Chat with us on WhatsApp</h3>
              </div>
              <p class="text-emerald-100 text-sm mb-4">
                Get instant support from our team via WhatsApp. We're available 24/7.
              </p>
              <a href="https://wa.me/201110446331" target="_blank"
                class="inline-flex items-center gap-2 bg-white text-emerald-700 px-6 py-2.5 rounded-xl font-semibold text-sm hover:bg-emerald-50 transition-colors no-underline">
                <i class="pi pi-external-link text-[18px]"></i>
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

  private readonly messageService = inject(MessageService);

  contactInfo = [
    { icon: 'envelope', title: 'Email', description: 'Our team typically responds within 2 hours.', link: 'mailto:support@wacloud.dev', linkText: 'support&#64;wacloud.dev' },
    { icon: 'clock', title: 'Working Hours', description: 'Monday — Friday, 9 AM — 6 PM (UTC+2)', link: null, linkText: null },
    { icon: 'map-marker', title: 'Location', description: 'Cairo, Egypt — Serving customers worldwide.', link: null, linkText: null },
  ];

  onSubmit(): void {
    this.messageService.add({ severity: 'success', summary: 'Message sent! We\'ll get back to you soon.', life: 4000 });
    this.form = { name: '', email: '', subject: '', message: '' };
  }
}

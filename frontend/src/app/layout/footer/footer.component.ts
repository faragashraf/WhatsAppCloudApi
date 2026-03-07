import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [RouterLink, MatIconModule],
  template: `
    <footer class="bg-slate-900 text-slate-400 border-t border-slate-800">
      <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-12">
        <div class="grid grid-cols-1 md:grid-cols-4 gap-8">
          <!-- Brand -->
          <div class="md:col-span-1">
            <div class="flex items-center gap-2 mb-4">
              <div class="w-8 h-8 bg-gradient-to-br from-emerald-500 to-green-600 rounded-lg flex items-center justify-center">
                <mat-icon class="text-white !text-[16px]">chat</mat-icon>
              </div>
              <span class="text-lg font-bold text-white">WaCloud</span>
            </div>
            <p class="text-sm leading-relaxed">
              The developer-first WhatsApp Cloud API platform. Build powerful messaging experiences.
            </p>
          </div>

          <!-- Product -->
          <div>
            <h4 class="text-white font-semibold text-sm mb-4 uppercase tracking-wider">Product</h4>
            <ul class="space-y-2 list-none p-0 m-0">
              <li><a routerLink="/pricing" class="text-sm hover:text-emerald-400 transition-colors no-underline text-slate-400">Pricing</a></li>
              <li><a routerLink="/about" class="text-sm hover:text-emerald-400 transition-colors no-underline text-slate-400">About</a></li>
              <li><a class="text-sm hover:text-emerald-400 transition-colors cursor-pointer">Documentation</a></li>
              <li><a class="text-sm hover:text-emerald-400 transition-colors cursor-pointer">API Reference</a></li>
            </ul>
          </div>

          <!-- Company -->
          <div>
            <h4 class="text-white font-semibold text-sm mb-4 uppercase tracking-wider">Company</h4>
            <ul class="space-y-2 list-none p-0 m-0">
              <li><a routerLink="/about" class="text-sm hover:text-emerald-400 transition-colors no-underline text-slate-400">About Us</a></li>
              <li><a routerLink="/contact" class="text-sm hover:text-emerald-400 transition-colors no-underline text-slate-400">Contact</a></li>
              <li><a class="text-sm hover:text-emerald-400 transition-colors cursor-pointer">Privacy Policy</a></li>
              <li><a class="text-sm hover:text-emerald-400 transition-colors cursor-pointer">Terms of Service</a></li>
            </ul>
          </div>

          <!-- Support -->
          <div>
            <h4 class="text-white font-semibold text-sm mb-4 uppercase tracking-wider">Support</h4>
            <ul class="space-y-2 list-none p-0 m-0">
              <li><a class="text-sm hover:text-emerald-400 transition-colors cursor-pointer">Help Center</a></li>
              <li><a class="text-sm hover:text-emerald-400 transition-colors cursor-pointer">Status</a></li>
              <li><a class="text-sm hover:text-emerald-400 transition-colors cursor-pointer">Community</a></li>
            </ul>
          </div>
        </div>

        <div class="border-t border-slate-800 mt-10 pt-8 flex flex-col sm:flex-row justify-between items-center gap-4">
          <p class="text-xs">&copy; {{ currentYear }} WaCloud. All rights reserved.</p>
          <div class="flex gap-4">
            <a class="text-slate-500 hover:text-emerald-400 transition-colors cursor-pointer">
              <mat-icon class="!text-[20px]">language</mat-icon>
            </a>
          </div>
        </div>
      </div>
    </footer>
  `,
})
export class FooterComponent {
  readonly currentYear = new Date().getFullYear();
}

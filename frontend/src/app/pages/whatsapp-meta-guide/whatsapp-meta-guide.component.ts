import { Component, computed, inject } from '@angular/core';
import { LanguageService } from '../../core/services';

@Component({
  selector: 'app-whatsapp-meta-guide',
  standalone: true,
  templateUrl: './whatsapp-meta-guide.component.html',
})
export class WhatsappMetaGuideComponent {
  private readonly langService = inject(LanguageService);
  readonly isArabic = computed(() => this.langService.currentLang() === 'ar');
}

import { Component, computed, inject } from '@angular/core';
import { LanguageService } from '../../core/services/language.service';

type GuideLang = 'ar' | 'en';

type GuideBadge = {
  icon: string;
  label: string;
  value: string;
};

type GuideTimelineItem = {
  stage: string;
  title: string;
  description: string;
  hint: string;
};

type GuideListItem = {
  title: string;
  description: string;
};

type GuideFeatureItem = {
  icon: string;
  title: string;
  description: string;
};

type GuideStepItem = {
  title: string;
  description: string;
};

type GuideContent = {
  hero: {
    eyebrow: string;
    title: string;
    subtitle: string;
  };
  badges: GuideBadge[];
  subscription: {
    title: string;
    subtitle: string;
    timeline: GuideTimelineItem[];
  };
  emailRules: {
    title: string;
    subtitle: string;
    items: GuideListItem[];
  };
  passwordRecovery: {
    title: string;
    subtitle: string;
    steps: GuideStepItem[];
  };
  junkChecklist: {
    title: string;
    subtitle: string;
    checks: string[];
  };
  automation: {
    title: string;
    subtitle: string;
    items: GuideFeatureItem[];
  };
  modules: {
    title: string;
    subtitle: string;
    items: GuideFeatureItem[];
  };
  reliability: {
    title: string;
    subtitle: string;
    items: GuideFeatureItem[];
  };
  launchPlan: {
    title: string;
    subtitle: string;
    steps: GuideStepItem[];
  };
};

@Component({
  selector: 'app-system-guide',
  standalone: true,
  templateUrl: './system-guide.component.html',
  styleUrl: './system-guide.component.scss',
})
export class SystemGuideComponent {
  readonly langService = inject(LanguageService);

  readonly isArabic = computed(() => this.langService.currentLang() === 'ar');
  readonly content = computed(() => this.guide[this.langService.currentLang() as GuideLang] ?? this.guide.ar);

  private static readonly latinPhrasePattern =
    /([A-Za-z][A-Za-z0-9@._+/#&:=-]*(?:\s+[A-Za-z0-9@._+/#&:=-]+)*)/g;

  formatText(value: string): string {
    if (!this.isArabic()) {
      return value;
    }

    return value.replace(SystemGuideComponent.latinPhrasePattern, '<bdi dir="ltr">$1</bdi>');
  }

  private readonly guide: Record<GuideLang, GuideContent> = {
    ar: {
      hero: {
        eyebrow: 'System Playbook',
        title: 'دليل التشغيل الكامل للنظام',
        subtitle: 'صفحة واحدة تجمع الاشتراكات، الإيميلات، استعادة كلمة المرور، الـ CRM، الأوتوماشين، الفلوهات، وأفضل ممارسات التشغيل اليومي لفريقك.',
      },
      badges: [
        { icon: 'pi pi-credit-card', label: 'قواعد الاشتراك', value: 'اشتراك + تجربة + صلاحيات إرسال' },
        { icon: 'pi pi-envelope', label: 'تنبيهات الإيميل', value: 'Queue + Retry + قواعد تنبيه ذكية' },
        { icon: 'pi pi-lock', label: 'استعادة كلمة المرور', value: 'OTP من 6 أرقام لمدة 10 دقائق' },
        { icon: 'pi pi-sitemap', label: 'Automation & Flows', value: 'Rules + Builder + Meta Flow + Webhooks' },
      ],
      subscription: {
        title: 'دورة حياة اشتراك الشركة',
        subtitle: 'السلوك الحالي مبني على منطق السيرفر: صلاحية الاشتراك هي المفتاح الأساسي للإرسال والعمليات.',
        timeline: [
          {
            stage: '01',
            title: 'اشتراك فعّال',
            description: 'طالما الشركة عندها Trial ساري أو Subscription Active، النظام يسمح بإرسال الرسائل حسب حدود الباقة.',
            hint: 'تطبيق حدود شهرية للرسائل + حد أقصى لحسابات WhatsApp حسب الخطة.',
          },
          {
            stage: '02',
            title: 'تنبيه قرب الانتهاء',
            description: 'من Email Center يمكنك إنشاء Rule ب Trigger = COMPANY_SUBSCRIPTION_EXPIRY وتحديد Lead Time من 0 إلى 365 يوم.',
            hint: 'المستلمين: Company Email أو Company Admins أو قائمة Custom.',
          },
          {
            stage: '03',
            title: 'إرسال التذكير تلقائيًا',
            description: 'EmailQueueProcessor يفحص الشركات ويولد رسالة تذكير عند الدخول داخل المدة المحددة قبل تاريخ الانتهاء.',
            hint: 'فيه DeduplicationKey يمنع تكرار نفس التذكير لنفس الشركة ونفس تاريخ الانتهاء.',
          },
          {
            stage: '04',
            title: 'بعد الانتهاء',
            description: 'عند انتهاء الاشتراك تُمنع عمليات الإرسال وتظهر رسائل انتهاء اشتراك أو تخطي حدود الخطة.',
            hint: 'التجديد يعيد الصلاحية مباشرة حسب حالة الاشتراك الجديدة.',
          },
        ],
      },
      emailRules: {
        title: 'متى تُرسل إيميلات قرب انتهاء الاشتراك؟',
        subtitle: 'هذا هو السلوك الفعلي في الكود الحالي:',
        items: [
          {
            title: 'Trigger معتمد',
            description: 'القواعد الآلية حاليًا تدعم Trigger واحد: COMPANY_SUBSCRIPTION_EXPIRY.',
          },
          {
            title: 'نافذة التنبيه',
            description: 'الإرسال يتم عندما يكون المتبقي حتى الانتهاء بين 0 و LeadTimeDays.',
          },
          {
            title: 'تشغيل الخلفية',
            description: 'Email Queue Worker يعالج دفعات حتى 20 عنصر، ولو لا يوجد عمل ينتظر تقريبًا 10 ثواني قبل الدورة التالية.',
          },
          {
            title: 'إعادة المحاولة',
            description: 'رسائل الإيميل تعمل Retry تلقائي حتى 5 محاولات قبل التحويل لحالة Failed.',
          },
          {
            title: 'مهم جدًا',
            description: 'لو العميل قال إن الإيميل لم يصل: يجب فحص Spam/Junk/Promotions بالإضافة إلى Inbox.',
          },
        ],
      },
      passwordRecovery: {
        title: 'استعادة كلمة المرور عبر الإيميل',
        subtitle: 'المسار مدعوم بالكامل عبر Forgot Password + OTP Verification + Reset.',
        steps: [
          {
            title: 'طلب الاسترجاع',
            description: 'المستخدم يرسل بريده في forgot-password. النظام لا يكشف إذا الإيميل موجود أم لا (سلوك أمني مقصود).',
          },
          {
            title: 'توليد OTP',
            description: 'يتم إنشاء OTP عشوائي من 6 أرقام وصلاحيته 10 دقائق.',
          },
          {
            title: 'إرسال البريد',
            description: 'الرسالة ترسل عبر SMTP (من Email Account أو إعدادات Email العامة) مع تسجيل Audit في Email Queue.',
          },
          {
            title: 'التحقق وتغيير كلمة المرور',
            description: 'بعد Verify OTP يتم السماح بتعيين كلمة مرور جديدة قوية، ويتم إلغاء OTP السابق تلقائيًا.',
          },
          {
            title: 'لو الرسالة لم تظهر',
            description: 'المستخدم لازم يفحص Junk/Spam/Promotions قبل طلب كود جديد.',
          },
        ],
      },
      junkChecklist: {
        title: 'Junk Mail Checklist',
        subtitle: 'لضمان وصول رسائل الاشتراك وكود الاسترجاع بشكل ثابت:',
        checks: [
          'أضف noreply@botglobalservice.com إلى Safe Senders.',
          'افحص Inbox ثم Spam/Junk ثم Promotions قبل اعتبار الرسالة مفقودة.',
          'تحقق من سياسات الشركة الداخلية التي قد تحجب الرسائل الآلية.',
          'استخدم دومين بريد صالح ومراقب للردود والإشعارات.',
        ],
      },
      automation: {
        title: 'Automation + CRM + Flow Engine',
        subtitle: 'النظام لا يعتمد فقط على ردود تلقائية بسيطة؛ بل يحتوي محرك تشغيل محادثات كامل.',
        items: [
          {
            icon: 'pi pi-bolt',
            title: 'Automation Rules',
            description: 'Triggers: any / keyword / contains / exact / regex مع رد Text أو Template.',
          },
          {
            icon: 'pi pi-share-alt',
            title: 'Conversation Flow Builder',
            description: 'Nodes تشمل: start, message, menu, capture_text, form, meta_flow, assign_agent, external_link, end.',
          },
          {
            icon: 'pi pi-database',
            title: 'Form Submissions',
            description: 'تجميع قيم الفورم و Meta Flow في سجل موحد، مع فلترة وتصدير Excel.',
          },
          {
            icon: 'pi pi-link',
            title: 'Custom Webhook Bridge',
            description: 'Template variables + filters + strict validation + أوضاع خارج نافذة 24 ساعة (block/allow_text/template).',
          },
          {
            icon: 'pi pi-verified',
            title: 'Assignment & Routing',
            description: 'توزيع المحادثات يدوي/آلي، التقاط المحادثة (Pick)، وتتبع مسؤول كل عميل.',
          },
          {
            icon: 'pi pi-box',
            title: 'Meta Flows Workspace',
            description: 'إدارة Meta Flow assets وربطها مباشرة داخل مسارات المحادثة.',
          },
        ],
      },
      modules: {
        title: 'وحدات النظام في واجهة واحدة',
        subtitle: 'الصفحة توضح للمستخدم النهائي إن المنصة ليست فقط إرسال رسائل، بل نظام تشغيل كامل.',
        items: [
          { icon: 'pi pi-comments', title: 'Inbox', description: 'إدارة المحادثات الحية مع حالات القراءة والتعيين.' },
          { icon: 'pi pi-users', title: 'Contacts', description: 'إدارة العملاء، التاجات، والاستيراد.' },
          { icon: 'pi pi-megaphone', title: 'Campaigns', description: 'حملات مجمعة بحالات Draft/Scheduled/Running/Completed.' },
          { icon: 'pi pi-file-edit', title: 'Templates', description: 'إدارة قوالب WhatsApp المعتمدة.' },
          { icon: 'pi pi-send', title: 'Send Message', description: 'إرسال مباشر نص/ميديا/Template من لوحة التحكم.' },
          { icon: 'pi pi-envelope', title: 'Email Center', description: 'حسابات SMTP + قواعد إشعارات + Queue Dashboard.' },
          { icon: 'pi pi-bell', title: 'Notifications', description: 'تنبيهات النظام والويبهوك والإنبوكس.' },
          { icon: 'pi pi-heart', title: 'Health & Logs', description: 'حالة المنصة وسجلات الرسائل للأثر التشغيلي.' },
          { icon: 'pi pi-user-edit', title: 'Users & Permissions', description: 'إدارة الفريق والصلاحيات حسب الدور.' },
          { icon: 'pi pi-wallet', title: 'Billing', description: 'عرض الاشتراك الحالي ومتابعة الباقة.' },
        ],
      },
      reliability: {
        title: 'اعتمادية وتشغيل مستقر',
        subtitle: 'المنصة مبنية على Queue-first architecture لضمان الاستمرارية تحت الضغط.',
        items: [
          {
            icon: 'pi pi-sync',
            title: 'Message Queue + Retry',
            description: 'الرسائل الخارجة تمر على queue مع إعادة محاولة تلقائية حتى 3 مرات.',
          },
          {
            icon: 'pi pi-at',
            title: 'Email Queue + Retry',
            description: 'الإيميلات المجدولة/الآلية تدعم Retry حتى 5 مرات مع تتبع الأخطاء.',
          },
          {
            icon: 'pi pi-shield',
            title: 'Window Policy Enforcement',
            description: 'رسائل inbox النصية تخضع لنافذة دعم 24 ساعة، مع fallback template في سيناريوهات webhook.',
          },
          {
            icon: 'pi pi-server',
            title: 'Background Workers',
            description: 'Workers مستقلة لمعالجة الرسائل والإيميل والويبهوك بشكل غير متزامن.',
          },
        ],
      },
      launchPlan: {
        title: 'خطة تشغيل الفريق على الصفحة',
        subtitle: 'اعرض هذه النقاط لأي عميل أو موظف جديد لبدء تشغيل صحيح من اليوم الأول.',
        steps: [
          { title: 'ضبط البريد', description: 'فعّل SMTP وتأكد من From Address وبيانات الاعتماد.' },
          { title: 'إنشاء Rule اشتراك', description: 'أضف Subscription Expiry Rule وحدد Lead Time والمستلمين.' },
          { title: 'تجربة استرجاع كلمة المرور', description: 'نفذ سيناريو OTP كامل وتأكد من وصول البريد.' },
          { title: 'تفعيل Flow أساسي', description: 'أنشئ Flow منشور للترحيب، التصنيف، والتحويل للوكيل.' },
          { title: 'ربط Webhook مخصص', description: 'استخدم template variables للتكامل مع CRM أو ERP.' },
          { title: 'تدريب الفريق', description: 'أكد على فحص Junk Mail وعلى سياسات نافذة 24 ساعة.' },
        ],
      },
    },
    en: {
      hero: {
        eyebrow: 'System Playbook',
        title: 'Complete Platform Guide',
        subtitle: 'One smart page that documents subscriptions, email behavior, password recovery, CRM, automation, flows, and day-to-day operating best practices.',
      },
      badges: [
        { icon: 'pi pi-credit-card', label: 'Subscription Logic', value: 'Plan + Trial + Sending Eligibility' },
        { icon: 'pi pi-envelope', label: 'Email Notifications', value: 'Queue + Retry + Rule-based Alerts' },
        { icon: 'pi pi-lock', label: 'Password Recovery', value: '6-digit OTP valid for 10 minutes' },
        { icon: 'pi pi-sitemap', label: 'Automation & Flows', value: 'Rules + Builder + Meta Flow + Webhooks' },
      ],
      subscription: {
        title: 'Company Subscription Lifecycle',
        subtitle: 'This reflects the current backend behavior: subscription validity controls what companies can do.',
        timeline: [
          {
            stage: '01',
            title: 'Active Subscription',
            description: 'When trial or paid subscription is valid, messaging is allowed based on the plan limits.',
            hint: 'Monthly message caps and WhatsApp account limits are enforced per plan.',
          },
          {
            stage: '02',
            title: 'Expiry Reminder Window',
            description: 'From Email Center, admins can create a rule with Trigger = COMPANY_SUBSCRIPTION_EXPIRY and set Lead Time from 0 to 365 days.',
            hint: 'Recipients can be Company Email, Company Admins, or a Custom list.',
          },
          {
            stage: '03',
            title: 'Automated Reminder Dispatch',
            description: 'EmailQueueProcessor generates reminder emails once the company enters the configured lead-time window.',
            hint: 'A deduplication key prevents duplicate reminder generation for the same company and expiry date.',
          },
          {
            stage: '04',
            title: 'Post-Expiry Behavior',
            description: 'When subscription expires, sending operations are blocked and plan-limit errors are returned where applicable.',
            hint: 'Renewal immediately restores eligibility according to the new subscription state.',
          },
        ],
      },
      emailRules: {
        title: 'When Are Subscription Reminder Emails Sent?',
        subtitle: 'Current behavior implemented in code:',
        items: [
          {
            title: 'Supported Trigger',
            description: 'Automated notification rules currently support one trigger: COMPANY_SUBSCRIPTION_EXPIRY.',
          },
          {
            title: 'Lead-Time Window',
            description: 'Emails are generated when days-until-expiry is between 0 and LeadTimeDays.',
          },
          {
            title: 'Background Processing',
            description: 'Email Queue Worker processes batches up to 20 items; when idle, it waits about 10 seconds before the next cycle.',
          },
          {
            title: 'Retries',
            description: 'Email queue items retry automatically up to 5 attempts before ending as Failed.',
          },
          {
            title: 'Important',
            description: 'If a customer says the email did not arrive, always check Spam/Junk/Promotions in addition to Inbox.',
          },
        ],
      },
      passwordRecovery: {
        title: 'Password Recovery by Email',
        subtitle: 'Fully implemented through Forgot Password + OTP Verification + Reset Password.',
        steps: [
          {
            title: 'Start Request',
            description: 'User submits email in forgot-password. API does not reveal whether the email exists (intentional security behavior).',
          },
          {
            title: 'OTP Generation',
            description: 'A random 6-digit OTP is generated and valid for 10 minutes.',
          },
          {
            title: 'Email Delivery',
            description: 'Email is sent over SMTP (from Email Account or global Email settings) with queue-audit tracking.',
          },
          {
            title: 'Verify and Reset',
            description: 'After OTP verification, user can set a new strong password and the old OTP is invalidated.',
          },
          {
            title: 'If Email Is Missing',
            description: 'User should check Junk/Spam/Promotions before requesting a new OTP.',
          },
        ],
      },
      junkChecklist: {
        title: 'Junk Mail Checklist',
        subtitle: 'To keep subscription and OTP emails reliably deliverable:',
        checks: [
          'Add noreply@botglobalservice.com to Safe Senders.',
          'Check Inbox, then Spam/Junk, then Promotions before reporting missing email.',
          'Verify corporate mail gateways are not blocking automated transactional emails.',
          'Use a valid monitored mailbox for company contact and admin notifications.',
        ],
      },
      automation: {
        title: 'Automation + CRM + Flow Engine',
        subtitle: 'The platform is not limited to simple auto-replies; it includes a full conversation runtime engine.',
        items: [
          {
            icon: 'pi pi-bolt',
            title: 'Automation Rules',
            description: 'Triggers: any / keyword / contains / exact / regex with text or template responses.',
          },
          {
            icon: 'pi pi-share-alt',
            title: 'Conversation Flow Builder',
            description: 'Nodes include: start, message, menu, capture_text, form, meta_flow, assign_agent, external_link, end.',
          },
          {
            icon: 'pi pi-database',
            title: 'Form Submissions',
            description: 'Form node and Meta Flow payloads are captured in one stream with filters and Excel export.',
          },
          {
            icon: 'pi pi-link',
            title: 'Custom Webhook Bridge',
            description: 'Template variables + filters + strict validation + outside-window modes (block/allow_text/template).',
          },
          {
            icon: 'pi pi-verified',
            title: 'Assignment & Routing',
            description: 'Manual/auto assignment, conversation pick-up, and ownership tracking for contacts.',
          },
          {
            icon: 'pi pi-box',
            title: 'Meta Flows Workspace',
            description: 'Manage Meta Flow assets and bind them directly to conversation nodes.',
          },
        ],
      },
      modules: {
        title: 'Platform Modules in One View',
        subtitle: 'This page communicates that the platform is a full operating system, not just message sending.',
        items: [
          { icon: 'pi pi-comments', title: 'Inbox', description: 'Live conversation handling with read state and assignment flows.' },
          { icon: 'pi pi-users', title: 'Contacts', description: 'Customer profiles, tags, and bulk import support.' },
          { icon: 'pi pi-megaphone', title: 'Campaigns', description: 'Bulk campaign lifecycle: Draft, Scheduled, Running, Completed.' },
          { icon: 'pi pi-file-edit', title: 'Templates', description: 'Manage approved WhatsApp templates.' },
          { icon: 'pi pi-send', title: 'Send Message', description: 'Direct text/media/template sending from dashboard.' },
          { icon: 'pi pi-envelope', title: 'Email Center', description: 'SMTP accounts, notification rules, and queue dashboard.' },
          { icon: 'pi pi-bell', title: 'Notifications', description: 'System, webhook, and inbox notifications.' },
          { icon: 'pi pi-heart', title: 'Health & Logs', description: 'Operational health and delivery diagnostics.' },
          { icon: 'pi pi-user-edit', title: 'Users & Permissions', description: 'Team management and role-based access.' },
          { icon: 'pi pi-wallet', title: 'Billing', description: 'Current plan visibility and subscription control.' },
        ],
      },
      reliability: {
        title: 'Reliability by Design',
        subtitle: 'Queue-first architecture keeps operations stable under load.',
        items: [
          {
            icon: 'pi pi-sync',
            title: 'Message Queue + Retry',
            description: 'Outbound messages pass through queue processing with automatic retries up to 3 attempts.',
          },
          {
            icon: 'pi pi-at',
            title: 'Email Queue + Retry',
            description: 'Scheduled and automated emails retry up to 5 attempts with error tracking.',
          },
          {
            icon: 'pi pi-shield',
            title: '24-Hour Policy Enforcement',
            description: 'Inbox text sending follows the 24-hour support window, with webhook fallback options.',
          },
          {
            icon: 'pi pi-server',
            title: 'Background Workers',
            description: 'Independent workers process message, email, and webhook pipelines asynchronously.',
          },
        ],
      },
      launchPlan: {
        title: 'Team Rollout Plan',
        subtitle: 'Use these steps to onboard any customer or internal team quickly.',
        steps: [
          { title: 'Configure Email', description: 'Enable SMTP and validate From Address and credentials.' },
          { title: 'Create Expiry Rule', description: 'Add subscription expiry rule with lead-time and recipients.' },
          { title: 'Run Password Recovery Test', description: 'Execute full OTP flow and verify inbox + junk handling.' },
          { title: 'Publish a Core Flow', description: 'Deploy a welcome/classification/handoff flow as baseline.' },
          { title: 'Connect Custom Webhook', description: 'Use template variables to integrate CRM or ERP events.' },
          { title: 'Train Operations Team', description: 'Reinforce Junk mail checks and 24-hour messaging policy.' },
        ],
      },
    },
  };
}

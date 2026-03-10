#!/usr/bin/env python3
"""Generate a modern RTL Arabic WhatsApp Business & Meta Integration PowerPoint."""

from pptx import Presentation
from pptx.util import Inches, Pt, Emu
from pptx.dml.color import RGBColor
from pptx.enum.text import PP_ALIGN, MSO_ANCHOR
from pptx.enum.shapes import MSO_SHAPE
import os

# ─── Color Palette ───
WA_GREEN      = RGBColor(0x25, 0xD3, 0x66)
WA_DARK_GREEN = RGBColor(0x07, 0x5E, 0x54)
WA_TEAL       = RGBColor(0x12, 0x8C, 0x7E)
DARK_BG       = RGBColor(0x0B, 0x14, 0x1E)
CARD_BG       = RGBColor(0x1A, 0x2A, 0x36)
WHITE         = RGBColor(0xFF, 0xFF, 0xFF)
LIGHT_GRAY    = RGBColor(0xB0, 0xBE, 0xC5)
GOLD          = RGBColor(0xFF, 0xD5, 0x4F)
RED_ACCENT    = RGBColor(0xFF, 0x53, 0x52)
BLUE_ACCENT   = RGBColor(0x00, 0xA8, 0xFF)
ORANGE_ACCENT = RGBColor(0xFF, 0x98, 0x00)

prs = Presentation()
prs.slide_width  = Inches(13.333)
prs.slide_height = Inches(7.5)
SLIDE_W = prs.slide_width
SLIDE_H = prs.slide_height

# ─── RTL Helper ────────────────────────────────────────────

def set_rtl(paragraph):
    """Set RTL direction on a paragraph via XML."""
    pPr = paragraph._p.get_or_add_pPr()
    pPr.set('rtl', '1')

def set_txbody_rtl(text_frame):
    """Set RTL on the text body (bodyPr) element."""
    bodyPr = text_frame._txBody.find(
        '{http://schemas.openxmlformats.org/drawingml/2006/main}bodyPr')
    if bodyPr is not None:
        bodyPr.set('rtlCol', '1')

# ─── Drawing Helpers ───────────────────────────────────────

def add_bg(slide, color=DARK_BG):
    fill = slide.background.fill
    fill.solid()
    fill.fore_color.rgb = color

def add_rect(slide, left, top, width, height, fill_color, border_color=None):
    shape = slide.shapes.add_shape(
        MSO_SHAPE.ROUNDED_RECTANGLE, left, top, width, height)
    shape.fill.solid()
    shape.fill.fore_color.rgb = fill_color
    if border_color:
        shape.line.color.rgb = border_color
        shape.line.width = Pt(1.5)
    else:
        shape.line.fill.background()
    return shape

def add_text_box(slide, left, top, width, height, text, font_size=18,
                 color=WHITE, bold=False, alignment=PP_ALIGN.RIGHT,
                 font_name="Segoe UI"):
    txBox = slide.shapes.add_textbox(left, top, width, height)
    tf = txBox.text_frame
    tf.word_wrap = True
    set_txbody_rtl(tf)
    p = tf.paragraphs[0]
    p.text = text
    p.font.size = Pt(font_size)
    p.font.color.rgb = color
    p.font.bold = bold
    p.font.name = font_name
    p.alignment = alignment
    set_rtl(p)
    return txBox

def add_green_bar(slide, top=Inches(0), height=Inches(0.06)):
    shape = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, 0, top, SLIDE_W, height)
    shape.fill.solid()
    shape.fill.fore_color.rgb = WA_GREEN
    shape.line.fill.background()

def add_slide_number(slide, num, total):
    add_text_box(slide, Inches(11), Inches(7.0), Inches(2), Inches(0.4),
                 f"{num} / {total}", 11, LIGHT_GRAY, alignment=PP_ALIGN.LEFT)

def add_section_header(slide, title, subtitle=""):
    add_green_bar(slide)
    add_text_box(slide, Inches(0.5), Inches(0.3), Inches(12.3), Inches(0.8),
                 title, 32, WA_GREEN, bold=True, alignment=PP_ALIGN.RIGHT)
    if subtitle:
        add_text_box(slide, Inches(0.5), Inches(1.0), Inches(12.3), Inches(0.5),
                     subtitle, 18, LIGHT_GRAY, alignment=PP_ALIGN.RIGHT)

def card(slide, left, top, w, h, title, bullets,
         title_color=WA_GREEN, icon=""):
    add_rect(slide, left, top, w, h, CARD_BG, WA_TEAL)
    title_text = f"{icon}  {title}" if icon else title
    add_text_box(slide, left + Inches(0.15), top + Inches(0.12),
                 w - Inches(0.3), Inches(0.5),
                 title_text, 18, title_color, bold=True,
                 alignment=PP_ALIGN.RIGHT)
    y = Inches(0.65)
    for b in bullets:
        add_text_box(slide, left + Inches(0.2), top + y,
                     w - Inches(0.4), Inches(0.35),
                     f"•  {b}", 13, LIGHT_GRAY, alignment=PP_ALIGN.RIGHT)
        y += Inches(0.32)


def rtl_x(cols, col_idx, card_w, gap, margin_right):
    """Return x for a card in an RTL grid.  col 0 = rightmost."""
    return (Inches(13.333) - margin_right
            - (col_idx + 1) * card_w - col_idx * gap)


TOTAL_SLIDES = 12

# ═══════════════════════════════════════════════════════════
# SLIDE 1 – Title
# ═══════════════════════════════════════════════════════════
slide = prs.slides.add_slide(prs.slide_layouts[6])
add_bg(slide)
add_green_bar(slide, Inches(0), Inches(0.1))
add_text_box(slide, Inches(0.5), Inches(1.8), Inches(12.3), Inches(1.2),
             "📱  واتساب بيزنس – التكامل مع Meta",
             44, WHITE, bold=True, alignment=PP_ALIGN.CENTER)
add_text_box(slide, Inches(0.5), Inches(3.0), Inches(12.3), Inches(0.8),
             "دليل شامل للمميزات، التسعير، والخطوات اللازمة",
             24, WA_GREEN, alignment=PP_ALIGN.CENTER)
add_text_box(slide, Inches(0.5), Inches(3.8), Inches(12.3), Inches(0.6),
             "WhatsApp Cloud API  ·  Meta Business Platform",
             20, LIGHT_GRAY, alignment=PP_ALIGN.CENTER)
add_green_bar(slide, Inches(7.3), Inches(0.06))
add_slide_number(slide, 1, TOTAL_SLIDES)

# ═══════════════════════════════════════════════════════════
# SLIDE 2 – What is WhatsApp Business
# ═══════════════════════════════════════════════════════════
slide = prs.slides.add_slide(prs.slide_layouts[6])
add_bg(slide)
add_section_header(slide,
    "ما هو واتساب بيزنس؟",
    "حل من Meta يتيح للشركات التواصل مع عملائها باحترافية عبر واتساب")

cw = Inches(3.8); ch = Inches(3.5); gap = Inches(0.35); mr = Inches(0.5)
cy = Inches(2.0)

card(slide, rtl_x(3, 0, cw, gap, mr), cy, cw, ch,
     "WhatsApp Business App", [
         "تطبيق مجاني للهاتف",
         "مناسب للأعمال الصغيرة (1-5 موظفين)",
         "رسائل ترحيب بسيطة فقط",
         "بدون أتمتة أو تكامل",
         "جهاز واحد إلى 4 أجهزة",
     ], LIGHT_GRAY, "📱")

card(slide, rtl_x(3, 1, cw, gap, mr), cy, cw, ch,
     "WhatsApp Business Platform", [
         "واجهة برمجة تطبيقات (API)",
         "للشركات المتوسطة والكبيرة",
         "أتمتة كاملة + Chatbots",
         "تكامل مع CRM, ERP",
         "يحتاج استضافة خاصة (On-Premise)",
     ], ORANGE_ACCENT, "🏢")

card(slide, rtl_x(3, 2, cw, gap, mr), cy, cw, ch,
     "WhatsApp Cloud API  ✅", [
         "API مستضاف على سيرفرات Meta",
         "إعداد سريع بدون سيرفر خاص",
         "تحديثات تلقائية من Meta",
         "تكلفة أقل + موثوقية عالية",
         "الخيار الأفضل للشركات",
     ], WA_GREEN, "☁️")

add_text_box(slide, Inches(0.5), Inches(5.8), Inches(12.3), Inches(0.5),
             "☁️  Cloud API هو الخيار الأمثل – مستضاف على Meta، تكلفة أقل، وإعداد أسرع",
             16, GOLD, bold=True, alignment=PP_ALIGN.CENTER)
add_slide_number(slide, 2, TOTAL_SLIDES)

# ═══════════════════════════════════════════════════════════
# SLIDE 3 – Mandatory Requirements
# ═══════════════════════════════════════════════════════════
slide = prs.slides.add_slide(prs.slide_layouts[6])
add_bg(slide)
add_section_header(slide,
    "⛔  المتطلبات الإلزامية",
    "جميع المتطلبات التالية إلزامية – بدونها لن تتمكن من استخدام WhatsApp Cloud API")

reqs = [
    ("🔴", "حساب Meta Business", "إلزامي",
     "يجب إنشاؤه وتفعيله قبل أي خطوة"),
    ("🔴", "التحقق من النشاط التجاري", "إلزامي",
     "بدونه محدود بـ 250 محادثة فقط"),
    ("🔴", "تطبيق Meta Developer", "إلزامي",
     "البوابة الوحيدة للوصول لـ API"),
    ("🔴", "رقم هاتف مخصص", "إلزامي",
     "سيُفصل نهائياً من التطبيق العادي"),
    ("🟡", "نطاق إنترنت (Domain)", "مطلوب بشدة",
     "ضروري للتحقق من النشاط التجاري"),
    ("🔴", "شركة متخصصة لإدارة الحساب", "إلزامي",
     "Meta توفر API فقط – بدون واجهة إدارة"),
]

y = Inches(1.9)
for i, (icon, name, status, desc) in enumerate(reqs):
    row_color = CARD_BG if i % 2 == 0 else RGBColor(0x15, 0x23, 0x2E)
    add_rect(slide, Inches(0.5), y, Inches(12.2), Inches(0.65), row_color)
    # RTL row:  Icon → Name → Status → Description
    add_text_box(slide, Inches(11.5), y + Inches(0.12), Inches(1.0), Inches(0.4),
                 icon, 18, WHITE, alignment=PP_ALIGN.CENTER)
    add_text_box(slide, Inches(7.8), y + Inches(0.12), Inches(3.5), Inches(0.4),
                 name, 16, WHITE, bold=True, alignment=PP_ALIGN.RIGHT)
    sc = RED_ACCENT if "إلزامي" in status else ORANGE_ACCENT
    add_text_box(slide, Inches(5.5), y + Inches(0.12), Inches(2.1), Inches(0.4),
                 status, 14, sc, bold=True, alignment=PP_ALIGN.CENTER)
    add_text_box(slide, Inches(0.6), y + Inches(0.12), Inches(4.8), Inches(0.4),
                 desc, 13, LIGHT_GRAY, alignment=PP_ALIGN.RIGHT)
    y += Inches(0.72)

add_text_box(slide, Inches(0.5), Inches(6.5), Inches(12.2), Inches(0.5),
             "⚠️  بدون شركة متخصصة = لا واجهة، لا إرسال، لا استقبال، لا إدارة للرسائل!",
             16, RED_ACCENT, bold=True, alignment=PP_ALIGN.CENTER)
add_slide_number(slide, 3, TOTAL_SLIDES)

# ═══════════════════════════════════════════════════════════
# SLIDE 4 – Why Specialized Company
# ═══════════════════════════════════════════════════════════
slide = prs.slides.add_slide(prs.slide_layouts[6])
add_bg(slide)
add_section_header(slide,
    "🏢  لماذا تحتاج شركة متخصصة؟",
    "Meta توفر فقط الـ API (واجهة برمجية خام) – لا توفر أي واجهة أو تطبيق لإدارة الرسائل!")

tasks = [
    ("⚙️", "إنشاء وتكوين الحساب",
     "إعداد تقني معقد يتضمن\nMeta Business + Developer App + Webhooks"),
    ("🖥️", "بناء منصة الإدارة",
     "واجهة كاملة لعرض وإرسال\nواستقبال وإدارة الرسائل"),
    ("🌐", "ربط الـ Webhook",
     "سيرفر HTTPS ثابت يعمل 24/7\nلاستقبال الرسائل فورياً"),
    ("📋", "إدارة القوالب",
     "إنشاء ومراجعة واعتماد قوالب الرسائل\nبخبرة بسياسات Meta"),
    ("🤖", "الأتمتة و Chatbots",
     "بناء سيناريوهات تفاعلية وربطها\nبقواعد البيانات"),
    ("🔒", "الأمان والصيانة",
     "حماية البيانات + متابعة تحديثات\nAPI المستمرة من Meta"),
]

cw2 = Inches(3.85); ch2 = Inches(2.2); gap2 = Inches(0.35)
for i, (icon, title, desc) in enumerate(tasks):
    col = i % 3; row = i // 3
    x = rtl_x(3, col, cw2, gap2, Inches(0.5))
    y = Inches(1.9) + row * (ch2 + Inches(0.3))
    add_rect(slide, x, y, cw2, ch2, CARD_BG, WA_TEAL)
    add_text_box(slide, x + Inches(0.15), y + Inches(0.15),
                 cw2 - Inches(0.3), Inches(0.5),
                 f"{icon}  {title}", 17, WA_GREEN, bold=True,
                 alignment=PP_ALIGN.RIGHT)
    add_text_box(slide, x + Inches(0.15), y + Inches(0.7),
                 cw2 - Inches(0.3), Inches(1.3),
                 desc, 14, LIGHT_GRAY, alignment=PP_ALIGN.RIGHT)

add_text_box(slide, Inches(0.5), Inches(6.6), Inches(12.3), Inches(0.5),
             "💡 الخلاصة: أنت بحاجة لشركة تقنية متخصصة تتولى إنشاء الحساب "
             "وبناء المنصة وإدارة التكامل بالكامل",
             15, GOLD, bold=True, alignment=PP_ALIGN.CENTER)
add_slide_number(slide, 4, TOTAL_SLIDES)

# ═══════════════════════════════════════════════════════════
# SLIDE 5 – Integration Steps (RTL flow ← )
# ═══════════════════════════════════════════════════════════
slide = prs.slides.add_slide(prs.slide_layouts[6])
add_bg(slide)
add_section_header(slide,
    "خطوات التكامل مع Meta",
    "8 خطوات مبسطة للبدء في استخدام WhatsApp Cloud API")

steps = [
    ("1", "إنشاء حساب\nMeta Business", "🏢"),
    ("2", "التحقق من\nالنشاط التجاري", "✅"),
    ("3", "إنشاء تطبيق\nMeta Developer", "👨‍💻"),
    ("4", "إعداد رقم\nواتساب بيزنس", "📱"),
    ("5", "الحصول على\nAccess Token", "🔑"),
    ("6", "إعداد\nWebhook", "🔗"),
    ("7", "تكوين\nالمنصة", "⚙️"),
    ("8", "اختبار\nالتكامل", "🚀"),
]

box_w = Inches(1.35); box_h = Inches(1.8)
for i, (num, label, icon) in enumerate(steps):
    x = rtl_x(8, i, box_w, Inches(0.18), Inches(0.4))
    y = Inches(2.2)
    clr = WA_GREEN if i < 7 else GOLD
    add_rect(slide, x, y, box_w, box_h, CARD_BG, clr)
    add_text_box(slide, x, y + Inches(0.1), box_w, Inches(0.5),
                 icon, 28, WHITE, alignment=PP_ALIGN.CENTER)
    add_text_box(slide, x, y + Inches(0.55), box_w, Inches(0.35),
                 num, 20, clr, bold=True, alignment=PP_ALIGN.CENTER)
    add_text_box(slide, x + Inches(0.05), y + Inches(0.95),
                 box_w - Inches(0.1), Inches(0.8),
                 label, 12, LIGHT_GRAY, alignment=PP_ALIGN.CENTER)
    if i < 7:
        add_text_box(slide, x - Inches(0.25), y + Inches(0.7),
                     Inches(0.3), Inches(0.4),
                     "←", 18, WA_GREEN, alignment=PP_ALIGN.CENTER)

notes = [
    "🔴  الخطوات 1-4 إلزامية ولا يمكن تجاوز أي منها",
    "🔑  الخطوة 5: يجب إنشاء Permanent Token للإنتاج (المؤقت صالح 24 ساعة فقط)",
    "🌐  الخطوة 6: تحتاج سيرفر بعنوان HTTPS ثابت يعمل 24/7",
    "🏢  جميع هذه الخطوات تتطلب شركة متخصصة للتنفيذ والإدارة",
]
y = Inches(4.5)
for note in notes:
    add_text_box(slide, Inches(0.5), y, Inches(12.3), Inches(0.45),
                 note, 14, LIGHT_GRAY, alignment=PP_ALIGN.RIGHT)
    y += Inches(0.45)
add_slide_number(slide, 5, TOTAL_SLIDES)

# ═══════════════════════════════════════════════════════════
# SLIDE 6 – Pricing (RTL card order)
# ═══════════════════════════════════════════════════════════
slide = prs.slides.add_slide(prs.slide_layouts[6])
add_bg(slide)
add_section_header(slide,
    "💰  نموذج التسعير",
    "التسعير بالمحادثة (Conversation-Based) – وليس بعدد الرسائل أو حجمها")

prices = [
    ("Service", "محادثات خدمة العملاء", "مجاني",
     "أول 1000/شهر", "رد على استفسار العميل", WA_GREEN),
    ("Authentication", "رسائل المصادقة", "~$0.005",
     "لكل محادثة", "OTP، رمز تحقق", BLUE_ACCENT),
    ("Utility", "رسائل خدمية", "~$0.008",
     "لكل محادثة", "تأكيد طلب، إشعار شحن", ORANGE_ACCENT),
    ("Marketing", "رسائل تسويقية", "~$0.014",
     "لكل محادثة", "عروض، خصومات، إعلانات", RED_ACCENT),
]

cw3 = Inches(2.9); ch3 = Inches(3.0); gap3 = Inches(0.25)
for i, (name, desc, price, unit, example, clr) in enumerate(prices):
    x = rtl_x(4, i, cw3, gap3, Inches(0.5))
    y = Inches(1.9)
    add_rect(slide, x, y, cw3, ch3, CARD_BG, clr)
    add_text_box(slide, x, y + Inches(0.15), cw3, Inches(0.4),
                 name, 20, clr, bold=True, alignment=PP_ALIGN.CENTER)
    add_text_box(slide, x, y + Inches(0.55), cw3, Inches(0.35),
                 desc, 13, LIGHT_GRAY, alignment=PP_ALIGN.CENTER)
    add_text_box(slide, x, y + Inches(1.0), cw3, Inches(0.6),
                 price, 28, WHITE, bold=True, alignment=PP_ALIGN.CENTER)
    add_text_box(slide, x, y + Inches(1.55), cw3, Inches(0.3),
                 unit, 11, LIGHT_GRAY, alignment=PP_ALIGN.CENTER)
    add_text_box(slide, x, y + Inches(2.1), cw3, Inches(0.5),
                 f"مثال: {example}", 11, LIGHT_GRAY, alignment=PP_ALIGN.CENTER)

pricing_notes = [
    "✅  المحادثة = نافذة 24 ساعة – أرسل عدد غير محدود من الرسائل بنفس التكلفة",
    "✅  الحجم لا يؤثر على السعر – نص صغير أو ملف 100MB = نفس تكلفة المحادثة",
    "✅  1000 محادثة خدمية مجانية شهرياً لكل حساب – الأسعار تختلف حسب البلد",
]
y = Inches(5.2)
for note in pricing_notes:
    add_text_box(slide, Inches(0.5), y, Inches(12.3), Inches(0.45),
                 note, 15, GOLD, alignment=PP_ALIGN.RIGHT)
    y += Inches(0.48)
add_slide_number(slide, 6, TOTAL_SLIDES)

# ═══════════════════════════════════════════════════════════
# SLIDE 7 – Messaging Limits (Tiers RTL)
# ═══════════════════════════════════════════════════════════
slide = prs.slides.add_slide(prs.slide_layouts[6])
add_bg(slide)
add_section_header(slide,
    "📈  حدود الرسائل – المستويات",
    "الحدود تتعلق بعدد المحادثات الجديدة/يوم – وليس حجم الرسالة")

tiers = [
    ("Unverified", "250", "حساب جديد\nغير مُحقق", LIGHT_GRAY),
    ("Tier 1", "1,000", "بعد التحقق من\nالنشاط التجاري", BLUE_ACCENT),
    ("Tier 2", "10,000", "ترقية تلقائية\n(2× خلال 7 أيام)", ORANGE_ACCENT),
    ("Tier 3", "100,000", "ترقية تلقائية\n(2× خلال 7 أيام)", WA_GREEN),
    ("Tier 4", "غير محدود", "ترقية تلقائية\n(2× خلال 7 أيام)", GOLD),
]

tier_w = Inches(2.2); tier_h = Inches(2.8)
for i, (name, limit, desc, clr) in enumerate(tiers):
    x = rtl_x(5, i, tier_w, Inches(0.25), Inches(0.5))
    y = Inches(2.0)
    add_rect(slide, x, y, tier_w, tier_h, CARD_BG, clr)
    add_text_box(slide, x, y + Inches(0.15), tier_w, Inches(0.4),
                 name, 18, clr, bold=True, alignment=PP_ALIGN.CENTER)
    add_text_box(slide, x, y + Inches(0.7), tier_w, Inches(0.6),
                 limit, 30, WHITE, bold=True, alignment=PP_ALIGN.CENTER)
    add_text_box(slide, x, y + Inches(1.3), tier_w, Inches(0.3),
                 "محادثة / 24 ساعة", 10, LIGHT_GRAY, alignment=PP_ALIGN.CENTER)
    add_text_box(slide, x + Inches(0.1), y + Inches(1.8),
                 tier_w - Inches(0.2), Inches(0.8),
                 desc, 12, LIGHT_GRAY, alignment=PP_ALIGN.CENTER)
    if i < 4:
        add_text_box(slide, x - Inches(0.2), y + Inches(1.0),
                     Inches(0.35), Inches(0.4),
                     "←", 22, clr, alignment=PP_ALIGN.CENTER)

# Quality rating box
add_rect(slide, Inches(0.7), Inches(5.2), Inches(11.8), Inches(1.8),
         CARD_BG, WA_TEAL)
add_text_box(slide, Inches(1), Inches(5.35), Inches(11.3), Inches(0.4),
             "⚠️  تقييم الجودة (Quality Rating) يؤثر على مستواك:",
             16, WHITE, bold=True, alignment=PP_ALIGN.RIGHT)

ratings = [
    ("🟢  High (أخضر)", "ممتاز – ترقية ممكنة", WA_GREEN),
    ("🟡  Medium (أصفر)", "متوسط – لا ترقية", ORANGE_ACCENT),
    ("🔴  Low (أحمر)",
     "خطر – قد يتم تخفيض المستوى أو إيقاف الحساب", RED_ACCENT),
]
y = Inches(5.8)
for label, effect, clr in ratings:
    add_text_box(slide, Inches(8), y, Inches(4), Inches(0.35),
                 label, 14, clr, bold=True, alignment=PP_ALIGN.RIGHT)
    add_text_box(slide, Inches(1.2), y, Inches(6.5), Inches(0.35),
                 effect, 13, LIGHT_GRAY, alignment=PP_ALIGN.RIGHT)
    y += Inches(0.38)
add_slide_number(slide, 7, TOTAL_SLIDES)

# ═══════════════════════════════════════════════════════════
# SLIDE 8 – Message Types & Attachments (RTL grid)
# ═══════════════════════════════════════════════════════════
slide = prs.slides.add_slide(prs.slide_layouts[6])
add_bg(slide)
add_section_header(slide,
    "📎  أنواع الرسائل والمرفقات المدعومة",
    "دعم كامل لجميع أنواع الوسائط – الحجم لا يؤثر على التكلفة")

msg_types = [
    ("📝", "نص", "4,096 حرف"),
    ("🖼️", "صورة", "5 MB"),
    ("🎬", "فيديو", "16 MB"),
    ("🎵", "صوت", "16 MB"),
    ("📄", "مستند", "100 MB"),
    ("😀", "ملصق", "500 KB"),
    ("📍", "موقع", "GPS"),
    ("👤", "جهة اتصال", "vCard"),
    ("🔘", "تفاعلي", "أزرار + قوائم"),
    ("📋", "قالب", "معتمد من Meta"),
]

bw = Inches(2.3); bh = Inches(1.5); bgap = Inches(0.2); cols = 5
for i, (icon, name, limit) in enumerate(msg_types):
    col = i % cols; row = i // cols
    x = rtl_x(cols, col, bw, bgap, Inches(0.5))
    y = Inches(1.9) + row * (bh + Inches(0.2))
    add_rect(slide, x, y, bw, bh, CARD_BG, WA_TEAL)
    add_text_box(slide, x, y + Inches(0.1), bw, Inches(0.5),
                 f"{icon}  {name}", 18, WHITE, bold=True,
                 alignment=PP_ALIGN.CENTER)
    add_text_box(slide, x, y + Inches(0.65), bw, Inches(0.4),
                 f"الحد الأقصى: {limit}", 13, WA_GREEN,
                 alignment=PP_ALIGN.CENTER)

add_text_box(slide, Inches(0.5), Inches(5.4), Inches(12.3), Inches(0.5),
             "📌  الصيغ المدعومة: JPEG, PNG, MP4, MP3, OGG, PDF, DOC, XLSX, PPT وغيرها",
             14, LIGHT_GRAY, alignment=PP_ALIGN.RIGHT)
add_text_box(slide, Inches(0.5), Inches(5.9), Inches(12.3), Inches(0.5),
             "💡  يمكن الإرسال عبر رابط URL أو رفع مباشر – مع إمكانية إضافة "
             "تعليق (Caption) لكل مرفق",
             14, GOLD, alignment=PP_ALIGN.RIGHT)
add_slide_number(slide, 8, TOTAL_SLIDES)

# ═══════════════════════════════════════════════════════════
# SLIDE 9 – Why Platform is Required
# ═══════════════════════════════════════════════════════════
slide = prs.slides.add_slide(prs.slide_layouts[6])
add_bg(slide)
add_section_header(slide,
    "🖥️  لماذا المنصة المتخصصة إلزامية؟",
    "الرقم المرتبط بـ Cloud API لا يعمل على تطبيق الهاتف – يحتاج منصة إدارة")

add_rect(slide, Inches(0.8), Inches(2.0), Inches(11.5), Inches(1.5),
         CARD_BG, RED_ACCENT)
add_text_box(slide, Inches(0.8), Inches(2.15), Inches(11.5), Inches(0.5),
             "❌  بدون منصة متخصصة:", 20, RED_ACCENT, bold=True,
             alignment=PP_ALIGN.CENTER)
add_text_box(slide, Inches(0.8), Inches(2.7), Inches(11.5), Inches(0.6),
             "❌ لا إدارة  ←  ❌ لا استقبال  ←  ❌ لا إرسال  ←  "
             "❌ لا يعمل على التطبيق  ←  🔗 مرتبط بـ Cloud API  ←  📱 الرقم",
             16, LIGHT_GRAY, alignment=PP_ALIGN.CENTER)

add_rect(slide, Inches(0.8), Inches(3.8), Inches(11.5), Inches(1.5),
         CARD_BG, WA_GREEN)
add_text_box(slide, Inches(0.8), Inches(3.95), Inches(11.5), Inches(0.5),
             "✅  مع منصة متخصصة:", 20, WA_GREEN, bold=True,
             alignment=PP_ALIGN.CENTER)
add_text_box(slide, Inches(0.8), Inches(4.5), Inches(11.5), Inches(0.6),
             "✅ تقارير  ←  ✅ أتمتة  ←  ✅ حملات  ←  ✅ صندوق وارد  ←  "
             "🖥️ منصة إدارة  ←  🔗 Cloud API  ←  📱 الرقم",
             16, WHITE, alignment=PP_ALIGN.CENTER)

# Comparison – RTL: ✅ on right, ❌ on left
comps = [
    ("لا يمكن رؤية الرسائل الواردة", "صندوق وارد كامل ومنظم"),
    ("لا يمكن الرد على العملاء", "رد فوري مع مرفقات"),
    ("إرسال فردي يدوي عبر API", "حملات جماعية بضغطة زر"),
    ("لا أتمتة أو ردود تلقائية", "أتمتة ذكية + فورم تفاعلي"),
    ("لا تقارير أو تحليلات", "تحليلات مفصلة + تقييم الجودة"),
    ("موظف واحد فقط", "فريق عمل متعدد بصلاحيات"),
]

y = Inches(5.5)
for bad, good in comps:
    add_text_box(slide, Inches(6.8), y, Inches(5.8), Inches(0.3),
                 f"✅  {good}", 12, WA_GREEN, alignment=PP_ALIGN.RIGHT)
    add_text_box(slide, Inches(0.5), y, Inches(5.8), Inches(0.3),
                 f"❌  {bad}", 12, RED_ACCENT, alignment=PP_ALIGN.RIGHT)
    y += Inches(0.3)
add_slide_number(slide, 9, TOTAL_SLIDES)

# ═══════════════════════════════════════════════════════════
# SLIDE 10 – Automation & Interactive Features
# ═══════════════════════════════════════════════════════════
slide = prs.slides.add_slide(prs.slide_layouts[6])
add_bg(slide)
add_section_header(slide,
    "🤖  الأتمتة الذكية والتفاعل مع العملاء",
    "سيناريوهات تفاعلية + فورم تعريفي + ربط بقواعد البيانات")

# Right card – Interactive options
rcard_w = Inches(5.8); rcard_h = Inches(4.5)
rx = Inches(13.333) - Inches(0.5) - rcard_w
add_rect(slide, rx, Inches(1.8), rcard_w, rcard_h, CARD_BG, WA_GREEN)
add_text_box(slide, rx + Inches(0.15), Inches(1.95),
             rcard_w - Inches(0.3), Inches(0.5),
             "🔘  الخيارات التفاعلية", 20, WA_GREEN, bold=True,
             alignment=PP_ALIGN.RIGHT)

items_interactive = [
    "أزرار الرد السريع (Quick Reply) – حتى 3 أزرار",
    "قوائم الاختيار (List Messages) – حتى 10 خيارات",
    "أزرار الروابط (URL Buttons) – توجيه لموقع محدد",
    "أزرار الاتصال (Call Buttons) – اتصال مباشر",
    "سيناريوهات متعددة المراحل (Multi-step Flows)",
    "توجيه ذكي للعملاء حسب اختياراتهم",
    "تحويل للموظف المختص عند الحاجة",
]
y = Inches(2.55)
for item in items_interactive:
    add_text_box(slide, rx + Inches(0.2), y, Inches(5.4), Inches(0.35),
                 f"•  {item}", 13, LIGHT_GRAY, alignment=PP_ALIGN.RIGHT)
    y += Inches(0.34)

# Left-top card – Forms
lx = Inches(0.5); lw = Inches(6.1)
add_rect(slide, lx, Inches(1.8), lw, Inches(2.0), CARD_BG, BLUE_ACCENT)
add_text_box(slide, lx + Inches(0.15), Inches(1.95),
             lw - Inches(0.3), Inches(0.5),
             "📝  فورم تعريفي تفاعلي (WhatsApp Flows)",
             17, BLUE_ACCENT, bold=True, alignment=PP_ALIGN.RIGHT)

form_items = [
    "جمع بيانات التسجيل (الاسم، البريد، الهاتف، العنوان)",
    "استبيانات رضا العملاء + نماذج الطلبات",
    "نماذج حجز المواعيد + تقييم الخدمة",
    "البيانات تُحفظ تلقائياً في قاعدة بيانات العميل (CRM)",
]
y = Inches(2.55)
for item in form_items:
    add_text_box(slide, lx + Inches(0.15), y, lw - Inches(0.3), Inches(0.35),
                 f"•  {item}", 13, LIGHT_GRAY, alignment=PP_ALIGN.RIGHT)
    y += Inches(0.35)

# Left-bottom card – DB Integration
add_rect(slide, lx, Inches(4.1), lw, Inches(2.2), CARD_BG, ORANGE_ACCENT)
add_text_box(slide, lx + Inches(0.15), Inches(4.25),
             lw - Inches(0.3), Inches(0.5),
             "🗄️  الربط بقواعد البيانات", 17, ORANGE_ACCENT, bold=True,
             alignment=PP_ALIGN.RIGHT)

db_items = [
    "بيانات الفورم تُخزّن في سجل العميل تلقائياً",
    "تصنيف تلقائي للعملاء بناءً على اختياراتهم",
    "سجل كامل لكل محادثة وتفاعل",
    "تنبيهات للموظفين عند الحاجة لتدخل بشري",
    "تقارير وتحليلات عن أنماط تفاعل العملاء",
]
y = Inches(4.85)
for item in db_items:
    add_text_box(slide, lx + Inches(0.15), y, lw - Inches(0.3), Inches(0.35),
                 f"•  {item}", 13, LIGHT_GRAY, alignment=PP_ALIGN.RIGHT)
    y += Inches(0.33)

# Bottom example bar
add_rect(slide, Inches(0.5), Inches(6.5), Inches(12.3), Inches(0.7),
         CARD_BG, GOLD)
add_text_box(slide, Inches(0.7), Inches(6.6), Inches(11.9), Inches(0.5),
             "💡  مثال: العميل يتواصل ← يختار من القائمة ← يملأ الفورم ← "
             "البيانات تتخزن تلقائياً ← يتم توجيهه للموظف المختص",
             14, GOLD, bold=True, alignment=PP_ALIGN.CENTER)
add_slide_number(slide, 10, TOTAL_SLIDES)

# ═══════════════════════════════════════════════════════════
# SLIDE 11 – Platform Features (RTL 4-col grid)
# ═══════════════════════════════════════════════════════════
slide = prs.slides.add_slide(prs.slide_layouts[6])
add_bg(slide)
add_section_header(slide,
    "✅  مميزات المنصة المتكاملة",
    "كل ما تحتاجه لإدارة واتساب بيزنس في مكان واحد")

features = [
    ("📨", "صندوق الوارد",
     ["عرض جميع المحادثات", "الرد مع مرفقات", "تعيين لموظفين"]),
    ("👥", "إدارة العملاء CRM",
     ["قاعدة بيانات شاملة", "تصنيف وتقسيم", "استيراد وتصدير"]),
    ("📢", "الحملات",
     ["رسائل جماعية", "جدولة الإرسال", "تتبع النتائج"]),
    ("🤖", "الأتمتة الذكية",
     ["ردود تلقائية + Chatbots", "فورم تعريفي تفاعلي", "ربط بقواعد البيانات"]),
    ("📊", "التقارير",
     ["معدلات التسليم والقراءة", "أداء الحملات", "تقييم الجودة"]),
    ("📋", "القوالب",
     ["إنشاء وإدارة القوالب", "متابعة الاعتماد", "اختبار قبل الإرسال"]),
    ("👥", "المستخدمين",
     ["صلاحيات وأدوار", "فريق عمل متعدد", "سجل العمليات"]),
    ("🔔", "الإشعارات",
     ["إشعارات فورية", "تنبيهات ذكية", "متابعة الأحداث"]),
]

fw = Inches(2.95); fh = Inches(2.2); fgap = Inches(0.2)
for i, (icon, title, bullets) in enumerate(features):
    col = i % 4; row = i // 4
    x = rtl_x(4, col, fw, fgap, Inches(0.4))
    y = Inches(1.8) + row * (fh + Inches(0.2))
    add_rect(slide, x, y, fw, fh, CARD_BG, WA_TEAL)
    add_text_box(slide, x + Inches(0.1), y + Inches(0.1),
                 fw - Inches(0.2), Inches(0.45),
                 f"{icon}  {title}", 16, WA_GREEN, bold=True,
                 alignment=PP_ALIGN.RIGHT)
    by = y + Inches(0.6)
    for b in bullets:
        add_text_box(slide, x + Inches(0.15), by,
                     fw - Inches(0.3), Inches(0.32),
                     f"•  {b}", 12, LIGHT_GRAY, alignment=PP_ALIGN.RIGHT)
        by += Inches(0.3)
add_slide_number(slide, 11, TOTAL_SLIDES)

# ═══════════════════════════════════════════════════════════
# SLIDE 12 – Summary & CTA
# ═══════════════════════════════════════════════════════════
slide = prs.slides.add_slide(prs.slide_layouts[6])
add_bg(slide)
add_green_bar(slide)

add_text_box(slide, Inches(0.5), Inches(1.0), Inches(12.3), Inches(0.8),
             "📋  الخلاصة", 36, WA_GREEN, bold=True,
             alignment=PP_ALIGN.RIGHT)

summary = [
    ("1", "واتساب Cloud API هو الطريقة الرسمية والأمثل للتواصل المؤسسي مع العملاء"),
    ("2", "التسعير بالمحادثة (24 ساعة) – وليس بعدد أو حجم الرسائل"),
    ("3", "حدود الرسائل تعتمد على المستوى وتترقى تلقائياً – غير مرتبطة بالحجم"),
    ("4", "المتطلبات إلزامية: حساب Meta Business + تحقق + رقم مخصص + منصة إدارة"),
    ("5", "الرقم المرتبط بـ API لا يعمل على الهاتف – يحتاج منصة متخصصة إلزامياً"),
    ("6", "تحتاج شركة تقنية متخصصة لإنشاء الحساب وبناء المنصة وإدارة التكامل"),
    ("7", "المنصة توفر: أتمتة ذكية + فورم تعريفي + ربط بقواعد البيانات + خيارات تفاعلية"),
]

y = Inches(2.0)
for num, text in summary:
    add_rect(slide, Inches(0.8), y, Inches(11.5), Inches(0.6), CARD_BG)
    # RTL: number on the right, text to its left
    add_text_box(slide, Inches(11.0), y + Inches(0.1), Inches(1.0), Inches(0.4),
                 num, 20, WA_GREEN, bold=True, alignment=PP_ALIGN.CENTER)
    add_text_box(slide, Inches(1.0), y + Inches(0.1), Inches(9.8), Inches(0.4),
                 text, 15, WHITE, alignment=PP_ALIGN.RIGHT)
    y += Inches(0.68)

add_rect(slide, Inches(2.5), Inches(6.5), Inches(8.3), Inches(0.7),
         WA_DARK_GREEN, WA_GREEN)
add_text_box(slide, Inches(2.5), Inches(6.55), Inches(8.3), Inches(0.6),
             "🚀  نحن جاهزون لمساعدتكم في إنشاء وإدارة حساب واتساب بيزنس الخاص بكم",
             18, WHITE, bold=True, alignment=PP_ALIGN.CENTER)

add_green_bar(slide, Inches(7.4), Inches(0.06))
add_slide_number(slide, 12, TOTAL_SLIDES)

# ─── Save ──────────────────────────────────────────────────
output_path = os.path.join(
    r"c:\Users\Ashraf-pc\source\repos\New folder\docs",
    "WhatsApp-Business-Meta-Integration.pptx")
prs.save(output_path)
print(f"✅ Presentation saved to: {output_path}")
print(f"📊 Total slides: {TOTAL_SLIDES}")

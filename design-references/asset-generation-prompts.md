# ثبت تولید دارایی‌های تصویری Hexara

## روش تولید

حالت استفاده‌شده: ImageGen داخلی Codex، تولید تصویر جدید از متن. تصاویر مرجع برای
جهت هنری مطالعه شدند، اما به‌عنوان تصویر ورودیِ ویرایشی یا منبع کپی استفاده نشدند.
برای هر terrain یک خروجی PNG مربعی ۱۲۵۴×۱۲۵۴ تولید شد؛ سپس خروجی‌های نهایی در
۵۱۲×۵۱۲ و WebP کیفیت ۸۰ آماده شدند.

## prompt پایه

متن زیر پایهٔ مشترک هر شش درخواست بود و بخش «موضوع terrain» برای هر خروجی تغییر کرد:

> Create an original premium digital board-game terrain texture for Hexara, a
> stylized-realistic miniature diorama seen from a consistent near top-down
> orthographic camera. Square seamless-looking composition suitable for clipping
> onto a hex tile, softly modeled depth, controlled vivid color, warm cinematic
> light, tactile hand-painted detail, readable at small size, calm open center for
> a separate number-token overlay, neutral safe edges for neighboring tiles. No
> hex border, no text, no letters, no numbers, no tokens, no roads, no buildings,
> no ports, no logos, no UI, no watermark, no recognizable commercial board-game
> identity. Original Hexara visual language, not Catan and not a copy of any
> reference asset.

موضوع‌های terrain:

1. `forest`: dense evergreen woodland, layered pine canopies, mossy clearings,
   deep emerald and teal-green palette.
2. `hills`: warm terracotta clay ridges, eroded rock strata, brick-red soil and
   sparse scrub, no loose round boulders in the center.
3. `fields`: sculpted golden grain terraces and flowing harvest rows, amber and
   honey palette with a calm central clearing.
4. `pasture`: lush green meadow, gentle grassy contours, a few tiny pale sheep
   away from the center, fresh spring palette.
5. `mountains`: cool slate mountain range, angular stone faces and restrained
   snowy caps, steel-blue and charcoal palette.
6. `desert`: windswept sand dunes, weathered sandstone, sparse dry shrubs and a
   tiny oasis accent away from the center, ochre and warm beige palette.

## نگاشت منابع

| خانواده | PNG منبع ImageGen |
|---|---|
| forest | `C:\Users\sadra\.codex\generated_images\019fb3bf-0a12-7253-b3eb-289f6ce88cdd\call_b8OhHt2wA3RCqkMEaAFW7VUj.png` |
| hills | `C:\Users\sadra\.codex\generated_images\019fb3bf-0a12-7253-b3eb-289f6ce88cdd\call_w4pjtDCYUvzy3sDTtXOfcDOd.png` |
| fields | `C:\Users\sadra\.codex\generated_images\019fb3bf-0a12-7253-b3eb-289f6ce88cdd\call_uDsGwGp0qzIsk3eF9Jjyr5kE.png` |
| pasture | `C:\Users\sadra\.codex\generated_images\019fb3bf-0a12-7253-b3eb-289f6ce88cdd\call_00G0BVbz9G8tfDlMTqXTn9Re.png` |
| mountains | `C:\Users\sadra\.codex\generated_images\019fb3bf-0a12-7253-b3eb-289f6ce88cdd\call_RouUnJX7gM82hzjZpACbaTGF.png` |
| desert | `C:\Users\sadra\.codex\generated_images\019fb3bf-0a12-7253-b3eb-289f6ce88cdd\call_k9HcafoyjnBKBVX8G1Uftk4u.png` |

## خروجی‌های محصول

برای هر خانواده سه فایل `01`، `02` و `03` در مسیر
`src/Hexara.Web/ClientApp/src/assets/generated/terrain` قرار دارد. نسخهٔ `01`
خروجی اصلیِ resizeشده است؛ `02` و `03` variationهای flipشده و قطعی هستند.
هر ۱۸ فایل در `TERRAIN_ART` ثبت و فقط از همان رجیستری مصرف می‌شوند.

## کارت‌های منابع

در مرحله‌ی پرداخت نهایی، برای پنج منبع نیز تصویر مستقل تولید شد. قاب طلایی، عنوان ترجمه‌شده،
تعداد و حالت انتخاب در Vue/CSS ساخته می‌شوند و داخل تصویر bake نشده‌اند. الگوی prompt مشترک:

> Create one original premium fantasy board-game resource card illustration for
> the independent game Hexara. Centered hero object, painterly 3D tabletop-game
> rendering, high material detail, dramatic but readable, luxurious dark navy and
> antique gold mood. Portrait artwork, important subject inside the central 70%
> safe area. No border, frame, text, letters, numbers, logo, symbols, watermark or
> UI. Original design, not based on any existing board-game brand or trade dress.

موضوع‌های اختصاصی: کنده‌های چوب در جنگل، آجرهای سفالی در معدن رس، دسته‌ی گندم رسیده،
گوسفند سفید در مرتع و سنگ‌های معدنی رگه‌دار در معدن کوهستانی.

| منبع | PNG اصلی ImageGen | خروجی محصول |
|---|---|---|
| Lumber | `call_XA3rzlCJJr89ynposDRMwP0M.png` | `generated/resources/lumber.jpg` |
| Brick | `call_beniZ2bus6wDAGdRkcC2cUkZ.png` | `generated/resources/brick.jpg` |
| Grain | `call_iuTKpuIzHMnBRqULbudL8KsR.png` | `generated/resources/grain.jpg` |
| Wool | `call_P4BKB73CV9cVtOgtomlNHKVe.png` | `generated/resources/wool.jpg` |
| Ore | `call_rW5WZJrDrwVoG2qmLf5cNUTE.png` | `generated/resources/ore.jpg` |

خروجی‌های محصول با ابعاد ۵۱۲×۷۶۸ و JPEG کیفیت ۹۰ آماده و از رجیستری مرکزی مصرف می‌شوند.

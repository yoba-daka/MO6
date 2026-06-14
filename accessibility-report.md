


להלן דוח בדיקות הנגישות, מאורגן ומסודר בפורמט Markdown קריא וברור. הדוח מחולק לארבעת עקרונות הנגישות המרכזיים (ניתן לתפיסה, ניתן להפעלה, ניתן להבנה, ועמיד). 

תחת כל קטגוריה פיצלתי את הבדיקות לאלו שנכשלו (כולל הסברים וקטעי הקוד הבעייתיים), אלו שעברו בהצלחה, ואלו שאינן רלוונטיות לעמוד.

---

# דוח סטטוס נגישות (Accessibility Report)

**סיכום תוצאות:**
* 🔴 **נכשל:** 14 בדיקות
* 🟢 **עבר:** 15 בדיקות
* ⚪ **לא רלוונטי:** 17 בדיקות

---

## 1. ניתן לתפיסה (Perceivable)
**סך הכל בעיות בקטגוריה זו: 62**

### 🔴 בדיקות שנכשלו

#### 1. כותרות חייבות לכלול טקסט גלוי
* **אלמנטים מושפעים:** 1
* **תקן WCAG:** גרסה 2.0/2.1/2.2 (רמה A) | SC 1.3.1 Info and Relationships
* **סיכום הבעיה:** תגיות כותרת (H1-H6 או `role="heading"`) צריכות לכלול טקסט ברור. אם הכותרת בנויה מאלמנטים גרפיים בלבד, יש להוסיף לה שם נגיש בעזרת `aria-label`, `aria-labelledby` או `title` כדי שהמשמעות שלה תישמר.
* **למה זה חשוב:** קוראי מסך מציגים רשימת כותרות כדי לסייע בניווט. כותרות ריקות מקשות על המשתמש להבין את מבנה הדף.

**קוד האלמנטים המושפעים:**
```html
<h6 class="mt-0 card-category" data-open-accessibility-text-original="12px" style="font-size: 12px;"></h6>
```

#### 2. קישורים חייבים להיות ניתנים להבחנה מהטקסט הסביב מבלי להסתמך על צבע בלבד
* **אלמנטים מושפעים:** 2
* **תקן WCAG:** גרסה 2.0/2.1/2.2 (רמה A) | SC 1.4.1 Use of Color
* **סיכום הבעיה:** כאשר קישורים מוצגים בתוך גוף טקסט ומסתמכים אך ורק על צבע כדי להתבלט, הצבע חייב להיות ביחס ניגודיות של לפחות 3:1, או שחייב להיות רמז חזותי נוסף כגון קו תחתי.
* **למה זה חשוב:** משתמשים עם ליקויי ראיית צבעים עשויים להיות חסרי יכולת לחלוטין להבחין בין קישור לטקסט הסביב.

**קוד האלמנטים המושפעים:**
```html
<!-- אלמנט 1 -->
<a class="text-success" href="/תקנון" target="_blank" data-open-accessibility-text-original="14px" style="font-size: 14px;">לתקנון</a>

<!-- אלמנט 2 -->
<a class="text-success" href="/תקנון" target="_blank" data-open-accessibility-text-original="14px" style="font-size: 14px;">לתקנון</a>
```

#### 3. יש להגדיר תגיות מתאימות באייקונים ואלמנטים מסוג SVG
* **אלמנטים מושפעים:** 3
* **תקן WCAG:** גרסה 2.0/2.1/2.2 (רמה A) | SC 1.1.1 Non-text Content
* **סיכום הבעיה:** אלמנטים מסוג SVG המשמשים לדקורציה בלבד נדרשים לכלול `aria-hidden`. אלמנטים המעבירים תוכן צריכים לכלול שם נגיש מתאים.

**קוד האלמנטים המושפעים:**
```html
<!-- אלמנט 1 -->
<svg xmlns="http://www.w3.org/2000/svg"><symbol id="plyr-airplay"><path d="M16 1H2a1 1 0 0 0-1 1v10a1 1 0 0 0 1 1h3v-2H3V3h12v8h-2v2h3a1 1 0 0 0 1-1V2a1 1 0 0 0-1-1z"></path><path d="M4 17h10l-5-6z"></path></symbol>... (קוד מקוצר)</svg>

<!-- אלמנט 2 -->
<svg viewBox="0 2 24 24" xmlns="http://www.w3.org/2000/svg"> <path d="M0 0h24v24H0z" fill="none"></path> <circle cx="12" cy="4" r="2"></circle> <path d="M19 13v-2c-1.54.02-3.09-.75-4.07-1.83l-1.29-1.43c-.17-.19-.38-.34-.61-.45-.01 0-.01-.01-.02-.01H13c-.35-.2-.75-.3-1.19-.26C10.76 7.11 10 8.04 10 9.09V15c0 1.1.9 2 2 2h5v5h2v-5.5c0-1.1-.9-2-2-2h-3v-3.45c1.29 1.07 3.25 1.94 5 1.95zm-6.17 5c-.41 1.16-1.52 2-2.83 2-1.66 0-3-1.34-3-3 0-1.31.84-2.41 2-2.83V12.1c-2.28.46-4 2.48-4 4.9 0 2.76 2.24 5 5 5 2.42 0 4.44-1.72 4.9-4h-2.07z"></path> </svg>

<!-- אלמנט 3 -->
<svg version="1.0" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" x="0px" y="0px" viewBox="0 0 40 40" enable-background="new 0 0 40 40" xml:space="preserve"> <path fill="#000000" d="M22,20l5.9,5.9l-2,2L20,22l-5.9,5.9l-2-2L18,20l-5.9-5.9l2-2L20,18l5.9-5.9l2,2L22,20z"></path> </svg>
```

#### 4. לטקסט חייב להיות ניגודיות מספקת ביחס לרקע שלו
* **אלמנטים מושפעים:** 56
* **תקן WCAG:** גרסה 2.0/2.1/2.2 (רמה AA) | SC 1.4.3 Contrast (Minimum)
* **סיכום הבעיה:** לטקסט ולתמונות טקסט יהיה יחס ניגודיות של לפחות 4.5:1 ביחס לרקע עבור טקסט בגודל רגיל. בעל ניגודיות נמוכה קשה או בלתי אפשרי לקריאה עבור משתמשים עם ראייה לקויה.

**מדגם קוד (10 הראשונים מתוך 56):**
```html
<button class="btn btn-primary btn-round px-3 py-2 mb-3 mb-sm-0 d-inline-flex align-items-center" data-toggle="modal" data-target="#loginModal" data-open-accessibility-text-original="12px" style="font-size: 12px;"><i class="mdi-small mdi-line-1-2 mdi-login-variant" data-open-accessibility-text-original="12px" style="font-size: 12px;"></i> התחברות</button>

<button class="btn btn-success btn-round px-3 py-2 d-inline-flex align-items-center" data-toggle="modal" data-target="#registerModal" data-open-accessibility-text-original="12px" style="font-size: 12px;"><i class="mdi-small mdi-line-1-2 mdi-pencil-box-outline" data-open-accessibility-text-original="12px" style="font-size: 12px;"></i> הרשמה</button>

<button class="btn btn-primary btn-round btn-home p-2" style="line-height: 1.4; font-size: 19.2px;" data-toggle="modal" data-target="#loginModal" data-open-accessibility-text-original="19.2px"><i class="mdi mdi-login-variant" data-open-accessibility-text-original="19.2px" style="font-size: 19.2px;"></i> התחברות</button>

<button class="btn btn-success btn-round btn-home p-2" style="line-height: 1.4; font-size: 19.2px;" data-toggle="modal" data-target="#registerModal" data-open-accessibility-text-original="19.2px"><i class="mdi mdi-pencil-box-outline" data-open-accessibility-text-original="19.2px" style="font-size: 19.2px;"></i> הרשמה</button>

<h6 class="mt-0 card-category" data-open-accessibility-text-original="12px" style="font-size: 12px;">כל האופציות ללמוד זוגיות אצל משה</h6>

<h6 class="mt-0 card-category" data-open-accessibility-text-original="12px" style="font-size: 12px;">אוהבים ללמוד דברים חדשים?</h6>

<h6 class="mt-0 card-category" data-open-accessibility-text-original="12px" style="font-size: 12px;">סודות ויסודות חכמת הקבלה</h6>

<p class="card-description" data-open-accessibility-text-original="14px" style="font-size: 14px;"> משה שרון מלמד קבלה קרוב ל 23 שנה ומפשט רעיונות גבוהים בשפה פשוטה בהרצאותיו ובספריו. רוצים להפתח לתוכן מרתק ומשנה חיים? מוזמנים לצלול בעזרת ספריו ובעזרת הקורסים וההרצאות שמסר על סוד האותיות והשמות, כתבי הרב אשלג, כתבי הרב קוק, רבי משה קורדוברו, האר"י הקדוש ועוד... </p>

<p class="card-description" data-open-accessibility-text-original="14px" style="font-size: 14px;"> משה שרון הוא מטפל אישי וזוגי ומכשיר מאמנים ומטפלים זוגיים בתכנית "טוב ומיטיב". בעל נסיון של 20 שנה בעזרה לזוגות לפני ואחרי חתונה, הן כמרצה והן כמטפל. העביר 35 מחזורים פיזיים של הקורס "זוגיות לפי הקבלה" לזוגות, מלמד זוגות בקורס הדיגיטלי "ירח דבש" והוציא לאור את הספר "זוגות זוגות". </p>

<p class="card-description" data-open-accessibility-text-original="14px" style="font-size: 14px;"> משה שרון הקליט עשרות פרקים בפודקאסט המצליח "איך עושה משפחה?" עם עדי הרפז שעוסק בחינוך ילדים וזוגיות והתארח בעשרות פודקאסטים נוספים כמו למשל עם אייל אברהם לוי על שליחות, עם איתן עזריה על חוק השתוות הצורה, עם עידן שלי על נפלאות הכעס, עם נועה זגורי על זוגיות ועם ערן שטרן ועוד ועוד. מוזמנים להאזין ולצפות </p>
```

### 🟢 בדיקות שעברו
* אלמנטים מסוג `<li>` חייבים להופיע ישירות בתוך אלמנטים מסוג `<ul>`
* דפים חייבים לכלול מבנה ציוני דרך תקין (main אחד, banner אחד, contentinfo אחד)
* כפתורי שליחה חייבים להיות בתוך אלמנטים מסוג `form`
* יש להגדיר תוויות ניתנות לזיהוי תוכניתית עבור שדות טופס
* יש להגדיר טקסט חלופי לתמונות באלמנטים מסוג `<img>`
* תמונות המעבירות תוכן שהוטמעו כתמונת רקע דורשות תגיות מתאימות

### ⚪ לא רלוונטי
* כותרות תמיד צריכות להיות מובנות באמצעות תגיות H
* ציוני דרך מאותו סוג חייבים לכלול שמות נגישים ייחודיים
* יש להגדיר תגיות מתאימות באלמנטים מסוג `OBJECT` ו `CANVAS`
* יש להגדיר טקסט חלופי עבור מפות אינטראקטיביות
* תוכן חייב להיות ניתן להגדלה עד 200% ללא אובדן פונקציונליות

---

## 2. ניתן להפעלה (Operable)
**סך הכל בעיות בקטגוריה זו: 25**

### 🔴 בדיקות שנכשלו

#### 1. כפתורים, קישורים ואלמנטים אינטראקטיבים נדרשים להגדיר תגיות לניווט באמצעות מקלדת
* **אלמנטים מושפעים:** 1
* **תקן WCAG:** גרסה 2.1/2.2 (רמה A) | SC 2.1.1 Keyboard
* **סיכום הבעיה:** כל אלמנט אינטראקטיבי נדרש להיות נגיש לניווט מקלדת ע"י הוספת `tabindex` חיובי (או 0).

**קוד האלמנטים המושפעים:**
```html
<div class="open-accessibility-expand-button" style=""> <!--?xml version="1.0" encoding="utf-8"?--> <svg viewBox="0 2 24 24" xmlns="http://www.w3.org/2000/svg"> <path d="M0 0h24v24H0z" fill="none"></path> <circle cx="12" cy="4" r="2"></circle> <path d="M19 13v-2c-1.54.02-3.09-.75-4.07-1.83l-1.29-1.43c-.17-.19-.38-.34-.61-.45-.01 0-.01-.01-.02-.01H13c-.35-.2-.75-.3-1.19-.26C10.76 7.11 10 8.04 10 9.09V15c0 1.1.9 2 2 2h5v5h2v-5.5c0-1.1-.9-2-2-2h-3v-3.45c1.29 1.07 3.25 1.94 5 1.95zm-6.17 5c-.41 1.16-1.52 2-2.83 2-1.66 0-3-1.34-3-3 0-1.31.84-2.41 2-2.83V12.1c-2.28.46-4 2.48-4 4.9 0 2.76 2.24 5 5 5 2.42 0 4.44-1.72 4.9-4h-2.07z"></path> </svg> </div>
```

#### 2. קישורים חייבים לכלול תיאור המסביר את מטרתם
* **אלמנטים מושפעים:** 1
* **תקן WCAG:** גרסה 2.1/2.2 (רמה A) | SC 2.4.4 Link Purpose (In Context)
* **סיכום הבעיה:** כל הקישורים חייבים לכלול שמות נגישים המתארים בבירור את מטרתם או היעד שלהם.

**קוד האלמנטים המושפעים:**
```html
<a href="https://zmaneshkol.co.il/product/%D7%A1%D7%55%D7%93%D7%55%D7%AA-%D7%97%D7%9B%D7%9E%D7%AA-%D7%A7%D7%91%D7%9C%D7%94-%D7%A8%D7%90%D7%A9%D7%9C%D7%A6-%D7%91-26-31718/" data-open-accessibility-text-original="14px" style="font-size: 14px;"> <h6 class="mt-0 card-category" data-open-accessibility-text-original="12px" style="font-size: 12px;"></h6> </a>
```

#### 3. שם נגיש של שדה טופס חייב לכלול את טקסט התווית הגלויה
* **אלמנטים מושפעים:** 1
* **תקן WCAG:** גרסה 2.1/2.2 (רמה A) | SC 2.5.3 Label in Name
* **סיכום הבעיה:** כאשר לפקד יש גם תווית גלויה וגם שם נגיש (כגון `aria-label`), יש לוודא שהשם הנגיש מכיל את הטקסט הזהה לתווית הגלויה כדי לשמור על עקביות.

**קוד האלמנטים המושפעים:**
```html
<input data-plyr="seek" type="range" min="0" max="100" step="0.01" value="0" autocomplete="off" role="slider" aria-label="Seek" aria-valuemin="0" aria-valuemax="100" aria-valuenow="0" id="plyr-seek-9457" style="--value: 0%;" class="">
```

#### 4. אלמנטי iframe שניתן להעביר אליהם מיקוד אסור שיהיה להם tabindex="-1"
* **אלמנטים מושפעים:** 3
* **תקן WCAG:** גרסה 2.1/2.2 (רמה A) | SC 2.1.1 Keyboard & SC 2.4.3 Focus Order
* **סיכום הבעיה:** הגדרת `tabindex="-1"` על `iframe` מסירה אותו לחלוטין מסדר ה-Tab של המקלדת ופוגעת בנגישות התוכן עבור משתמשים הנשענים על מקלדת.

**קוד האלמנטים המושפעים:**
```html
<!-- אלמנט 1 -->
<iframe id="youtube-7973" frameborder="0" allowfullscreen="" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" referrerpolicy="strict-origin-when-cross-origin" title="Player for " width="640" height="360" src="https://www.youtube.com/embed/5XXAjk7k-Po?..." tabindex="-1"></iframe>

<!-- אלמנט 2 -->
<iframe title="reCAPTCHA" width="256" height="60" role="presentation" name="a-vdldvwmltxw5" frameborder="0" scrolling="no" sandbox="..." src="https://www.google.com/recaptcha/api2/anchor?..." tabindex="-1"></iframe>

<!-- אלמנט 3 -->
<iframe title="reCAPTCHA" width="256" height="60" role="presentation" name="a-fdg6fz7261ar" frameborder="0" scrolling="no" sandbox="..." src="https://www.google.com/recaptcha/api2/anchor?..." tabindex="-1"></iframe>
```

#### 5. כפתורים, קישורים, ואלמנטים אינטראקטיבים נדרשים להציג מיקוד לניווט מקלדת
* **אלמנטים מושפעים:** 19
* **תקן WCAG:** גרסה 2.0/2.1/2.2 (רמה AA) | SC 2.4.7 Focus Visible
* **סיכום הבעיה:** אלמנטים אינטראקטיביים נדרשים להציג מיקוד מקלדת הנראה לעין (כגון מסגרת, קו תחתי או שינוי צבע) כאשר מנווטים אליהם.

**מדגם קוד (חלק מתוך 19):**
```html
<a class="navbar-brand pt-1" href="/" data-open-accessibility-text-original="18px" style="font-size: 18px;"> <img src="/media/sz3hjxoe/icon-moshe-sharon.svg" alt="משה שרון - לדף הבית" class="icon"> </a>

<a class="navbar-brand" href="/" data-open-accessibility-text-original="18px" style="font-size: 18px;"> משה שרון </a>

<a href="/אודות" class="nav-link" data-open-accessibility-text-original="12px" style="font-size: 12px;"> <i class="mdi mdi-information mdi-line-1" data-open-accessibility-text-original="12px" style="font-size: 12px;"></i> אודות </a>

<a class="nav-link" href="/מאמרים" data-open-accessibility-text-original="12px" style="font-size: 12px;"> <i class="mdi mdi-card-text mdi-line-1" data-open-accessibility-text-original="12px" style="font-size: 12px;"></i> מאמרים </a>

<a class="nav-link" href="/פודקאסטים" data-open-accessibility-text-original="12px" style="font-size: 12px;"> <i class="mdi mdi-headphones mdi-line-1" data-open-accessibility-text-original="12px" style="font-size: 12px;"></i> פודקאסטים </a>

<a class="nav-link" href="/קורסים-והרצאות" data-open-accessibility-text-original="12px" style="font-size: 12px;"> <i class="mdi mdi-library-video mdi-line-1" data-open-accessibility-text-original="12px" style="font-size: 12px;"></i> קורסים והרצאות </a>

<a class="nav-link" href="/אירועים" data-open-accessibility-text-original="12px" style="font-size: 12px;"> <i class="mdi mdi-calendar mdi-line-1" data-open-accessibility-text-original="12px" style="font-size: 12px;"></i> אירועים </a>

<a class="nav-link" href="/ספרים" data-open-accessibility-text-original="12px" style="font-size: 12px;"> <i class="mdi mdi-bookshelf mdi-line-1" data-open-accessibility-text-original="12px" style="font-size: 12px;"></i> ספרים </a>

<a class="nav-link" href="/צור-קשר" data-open-accessibility-text-original="12px" style="font-size: 12px;"> <i class="mdi mdi-contact-mail mdi-line-1" data-open-accessibility-text-original="12px" style="font-size: 12px;"></i> צור קשר </a>

<a class="nav-link" href="/מנוי" data-open-accessibility-text-original="12px" style="font-size: 12px;"> <i class="mdi mdi-crown mdi-line-1" data-open-accessibility-text-original="12px" style="font-size: 12px;"></i> מנוי </a>
```

### 🟢 בדיקות שעברו
* הגדרת קישורי דילוג למשתמשים המנווטים במקלדת
* הגדרת כותרת העמוד באמצעות אלמנט מסוג `TITLE`
* בכל עמוד צריכה להופיע כותרת ראשית אחת בדיוק עם שם נגיש מתאים

### ⚪ לא רלוונטי
* יש להגדיר תגית `tabindex` בכפתורים, קישורים, ואלמנטים מוסתרים, כדי להמנע מניווט כושל באמצעות מקלדת
* שדות חיפוש חייבים להיות עטופים עם `role="search"`
* חלונות קופצים נדרשים ללכוד את ניווט המקלדת
* חלונות קופצים הלוכדים את הניווט חייבים לאפשר יציאה דרך המקלדת

---

## 3. ניתן להבנה (Understandable)
**סך הכל בעיות בקטגוריה זו: 57**

### 🔴 בדיקות שנכשלו

#### 1. יש לסמן טקסט בשפה שונה משפת העמוד בתגית lang
* **אלמנטים מושפעים:** 29
* **תקן WCAG:** גרסה 2.0/2.1/2.2 (רמה AA) | SC 3.1.2 Language of Parts
* **סיכום הבעיה:** טקסט הנכתב בשפה שונה מהשפה שהוגדרה באלמנט מסוג HTML חייב לכלול את תגית ה- `lang` המתאימה, כדי לאפשר לקוראי מסך להקריא את התוכן בשפה הרלוונטית.

**מדגם קוד (חלק מתוך 29):**
```html
<span class="label--pressed plyr__sr-only">Pause</span>

<progress class="plyr__progress__buffer" min="0" max="100" value="0" role="presentation" aria-hidden="true">% buffered</progress>

<span class="label--pressed plyr__sr-only">Disable captions</span>

<span class="label--not-pressed plyr__sr-only">Enable captions</span>

<span class="plyr__sr-only">Settings</span>

<span>Captions<span class="plyr__menu__value">Disabled</span></span>

<span>Quality<span class="plyr__menu__value">undefined</span></span>

<span class="plyr__menu__value">undefined</span>

<span>Speed<span class="plyr__menu__value">Normal</span></span>

<span aria-hidden="true">Captions</span>

<span class="plyr__sr-only">Go back to previous menu</span>

<span aria-hidden="true">Quality</span>

<span class="plyr__sr-only">Go back to previous menu</span>

<span aria-hidden="true">Speed</span>

<span class="plyr__sr-only">Go back to previous menu</span>

<span class="label--pressed plyr__sr-only">Exit fullscreen</span>

<span class="label--not-pressed plyr__sr-only">Enter fullscreen</span>

<i class="material-icons" aria-hidden="true" data-open-accessibility-text-original="16px" style="font-size: 16px;">clear</i>

<i class="material-icons" aria-hidden="true" data-open-accessibility-text-original="16px" style="font-size: 16px;">clear</i>

<i class="material-icons" data-open-accessibility-text-original="24px" style="font-size: 24px;">badge</i>

<i class="material-icons" data-open-accessibility-text-original="24px" style="font-size: 24px;">email</i>

<i class="material-icons" data-open-accessibility-text-original="24px" style="font-size: 24px;">done_all</i>

<i class="material-icons" aria-hidden="true" data-open-accessibility-text-original="16px" style="font-size: 16px;">clear</i>

<i class="material-icons" data-open-accessibility-text-original="24px" style="font-size: 24px;">badge</i>

<i class="material-icons" data-open-accessibility-text-original="24px" style="font-size: 24px;">email</i>

<i class="material-icons" data-open-accessibility-text-original="24px" style="font-size: 24px;">done_all</i>

<i class="material-icons" data-open-accessibility-text-original="24px" style="font-size: 24px;">phone</i>

<i class="material-icons" aria-hidden="true" data-open-accessibility-text-original="16px" style="font-size: 16px;">clear</i>
```

#### 2. קישורים הנפתחים בחלונות חדשים צריכים לכלול אינדיקטורים ברורים
* **אלמנטים מושפעים:** 26
* **תקן WCAG:** גרסה 2.0/2.1/2.2 (רמה AAA) | SC 1.3.1 Info and Relationships (Note)
* **סיכום הבעיה:** קישורים הנפתחים בחלונות או בטאבים חדשים צריכים להיות מזוהים בבירור כדי שמשתמשים יבינו שהם עומדים להילקח להקשר גלישה חדש.

**מדגם קוד (חלק מתוך 26):**
```html
<a href="/הצהרת-נגישות.pdf" target="_blank">הצהרת נגישות</a>

<a href="https://www.kabook.co.il/items~3.htm" target="_blank" class="btn btn-primary btn-round px-3 py-2" data-open-accessibility-text-original="12px" style="font-size: 12px;"> ספרי קבלה</a>

<a href="https://mo6.co.il/%D7%9E%D7%A0%D7%95%D7%99" target="_blank" class="btn btn-primary btn-round px-3 py-2" data-open-accessibility-text-original="12px" style="font-size: 12px;"> מנוי לסדרות</a>

<a href="/מאמרים/מאמרים-על-חכמת-הקבלה/" target="_blank" class="btn btn-primary btn-round px-3 py-2" data-open-accessibility-text-original="12px" style="font-size: 12px;"> מאמרים </a>

<a href="https://sharon.mo6.co.il/honeymoon/" target="_blank" class="btn btn-primary btn-round px-3 py-2" data-open-accessibility-text-original="12px" style="font-size: 12px;"> קורס דיגיטלי</a>

<a href="/ספרים/זוגות-זוגות/" target="_blank" class="btn btn-primary btn-round px-3 py-2" data-open-accessibility-text-original="12px" style="font-size: 12px;"> זוגות זוגות - הספר בחצי מחיר</a>

<a href="https://mo6.co.il/%D7%9E%D7%A0%D7%95%D7%99" target="_blank" class="btn btn-primary btn-round px-3 py-2" data-open-accessibility-text-original="12px" style="font-size: 12px;"> עשו מנוי לאתר וקורסי הזוגיות יפתחו לכם</a>

<a href="/קורסים-והרצאות/פודקאסטים/משה-שרון-מתארח-בפודקאסטים/העולם-לא-יכול-להתקיים-בלעדיך/" target="_blank" class="btn btn-primary btn-round px-3 py-2" data-open-accessibility-text-original="12px" style="font-size: 12px;"> שליחות</a>

<a href="/קורסים-והרצאות/פודקאסטים/משה-שרון-מתארח-בפודקאסטים/גבולות-הגיון-עם-עידן-שלי-נפלאות-הכעס/" target="_blank" class="btn btn-primary btn-round px-3 py-2" data-open-accessibility-text-original="12px" style="font-size: 12px;"> נפלאות הכעס</a>

<a href="/קורסים-והרצאות/פודקאסטים/משה-שרון-מתארח-בפודקאסטים/עם-איתן-עזריה-על-חוק-השתוות-הצורה/" target="_blank" class="btn btn-primary btn-round px-3 py-2" data-open-accessibility-text-original="12px" style="font-size: 12px;"> השתוות הצורה</a>
```

#### 3. שדות טופס חייבים לכלול תוויות או הוראות גלויות
* **אלמנטים מושפעים:** 2
* **תקן WCAG:** גרסה 2.0/2.1/2.2 (רמה A) | SC 3.3.2 Labels or Instructions
* **סיכום הבעיה:** כל שדה טופס צריך לכלול טקסט גלוי שמתאר בבירור את מטרתו (תווית, טקסט הוראות וכד').

**קוד האלמנטים המושפעים:**
```html
<!-- אלמנט 1 -->
<input data-plyr="volume" type="range" min="0" max="1" step="0.05" value="1" autocomplete="off" role="slider" aria-label="Volume" aria-valuemin="0" aria-valuemax="100" aria-valuenow="100" id="plyr-volume-9457" aria-valuetext="100.0%" style="--value: 100%;" class="">

<!-- אלמנט 2 -->
<input id="newsletterEmail" name="fields[subscribers_email]" type="email" class="form-control" autocomplete="email" placeholder="כתובת האימייל שלך..." aria-describedby="newsletterHelp" required="">
```

### 🟢 בדיקות שעברו
* במהלך ניווט מקלדת, אין לאפשר מעבר בלתי צפוי בין אלמנטים
* יש להגדיר את שפת העמוד על ידי תגית ה `lang` באלמנט ה `<html>`

---

## 4. עמיד (Robust)
**סך הכל בעיות בקטגוריה זו: 5**

### 🔴 בדיקות שנכשלו

#### 1. טופס חייב לכלול שם נגיש באמצעות תגיות ARIA
* **אלמנטים מושפעים:** 4
* **תקן WCAG:** גרסה 2.0/2.1/2.2 (רמה A) | SC 4.1.2 Name, Role, Value
* **סיכום הבעיה:** אלמנטים מסוג טופס (`<form>`) חייבים לכלול שמות נגישים הניתנים לזיהוי תוכניתי באמצעות `aria-label`, `aria-labelledby`, או תגיות `title`.

**קוד האלמנטים המושפעים:**
```html
<!-- אלמנט 1 -->
<form class="newsletter-form" action="https://ravxx-subscriber-v1.ravpage.co.il/" method="post"> ... </form>

<!-- אלמנט 2 -->
<form action="/" enctype="multipart/form-data" id="formff4e5b62114041a98f16b6912776e2fb" method="post" name="loginForm" novalidate="novalidate"> ... </form>

<!-- אלמנט 3 -->
<form action="/" enctype="multipart/form-data" id="form9c7dce16db4a467da680e8117edd07f9" method="post" name="registerForm" novalidate="novalidate"> ... </form>

<!-- אלמנט 4 -->
<form action="/" enctype="multipart/form-data" id="forma32350b88f7c49e09c1c8f1a81eb50ec" method="post" novalidate="novalidate"> ... </form>
```
*(תוכן הטפסים קוצר לשם קריאות)*

#### 2. לאלמנטי iframe חייב להיות שם נגיש ייחודי ולא ריק באמצעות תכונת title
* **אלמנטים מושפעים:** 1
* **תקן WCAG:** גרסה 2.0/2.1/2.2 (רמה A) | SC 4.1.2 Name, Role, Value
* **סיכום הבעיה:** קוראי מסך משתמשים בתכונת `title` של `iframe` כדי להכריז על מטרתו. ה-title לא יכול להיות ריק (title="") או חסר.

**קוד האלמנטים המושפעים:**
```html
<iframe style="display: none;" tabindex="-1"></iframe>
```

### 🟢 בדיקות שעברו
* המנעות מקינון של אלמנטים אינטראקטיביים זה בתוך זה
* תכונות ARIA חייבות להפנות למזהים קיימים במסמך
* כפתורים, קישורים ואלמנטים אינטראקטיבים מסומנים בתגיות `role` לקוראי מסך
* כפתורים, קישורים ואלמנטים אינטראקטיבים מסומנים בתגיות `ARIA` לקוראי מסך

### ⚪ לא רלוונטי
* Anchor elements with href="#" must have a role matching their purpose
* כפתורים, קישורים, ואלמנטים חבויים, מוסתרים מקוראי מסך על ידי סימון של תגיות ARIA
* יש להימנע מהכללת אלמנטים אינטראקטיביים בתוך אלמנט `<label>`, למעט שדה הטופס שאותו התווית מתייגת
* אלמנטים שמפעילים תפריטי משנה צריכים להשתמש ב-`aria-haspopup` כדי לציין נוכחות תפריט משנה
* רשימת בחירה חייבת לכלול אלמנטי אפשרות
* אלמנטי אפשרות לא יכולים לכלול צאצאים אינטראקטיביים
* אלמנטי `summary` לא יכולים לכלול צאצאים אינטראקטיביים
* אלמנטים שפותחים או סוגרים תפריטי משנה צריכים להשתמש ב-`aria-expanded` כדי לציין את מצבם הנוכחי
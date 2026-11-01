# DECISIONS

בחרתי באפשרות העבודה עם Docker ו־PostgreSQL, בהתאם למפרט המשימה.

## 1. החלטות ארכיטקטוניות
- הפרדתי בין `Controller` ל־`Service`: ה־Controller אחראי ל־HTTP, וה־Service ללוגיקה העסקית ולגישה ל־DB.
- לא הוספתי שכבות ותבניות מיותרות (Over-engineering).
- ב־Angular העברתי קריאות HTTP ל־Service, החלפתי `any` בטיפוסים מוגדרים והשתמשתי ב־RxJS לניהול subscriptions ולמניעת memory leaks.

## 2. הבאג ביתרת החופשה
- **הבאג:** בדיקת היתרה לא הפחיתה את ימי החופשה שכבר אושרו ונוצלו.
- **התיקון:** השוואת ימי הבקשה ליתרה בפועל: מכסה שנתית פחות ימים שאושרו.
- **הטסטים:** בדיקות לבקשה בתוך היתרה, לניצול היתרה המדויקת ולחריגה ממנה.

## 3. אישור בקשה (approve) ו־Concurrency
- בקשה שאינה קיימת מחזירה `404 Not Found`; ניסיון לאשר בקשה שכבר טופלה נדחה.
- למניעת אישור כפול, השתמשתי ב־Transaction ובנעילת הרשומה בעת שליפתה לעדכון (`SELECT FOR UPDATE`).
- בקשה מקבילה ממתינה לשחרור הנעילה, ולאחר מכן נבדק הסטטוס המעודכן.

## 4. על מה ויתרתי בגלל הזמן
- בדיקות אבטחה, למרות שזה בהחלט דבר חשוב.
- עם יום נוסף הייתי משקיעה בהרחבת בדיקות הקצה ובבדיקות נוספות של תרחישי concurrency, וכן בעיצוב קצת יותר נעים לענין.

## 5. שימוש ב־AI

### איפה AI עזר
1. **Refactoring ב־Angular:** הצעות להעברת קריאות HTTP ל־Service, הוספת טיפוסים ושיפור ניהול subscriptions. בדקתי והתאמתי לקוד הקיים.
2. **הפרדת שכבות ב־Backend:** נעזרתי בהצעות לחלוקת אחריות בין Controller ל־Service, תוך הימנעות משכבות מיותרות.

פרומפטים ל AI:
- Improve the backend architecture with minimal changes.

Currently, the Controller handles database access, business logic, and validation.

Refactor it to improve separation of concerns:

Move business logic into an appropriate Service layer.
Keep the Controller responsible for handling HTTP requests and responses.
Keep database access in the appropriate layer, following the existing project structure.
Move validation to an appropriate place without duplicating existing logic.
Avoid unnecessary abstractions, interfaces, or additional layers.
Preserve existing API behavior and functionality.
After the changes, briefly explain the architectural decisions and why this separation is appropriate for this project.

Run the relevant tests and build checks.

- Refactor the Angular component with minimal changes:

Move all HTTP calls from the component into an Angular service.
Replace all any types with proper TypeScript interfaces and types.
Use clean RxJS patterns and avoid nested subscriptions.
Prevent memory leaks using async pipe or takeUntilDestroyed, as appropriate.
Keep the component focused on UI logic and the service responsible for API communication.
Preserve existing functionality and UI behavior.
Avoid unnecessary abstractions, new libraries, or unrelated refactoring.
Run the relevant tests and build checks after the changes.


### איפה דחיתי/תיקנתי הצעה של AI
- כאשר ביקשתי ממנו להוסיף את הטופס - הוסיף על הקומפוננטה הקיימת, לדעתי זה דבר מינמלי של הפרדה שצריך לעשות.

### אבטחה
- לא הספקתי לטפל בזה

## 6. הוראות הרצה
-


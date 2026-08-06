import { DOCUMENT_CATEGORY, type AnalysisResult } from "@/lib/types";

// A hand-written, fictional analysis of a National Insurance (Bituach Leumi) income-support
// letter, rendered through the real AnalysisCard on /sample — no API call, no credits, no
// network. This is the demo's insurance policy: if Anthropic/Azure hiccup mid-pitch, this
// screen still works with Wi-Fi off.
//
// Two of the three requiredActions carry a hand-authored `hebrewPhrase` — the Hebrew sentence
// to say at the counter or on the phone, spoken with the Hebrew voice regardless of UI
// language. This is the demo slice of feature #4 (full version: extending the AI's tool schema
// to generate these for every real document — deferred, see the implementation plan, since it
// touches the analysis money-path and risks the measured 97% clean-parse rate).
//
// *6050 is National Insurance's real, publicly known short-dial number — used here only as a
// realistic example inside a fictional demo letter, not as a live claim about this document.
export const SAMPLE_ANALYSIS: AnalysisResult = {
  documentType: {
    he: "מכתב מהמוסד לביטוח לאומי",
    am: "ከብሔራዊ ኢንሹራንስ (ቢጡአኽ ለኡሚ) የተላከ ደብዳቤ",
    en: "Letter from National Insurance (Bituach Leumi)",
  },
  category: DOCUMENT_CATEGORY.Government,
  urgencyLevel: "High",
  summary: {
    he: "המוסד לביטוח לאומי שולח הודעה בנוגע לקצבת הבטחת ההכנסה שלכם — נדרש לעדכן פרטים עד למועד שצוין, אחרת התשלום עלול להיפסק.",
    am: "ብሔራዊ ኢንሹራንስ ተቋም ስለ ወርሃዊ የገቢ ማረጋገጫ አበልዎ ማሳወቂያ ልኳል — በተጠቀሰው ቀን በፊት መረጃዎን ማዘመን ያስፈልጋል፣ አለበለዚያ ክፍያው ሊቋረጥ ይችላል።",
    en: "National Insurance sent a notice about your monthly income-support allowance — you need to update your details by the date shown, or the payment may stop.",
  },
  explanation: {
    he: "המכתב מבקש מכם לעדכן את פרטי ההכנסה שלכם כדי להמשיך לקבל את קצבת הבטחת ההכנסה החודשית. עליכם לצרף תלושי שכר או אישור הכנסה עדכני, ולוודא שהמסמכים מגיעים למוסד לביטוח לאומי עד למועד שצוין במכתב. אם לא תגיבו עד אז, התשלום החודשי עלול להיפסק עד לקבלת המידע המעודכן. אפשר להגיש את המסמכים דרך האינטרנט, בדואר, או ישירות בסניף הקרוב.",
    am: "ደብዳቤው ወርሃዊ የገቢ ማረጋገጫ አበልዎን ማግኘት እንዲቀጥሉ የገቢ መረጃዎን እንዲያዘምኑ ይጠይቅዎታል። የቅርብ ጊዜ የደመወዝ ደረሰኞችን ወይም የገቢ ማረጋገጫ ማያያዝ እና ሰነዶቹ በደብዳቤው በተጠቀሰው ቀን በፊት ለብሔራዊ ኢንሹራንስ ተቋም መድረሳቸውን ማረጋገጥ ያስፈልግዎታል። እስከዚያ ካልመለሱ፣ የተዘመነው መረጃ እስኪደርስ ድረስ ወርሃዊ ክፍያው ሊቋረጥ ይችላል። ሰነዶቹን በኢንተርኔት፣ በፖስታ ወይም በቅርብ ቅርንጫፍ በቀጥታ ማስገባት ይችላሉ።",
    en: "The letter asks you to update your income details so you can keep receiving your monthly income-support allowance. You need to attach recent pay slips or proof of income, and make sure the documents reach National Insurance by the date shown in the letter. If you don't respond by then, the monthly payment may stop until the updated information arrives. You can submit the documents online, by mail, or directly at the nearest branch.",
  },
  keyPoints: [
    {
      he: "העדכון מתייחס לקצבת הבטחת ההכנסה החודשית שלכם.",
      am: "ማዘመኑ የሚመለከተው ወርሃዊ የገቢ ማረጋገጫ አበልዎን ነው።",
      en: "The update concerns your monthly income-support allowance.",
    },
    {
      he: "יש לצרף תלושי שכר או אישור הכנסה עדכני.",
      am: "የቅርብ ጊዜ የደመወዝ ደረሰኞችን ወይም የገቢ ማረጋገጫ ማያያዝ ያስፈልጋል።",
      en: "You need to attach recent pay slips or proof of income.",
    },
    {
      he: "אי מענה עד למועד עלול לגרום להפסקת התשלום החודשי.",
      am: "እስከ ቀነ-ገደቡ ምላሽ ካልሰጡ ወርሃዊ ክፍያው ሊቋረጥ ይችላል።",
      en: "Not responding by the deadline may stop your monthly payment.",
    },
    {
      he: "אפשר להגיש את המסמכים באינטרנט, בדואר או בסניף.",
      am: "ሰነዶቹን በኢንተርኔት፣ በፖስታ ወይም በቅርንጫፍ ማስገባት ይቻላል።",
      en: "You can submit the documents online, by mail, or at a branch.",
    },
  ],
  requiredActions: [
    {
      description: {
        he: "התקשרו למוקד הביטוח הלאומי כדי לוודא שהבקשה שלכם התקבלה",
        am: "ጥያቄዎ እንደደረሰ ለማረጋገጥ ወደ ብሔራዊ ኢንሹራንስ ማዕከል ይደውሉ",
        en: "Call the National Insurance call center to confirm they received your request",
      },
      isMandatory: true,
      hebrewPhrase: {
        say: "שלום, קיבלתי מכתב על עדכון הכנסה ואני רוצה לוודא שהמסמכים שלי התקבלו.",
        meaning: {
          he: "שלום, קיבלתי מכתב על עדכון הכנסה ואני רוצה לוודא שהמסמכים שלי התקבלו.",
          am: "ሰላም፣ ስለ ገቢ ማዘመን ደብዳቤ ደርሶኛል፣ ሰነዶቼ መድረሳቸውን ማረጋገጥ እፈልጋለሁ።",
          en: "Hello, I received a letter about an income update and I want to confirm my documents were received.",
        },
      },
      contactPhone: "*6050",
    },
    {
      description: {
        he: "הגישו תלושי שכר של שלושת החודשים האחרונים",
        am: "ላለፉት ሶስት ወራት የደመወዝ ደረሰኞችን ያስገቡ",
        en: "Submit pay slips from the last three months",
      },
      isMandatory: true,
      hebrewPhrase: {
        say: "אני רוצה להגיש תלושי שכר לעדכון קצבת הבטחת ההכנסה שלי.",
        meaning: {
          he: "אני רוצה להגיש תלושי שכר לעדכון קצבת הבטחת ההכנסה שלי.",
          am: "የገቢ ማረጋገጫ አበሌን ለማዘመን የደመወዝ ደረሰኞችን ማስገባት እፈልጋለሁ።",
          en: "I want to submit pay slips to update my income-support allowance.",
        },
      },
      contactPhone: null,
    },
    {
      description: {
        he: "שמרו העתק של כל המסמכים שהגשתם",
        am: "ያስገቡዋቸውን ሰነዶች ሁሉ ቅጂ ያስቀምጡ",
        en: "Keep a copy of everything you submit",
      },
      isMandatory: false,
    },
  ],
  deadlines: [
    {
      // Relative to load time so the demo never shows an expired date.
      date: new Date(Date.now() + 21 * 24 * 60 * 60 * 1000).toISOString(),
      description: {
        he: "מועד אחרון להגשת המסמכים המעודכנים",
        am: "ለተዘመኑ ሰነዶች ማቅረቢያ የመጨረሻ ቀን",
        en: "Deadline to submit the updated documents",
      },
    },
  ],
};

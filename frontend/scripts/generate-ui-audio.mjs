#!/usr/bin/env node
// Generates the small set of static MP3 clips this build ships instead of calling the API at
// runtime: the trilingual "choose your language" clips for LanguageGate (feature #2 slice), the
// /sample page's full spoken walkthrough per language, and the one Hebrew phrase-card clip
// (feature #3/#4 slices). All three exist so /sample and the language gate work with zero
// network calls and zero credits at demo time.
//
// Deliberately a plain Node script, not a dotnet tool — a dotnet tool would have to parse
// TypeScript to read frontend source, and buys nothing over ~15 lines of fetch(). Mirrors
// backend/src/AmharicHelper.Infrastructure/Tts/AzureTtsProvider.cs's exact request shape
// (same SSML wrapping, same header names, same output format) so this produces byte-identical
// audio to what the real app would generate for the same text.
//
// Usage:
//   AZURE_SPEECH_KEY=... AZURE_SPEECH_REGION=eastus node scripts/generate-ui-audio.mjs
//
// Never run this in CI or the Netlify build — there is no Azure key there, and a flaky Azure
// minute would fail a deploy. Output is committed to the repo like any other static asset.
// Re-runs are cheap: a manifest.json of sha1(text) per output path skips anything unchanged.

import { createHash } from "node:crypto";
import { mkdir, readFile, writeFile, access } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PUBLIC_AUDIO = path.join(__dirname, "..", "public", "audio");
const MANIFEST_PATH = path.join(PUBLIC_AUDIO, "manifest.json");

const REGION = process.env.AZURE_SPEECH_REGION || process.env.AZURE_TTS_REGION;
const KEY = process.env.AZURE_SPEECH_KEY || process.env.AZURE_TTS_KEY;

// Mirrors TtsOptions.cs defaults exactly.
const VOICE = {
  he: { voice: "he-IL-HilaNeural", locale: "he-IL" },
  am: { voice: "am-ET-MekdesNeural", locale: "am-ET" },
  en: { voice: "en-US-JennyNeural", locale: "en-US" },
};

// --- Feature #2 slice: the three LanguageGate "choose your language" clips. -----------------
const GATE_TEXT = {
  he: "בחרו שפה",
  am: "ቋንቋ ይምረጡ",
  en: "Choose your language",
};

// --- Feature #3 slice: /sample's full spoken walkthrough, one clip per language. ------------
// Section labels approximate backend SpokenTextBuilder.cs's own labels; this is a one-time
// generation script, not shipped code, so an exact match isn't required.
const SECTION_LABELS = {
  he: { summary: "סיכום", explanation: "הסבר מפורט", keyPoints: "נקודות מפתח", actions: "פעולות נדרשות", deadlines: "מועדים חשובים" },
  am: { summary: "ማጠቃለያ", explanation: "ዝርዝር ማብራሪያ", keyPoints: "ቁልፍ ነጥቦች", actions: "የሚያስፈልጉ እርምጃዎች", deadlines: "አስፈላጊ ቀኖች" },
  en: { summary: "Summary", explanation: "Explanation", keyPoints: "Key points", actions: "Required actions", deadlines: "Deadlines" },
};

const SAMPLE = {
  summary: {
    he: "המוסד לביטוח לאומי שולח הודעה בנוגע לקצבת הבטחת ההכנסה שלכם — נדרש לעדכן פרטים עד למועד שצוין, אחרת התשלום עלול להיפסק.",
    am: "ብሔራዊ ኢንሹራንስ ተቋም ስለ ወርሃዊ የገቢ ማረጋገጫ አበልዎ ማሳወቂያ ልኳል — በተጠቀሰው ቀን በፊት መረጃዎን ማዘመን ያስፈልጋል፣ አለበለዚያ ክፍያው ሊቋረጥ ይችላል።",
    en: "National Insurance sent a notice about your monthly income-support allowance — you need to update your details by the date shown, or the payment may stop.",
  },
  explanation: {
    he: "המכתב מבקש מכם לעדכן את פרטי ההכנסה שלכם כדי להמשיך לקבל את קצבת הבטחת ההכנסה החודשית. עליכם לצרף תלושי שכר או אישור הכנסה עדכני, ולוודא שהמסמכים מגיעים למוסד לביטוח לאומי עד למועד שצוין במכתב.",
    am: "ደብዳቤው ወርሃዊ የገቢ ማረጋገጫ አበልዎን ማግኘት እንዲቀጥሉ የገቢ መረጃዎን እንዲያዘምኑ ይጠይቅዎታል። የቅርብ ጊዜ የደመወዝ ደረሰኞችን ወይም የገቢ ማረጋገጫ ማያያዝ እና ሰነዶቹ በተጠቀሰው ቀን በፊት መድረሳቸውን ማረጋገጥ ያስፈልግዎታል።",
    en: "The letter asks you to update your income details so you can keep receiving your monthly income-support allowance. Attach recent pay slips or proof of income, and make sure the documents arrive by the date shown.",
  },
  keyPoints: {
    he: "העדכון מתייחס לקצבת הבטחת ההכנסה החודשית שלכם. יש לצרף תלושי שכר או אישור הכנסה עדכני. אי מענה עד למועד עלול לגרום להפסקת התשלום. אפשר להגיש את המסמכים באינטרנט, בדואר או בסניף.",
    am: "ማዘመኑ የሚመለከተው ወርሃዊ የገቢ ማረጋገጫ አበልዎን ነው። የቅርብ ጊዜ የደመወዝ ደረሰኞችን ማያያዝ ያስፈልጋል። እስከ ቀነ-ገደቡ ምላሽ ካልሰጡ ክፍያው ሊቋረጥ ይችላል። ሰነዶቹን በኢንተርኔት፣ በፖስታ ወይም በቅርንጫፍ ማስገባት ይቻላል።",
    en: "The update concerns your monthly income-support allowance. Attach recent pay slips or proof of income. Not responding by the deadline may stop your payment. You can submit documents online, by mail, or at a branch.",
  },
  actions: {
    he: "התקשרו למוקד הביטוח הלאומי כדי לוודא שהבקשה התקבלה. הגישו תלושי שכר של שלושת החודשים האחרונים. שמרו העתק של כל המסמכים שהגשתם.",
    am: "ጥያቄዎ እንደደረሰ ለማረጋገጥ ወደ ብሔራዊ ኢንሹራንስ ማዕከል ይደውሉ። ላለፉት ሶስት ወራት የደመወዝ ደረሰኞችን ያስገቡ። ያስገቡዋቸውን ሰነዶች ሁሉ ቅጂ ያስቀምጡ።",
    en: "Call the National Insurance call center to confirm your request was received. Submit pay slips from the last three months. Keep a copy of everything you submit.",
  },
  deadlines: {
    he: "מועד אחרון להגשת המסמכים המעודכנים.",
    am: "ለተዘመኑ ሰነዶች ማቅረቢያ የመጨረሻ ቀን።",
    en: "Deadline to submit the updated documents.",
  },
};

// --- Feature #4 slice: the one hand-authored Hebrew phrase card clip. Always the Hebrew
// voice, regardless of UI language — see PhraseCard.tsx / the type doc on HebrewPhrase.say.
const PHRASE_0_TEXT = "שלום, קיבלתי מכתב על עדכון הכנסה ואני רוצה לוודא שהמסמכים שלי התקבלו.";

function buildSampleFullText(lang) {
  const l = SECTION_LABELS[lang];
  return [
    `${l.summary}. ${SAMPLE.summary[lang]}`,
    `${l.explanation}. ${SAMPLE.explanation[lang]}`,
    `${l.keyPoints}. ${SAMPLE.keyPoints[lang]}`,
    `${l.actions}. ${SAMPLE.actions[lang]}`,
    `${l.deadlines}. ${SAMPLE.deadlines[lang]}`,
  ].join(" ");
}

function jobs() {
  const list = [];
  for (const lang of ["he", "am", "en"]) {
    list.push({ outPath: `gate/${lang}.mp3`, lang, text: GATE_TEXT[lang] });
    list.push({ outPath: `sample/${lang}/Full.mp3`, lang, text: buildSampleFullText(lang) });
  }
  list.push({ outPath: "sample/phrase-0.mp3", lang: "he", text: PHRASE_0_TEXT });
  return list;
}

function sha1(text) {
  return createHash("sha1").update(text, "utf8").digest("hex");
}

async function fileExists(p) {
  try { await access(p); return true; } catch { return false; }
}

async function synthesize(text, lang) {
  const { voice, locale } = VOICE[lang];
  const escaped = text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  const ssml = `<speak version='1.0' xml:lang='${locale}'><voice xml:lang='${locale}' name='${voice}'>${escaped}</voice></speak>`;

  const res = await fetch(`https://${REGION}.tts.speech.microsoft.com/cognitiveservices/v1`, {
    method: "POST",
    headers: {
      "Ocp-Apim-Subscription-Key": KEY,
      "Content-Type": "application/ssml+xml",
      "X-Microsoft-OutputFormat": "audio-24khz-48kbitrate-mono-mp3",
      "User-Agent": "Fana-audio-generator",
    },
    body: ssml,
  });

  if (!res.ok) {
    const body = await res.text().catch(() => "");
    throw new Error(`Azure TTS failed: ${res.status} ${res.statusText} ${body}`);
  }
  return Buffer.from(await res.arrayBuffer());
}

async function main() {
  if (!REGION || !KEY) {
    console.error(
      "Set AZURE_SPEECH_KEY and AZURE_SPEECH_REGION before running this script.\n" +
      "  AZURE_SPEECH_KEY=... AZURE_SPEECH_REGION=eastus node scripts/generate-ui-audio.mjs"
    );
    process.exit(1);
  }

  let manifest = {};
  if (await fileExists(MANIFEST_PATH)) {
    manifest = JSON.parse(await readFile(MANIFEST_PATH, "utf8"));
  }

  let generated = 0;
  let skipped = 0;

  for (const job of jobs()) {
    const outFile = path.join(PUBLIC_AUDIO, job.outPath);
    const hash = sha1(job.text);
    const unchanged = manifest[job.outPath] === hash && (await fileExists(outFile));
    if (unchanged) {
      skipped++;
      continue;
    }

    await mkdir(path.dirname(outFile), { recursive: true });
    console.log(`Generating ${job.outPath} (${job.lang})...`);
    const audio = await synthesize(job.text, job.lang);
    await writeFile(outFile, audio);
    manifest[job.outPath] = hash;
    generated++;
  }

  await writeFile(MANIFEST_PATH, JSON.stringify(manifest, null, 2) + "\n");
  console.log(`Done. ${generated} generated, ${skipped} unchanged.`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});

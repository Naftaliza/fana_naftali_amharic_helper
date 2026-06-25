// Shared frontend types, mirroring the backend DTOs.

export type Language = "he" | "am" | "en";

// `flag` is an ISO country code used to load a flag image (emoji flags don't render on Windows).
export const LANGUAGES: { code: Language; label: string; rtl: boolean; flag: string }[] = [
  { code: "he", label: "עברית", rtl: true, flag: "il" },
  { code: "am", label: "አማርኛ", rtl: false, flag: "et" }, // Amharic (Ethiopic) is written LTR.
  { code: "en", label: "English", rtl: false, flag: "gb" },
];

// Only Hebrew is rendered right-to-left.
export function isRtl(language: Language): boolean {
  return language === "he";
}

// Maps frontend language codes to the backend Language enum (Hebrew=0, Amharic=1, English=2).
export const LANGUAGE_ENUM: Record<Language, number> = { he: 0, am: 1, en: 2 };

export type UrgencyLevel = "Low" | "Medium" | "High" | "Critical";

export interface AnalysisResult {
  summary: string;
  documentType: string;
  urgencyLevel: UrgencyLevel;
  keyPoints: string[];
  requiredActions: { description: string; isMandatory: boolean }[];
  deadlines: { date: string | null; description: string }[];
  translatedAmharic: string;
  translatedSimpleHebrew: string;
}

export interface DocumentSummary {
  id: string;
  fileName: string;
  contentType: string;
  uploadedAt: string;
  hasAnalysis: boolean;
}

export interface DocumentDetail {
  id: string;
  fileName: string;
  contentType: string;
  ocrText: string | null;
  uploadedAt: string;
  analysis: AnalysisResult | null;
}

export interface ChatMessage {
  id: string;
  role: number; // 0 = user, 1 = assistant
  content: string;
  createdAt: string;
}

export interface AuthUser {
  id: string;
  email: string;
  displayName: string;
  preferredLanguage: number;
}

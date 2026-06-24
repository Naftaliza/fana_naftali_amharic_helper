// Shared frontend types, mirroring the backend DTOs.

export type Language = "he" | "am" | "en";

export const LANGUAGES: { code: Language; label: string; rtl: boolean }[] = [
  { code: "he", label: "עברית", rtl: true },
  { code: "am", label: "አማርኛ", rtl: false }, // Amharic (Ethiopic) is written LTR.
  { code: "en", label: "English", rtl: false },
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

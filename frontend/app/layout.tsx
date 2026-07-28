import type { Metadata, Viewport } from "next";
import { Noto_Sans_Hebrew, Noto_Sans_Ethiopic } from "next/font/google";
import "./globals.css";
import { LanguageProvider } from "@/lib/language-context";
import { AuthProvider } from "@/lib/auth-context";
import { OrganizationProvider } from "@/lib/organization-context";
import { ThemeProvider } from "@/lib/theme-context";
import { Navbar } from "@/components/Navbar";
import { SkipLink } from "@/components/SkipLink";
import { ServiceWorker } from "@/components/ServiceWorker";
import { AccessibilityWidget } from "@/components/AccessibilityWidget";
import { LanguageGate } from "@/components/LanguageGate";
import { Onboarding } from "@/components/Onboarding";
import { LeadFeedbackPrompt } from "@/components/LeadFeedbackPrompt";

// Runs before paint to set the theme + accessibility options + whether a language has already
// been chosen, avoiding any flash. Without the 'lang-chosen' class here, a returning visitor
// (localStorage already has 'lang' from a prior visit) would see LanguageGate render open for
// one frame — React can't know that until its own mount effect runs, which is after paint —
// then snap shut. globals.css hides the gate outright whenever this class is present, so the
// dialog never becomes visible in the first place, no matter how fast React's effect runs.
const themeScript = `(function(){try{var d=document.documentElement;
var t=localStorage.getItem('theme');if(!t){t=window.matchMedia('(prefers-color-scheme: dark)').matches?'dark':'light';}if(t==='dark'){d.classList.add('dark');}
var a=JSON.parse(localStorage.getItem('a11y')||'{}');
if(a.contrast)d.classList.add('a11y-contrast');
if(a.grayscale)d.classList.add('a11y-grayscale');
if(a.dyslexia)d.classList.add('a11y-dyslexia');
if(a.highlightLinks)d.classList.add('a11y-highlight-links');
if(a.scale&&a.scale!==1)d.style.fontSize=Math.round(a.scale*100)+'%';
if(localStorage.getItem('lang'))d.classList.add('lang-chosen');
}catch(e){}})();`;

const hebrew = Noto_Sans_Hebrew({ subsets: ["hebrew"], variable: "--font-noto", display: "swap" });
const ethiopic = Noto_Sans_Ethiopic({ subsets: ["ethiopic"], weight: ["400", "500", "700"], variable: "--font-ethiopic", display: "swap" });

export const metadata: Metadata = {
  title: "Amharic Helper",
  description: "Understand Hebrew documents in Amharic and simple Hebrew.",
  manifest: "/manifest.webmanifest",
  appleWebApp: { capable: true, statusBarStyle: "default", title: "Amharic Helper" },
  icons: { apple: "/icon-192.png" },
};

export const viewport: Viewport = {
  themeColor: "#2563eb",
  width: "device-width",
  initialScale: 1,
  viewportFit: "cover",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  // Default to Hebrew + RTL on the server; the LanguageProvider adjusts on the client.
  return (
    <html lang="he" dir="rtl" className={`${hebrew.variable} ${ethiopic.variable}`}>
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeScript }} />
      </head>
      <body>
        <ThemeProvider>
          <LanguageProvider>
            <AuthProvider>
              <OrganizationProvider>
                <SkipLink />
                <Navbar />
                <main id="main" className="mx-auto max-w-6xl px-4 py-8">{children}</main>
                <AccessibilityWidget />
                <LanguageGate />
                <Onboarding />
                <LeadFeedbackPrompt />
                <ServiceWorker />
              </OrganizationProvider>
            </AuthProvider>
          </LanguageProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}

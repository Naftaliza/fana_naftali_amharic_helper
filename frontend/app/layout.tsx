import type { Metadata, Viewport } from "next";
import { Noto_Sans_Hebrew, Noto_Sans_Ethiopic } from "next/font/google";
import "./globals.css";
import { LanguageProvider } from "@/lib/language-context";
import { AuthProvider } from "@/lib/auth-context";
import { OrganizationProvider } from "@/lib/organization-context";
import { ThemeProvider } from "@/lib/theme-context";
import { Navbar } from "@/components/Navbar";
import { BottomNav } from "@/components/BottomNav";
import { RouteAnnouncer } from "@/components/RouteAnnouncer";
import { SkipLink } from "@/components/SkipLink";
import { ServiceWorker } from "@/components/ServiceWorker";
import { AccessibilityWidget } from "@/components/AccessibilityWidget";
import { LanguageGate } from "@/components/LanguageGate";
import { Onboarding } from "@/components/Onboarding";
import { OnboardingProvider } from "@/lib/onboarding-context";
import { LeadFeedbackPrompt } from "@/components/LeadFeedbackPrompt";
import { SaveTrialAnalysisPrompt } from "@/components/SaveTrialAnalysisPrompt";
import { RedeemPendingPrompt } from "@/components/RedeemPendingPrompt";
import { OfflineBanner } from "@/components/OfflineBanner";
import { InstallPrompt } from "@/components/InstallPrompt";

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

const SITE_URL = process.env.NEXT_PUBLIC_SITE_URL ?? "https://fana.app";
const DESCRIPTION = "פאנא · ፋና · Fana — understand Hebrew documents in Amharic and simple Hebrew.";

export const metadata: Metadata = {
  metadataBase: new URL(SITE_URL),
  title: "Fana · פאנא · ፋና",
  description: DESCRIPTION,
  manifest: "/manifest.webmanifest",
  appleWebApp: { capable: true, statusBarStyle: "default", title: "Fana" },
  icons: { apple: "/icon-192.png" },
  openGraph: {
    title: "Fana",
    description: DESCRIPTION,
    type: "website",
    locale: "he_IL",
    alternateLocale: ["am_ET", "en_US"],
  },
  twitter: {
    card: "summary_large_image",
    title: "Fana",
    description: DESCRIPTION,
  },
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
                <OnboardingProvider>
                  <SkipLink />
                  {/* BottomNav is always position:fixed (bottom on mobile, top-under-Navbar on
                      desktop — see the component), so unlike Navbar/main it doesn't need to sit
                      in document flow, and unlike the other fixed overlays below it doesn't
                      need data-a11y-scope's content to be its sibling either. It's rendered here
                      only for code-adjacency to Navbar; fixed positioning makes its actual DOM
                      placement irrelevant. */}
                  <BottomNav />
                  {/* data-a11y-scope: the grayscale filter (globals.css) applies here, not on
                      <html>/<body> — a filtered element becomes the containing block for its
                      position:fixed descendants, which would silently reposition every fixed
                      overlay below (LanguageGate, Onboarding, the prompts, BottomNav above)
                      relative to this box instead of the viewport. Keeping those overlays as
                      siblings, outside this div, is what avoids that; each grays itself out
                      individually via data-a11y-fixed instead. */}
                  <div data-a11y-scope>
                    <Navbar />
                    <OfflineBanner />
                    {/* pb-24 clears the fixed bottom tab bar on mobile; pt-24 on desktop clears
                        BottomNav's fixed top-under-Navbar row instead — so the last (and first)
                        element of every page stays reachable instead of sitting behind either. */}
                    <main id="main" className="mx-auto max-w-6xl px-4 pb-24 pt-8 md:pb-8 md:pt-24">{children}</main>
                  </div>
                  <AccessibilityWidget />
                  <LanguageGate />
                  <Onboarding />
                  <LeadFeedbackPrompt />
                  <SaveTrialAnalysisPrompt />
                  <RedeemPendingPrompt />
                  <InstallPrompt />
                  <RouteAnnouncer />
                  <ServiceWorker />
                </OnboardingProvider>
              </OrganizationProvider>
            </AuthProvider>
          </LanguageProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}

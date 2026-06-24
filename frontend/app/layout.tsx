import type { Metadata, Viewport } from "next";
import { Noto_Sans_Hebrew, Noto_Sans_Ethiopic } from "next/font/google";
import "./globals.css";
import { LanguageProvider } from "@/lib/language-context";
import { AuthProvider } from "@/lib/auth-context";
import { Navbar } from "@/components/Navbar";
import { ServiceWorker } from "@/components/ServiceWorker";

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
      <body>
        <LanguageProvider>
          <AuthProvider>
            <Navbar />
            <main className="mx-auto max-w-6xl px-4 py-8">{children}</main>
            <ServiceWorker />
          </AuthProvider>
        </LanguageProvider>
      </body>
    </html>
  );
}

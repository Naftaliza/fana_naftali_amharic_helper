"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { FileText, Menu, X, Check, LayoutDashboard, User, LogOut, LogIn, UserPlus } from "lucide-react";
import { useAuth } from "@/lib/auth-context";
import { useLanguage } from "@/lib/language-context";
import { LANGUAGES } from "@/lib/types";

export function Navbar() {
  const { t, language, setLanguage } = useLanguage();
  const { user, logout } = useAuth();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const pathname = usePathname();

  // Close the menu on outside click and whenever the route changes.
  useEffect(() => {
    const onClick = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", onClick);
    return () => document.removeEventListener("mousedown", onClick);
  }, []);
  useEffect(() => setOpen(false), [pathname]);

  const itemClass =
    "flex w-full items-center gap-3 px-4 py-3 text-start text-base text-gray-700 hover:bg-gray-50";

  return (
    <header className="pt-safe sticky top-0 z-40 border-b border-gray-100 bg-white/85 backdrop-blur">
      <nav className="mx-auto flex h-16 max-w-6xl items-center justify-between px-4">
        <Link href="/" className="flex items-center gap-2 text-lg font-bold text-brand">
          <span className="grid h-9 w-9 place-items-center rounded-xl bg-brand-gradient text-white">
            <FileText className="h-5 w-5" />
          </span>
          <span>{t("app.name")}</span>
        </Link>

        <div ref={ref} className="relative">
          <button
            onClick={() => setOpen((o) => !o)}
            aria-label={t("nav.menu")}
            aria-expanded={open}
            className="grid h-11 w-11 place-items-center rounded-xl border border-gray-200 bg-white text-gray-700 hover:bg-gray-50"
          >
            {open ? <X className="h-6 w-6" /> : <Menu className="h-6 w-6" />}
          </button>

          {open && (
            <div className="absolute end-0 z-50 mt-2 w-64 overflow-hidden rounded-2xl border border-gray-100 bg-white shadow-soft">
              {/* Language preference */}
              <p className="px-4 pt-3 pb-1 text-xs font-semibold uppercase tracking-wide text-gray-400">
                {t("nav.language")}
              </p>
              {LANGUAGES.map((l) => (
                <button
                  key={l.code}
                  onClick={() => setLanguage(l.code)}
                  className={`${itemClass} justify-between ${language === l.code ? "text-brand" : ""}`}
                >
                  {l.label}
                  {language === l.code && <Check className="h-4 w-4" />}
                </button>
              ))}

              <div className="my-1 border-t border-gray-100" />

              {/* Account / navigation */}
              {user ? (
                <>
                  <Link href="/dashboard" className={itemClass}>
                    <LayoutDashboard className="h-5 w-5 text-brand" />{t("nav.dashboard")}
                  </Link>
                  <Link href="/profile" className={itemClass}>
                    <User className="h-5 w-5 text-brand" />{t("nav.profile")}
                  </Link>
                  <button onClick={() => { setOpen(false); logout(); }} className={`${itemClass} text-red-600`}>
                    <LogOut className="h-5 w-5" />{t("nav.logout")}
                  </button>
                </>
              ) : (
                <>
                  <Link href="/login" className={itemClass}>
                    <LogIn className="h-5 w-5 text-brand" />{t("nav.login")}
                  </Link>
                  <Link href="/register" className={itemClass}>
                    <UserPlus className="h-5 w-5 text-brand" />{t("nav.register")}
                  </Link>
                </>
              )}
            </div>
          )}
        </div>
      </nav>
    </header>
  );
}
